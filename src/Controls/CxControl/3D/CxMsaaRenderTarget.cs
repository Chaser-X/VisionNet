using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;

namespace VisionNet.Controls
{
    /// <summary>
    /// Off-screen render target for <see cref="CxDisplay"/>: a multisampled colour+depth
    /// framebuffer that the scene is drawn into and then resolved to the window, plus a 1×1
    /// single-sample depth framebuffer that answers depth-buffer picking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Splitting display from computation is what lets MSAA and depth-based picking coexist: a
    /// multisampled depth buffer cannot be read back with <c>glReadPixels</c>, so picking reads a
    /// <b>resolved single sample</b> instead. The scene is still drawn once — the pick depth is
    /// blitted out of the same multisampled attachment rather than re-rendering the scene.
    /// </para>
    /// <para>
    /// The 1-pixel depth read is issued into a pixel-pack buffer and completed on a later frame
    /// (see <see cref="BeginDepthRead"/> / <see cref="TryEndDepthRead"/>), so it never blocks the
    /// UI thread on the GPU finishing the frame.
    /// </para>
    /// <para>All methods require the owning GL context to be current.</para>
    /// </remarks>
    internal sealed class CxMsaaRenderTarget : IDisposable
    {
        /// <summary>MSAA sample count requested from the driver (clamped to what it grants).</summary>
        private const int MaxRequestedSamples = 4;

        // Multisampled render target the scene is drawn into.
        private int _fbo;
        private int _colorRbo;
        private int _depthRbo;
        private int _samples;
        private int _width;
        private int _height;

        // 1×1 single-sample depth target that picking reads from.
        private int _pickFbo;
        private int _pickDepthRbo;

        // Asynchronous 1-pixel depth read.
        private int _pbo;
        private IntPtr _fence = IntPtr.Zero;
        private bool _readPending;

        private readonly float[] _depthScratch = new float[1];

        /// <summary>Sample count actually granted by the driver (0 = MSAA unavailable).</summary>
        public int Samples => _samples;

        /// <summary><c>true</c> once a complete render target exists.</summary>
        public bool IsCreated => _fbo != 0;

        /// <summary>
        /// <c>true</c> while an asynchronous 1-pixel depth read can be started. True whenever the
        /// pack buffer exists, which is independent of whether the multisampled framebuffer is
        /// currently in use (it stays available when MSAA is off, reading the window's depth buffer).
        /// </summary>
        public bool CanReadDepth => _pbo != 0;

        /// <summary>
        /// Resolves the scene depth at a control-space pixel: from the control's off-screen target
        /// when one exists, otherwise from the window framebuffer. This is the single depth source
        /// used by picking and the camera's click-to-focus. Requires the GL context to be current.
        /// </summary>
        public static float? ReadDepthAtPixel(CxGlContext gl, int controlX, int controlY)
        {
            if (gl == null) return null;

            CxMsaaRenderTarget target = gl.DepthTarget;
            if (target != null && target.IsCreated)
                return target.ReadDepthAt(controlX, controlY);

            // Fallback: the control is drawing straight to the window, so read its depth buffer.
            int glY = gl.Height - 1 - controlY;   // GL Y is bottom-up.
            if (controlX < 0 || controlX >= gl.Width || glY < 0 || glY >= gl.Height) return null;
            float[] depth = new float[1];
            GL.ReadPixels(controlX, glY, 1, 1, PixelFormat.DepthComponent, PixelType.Float, depth);
            return depth[0];
        }

        /// <summary>
        /// Creates the render target for the given drawable size, or re-creates it after a resize.
        /// Returns <c>false</c> when no usable framebuffer could be built (the caller then falls
        /// back to drawing straight to the window).
        /// </summary>
        public bool EnsureCreated(int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            if (_fbo != 0 && _width == width && _height == height) return true;

            Destroy();
            _width = width;
            _height = height;

            int maxSamples;
            try { maxSamples = GL.GetInteger(GetPName.MaxSamples); }
            catch { maxSamples = 0; }
            int samples = Math.Max(0, Math.Min(MaxRequestedSamples, maxSamples));

            _fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

            _colorRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _colorRbo);
            StoreStorage(_colorRbo, samples, RenderbufferStorage.Rgba8, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _colorRbo);

            _depthRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRbo);
            StoreStorage(_depthRbo, samples, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthRbo);

            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Debug.WriteLine("[CxControl] MSAA render target incomplete: " + status);
                Destroy();
                return false;
            }
            _samples = samples;
            Debug.WriteLine(string.Format(
                "[CxControl] MSAA render target: {0}x{1}, samples={2} (requested {3})",
                width, height, samples, MaxRequestedSamples));

            // 1×1 single-sample depth target for picking. Depth-only, so both the draw and read
            // buffers are disabled or the framebuffer would be incomplete.
            _pickFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pickFbo);
            _pickDepthRbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _pickDepthRbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.DepthComponent24, 1, 1);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _pickDepthRbo);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Debug.WriteLine("[CxControl] pick framebuffer incomplete: " + status);
                Destroy();
                return false;
            }

            _pbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbo);
            GL.BufferData(BufferTarget.PixelPackBuffer, sizeof(float), IntPtr.Zero, BufferUsageHint.StreamRead);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            return true;
        }

        private static void StoreStorage(int rbo, int samples, RenderbufferStorage format, int width, int height)
        {
            if (samples > 0)
                GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, format, width, height);
            else
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, format, width, height);
        }

        /// <summary>Binds the multisampled framebuffer and its viewport for scene rendering.</summary>
        public void BindForRender()
        {
            if (_fbo == 0) return;
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            GL.Viewport(0, 0, _width, _height);
        }

        /// <summary>Resolves the multisampled colour buffer into the window's back buffer.</summary>
        public void ResolveToDefault()
        {
            if (_fbo == 0) return;
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        /// <summary>
        /// Synchronously reads the resolved depth at a control-space pixel. Used by the click path,
        /// where a single blocking read is acceptable.
        /// </summary>
        public float? ReadDepthAt(int controlX, int controlY)
        {
            if (!BlitDepthToPick(controlX, controlY)) return null;
            GL.ReadPixels(0, 0, 1, 1, PixelFormat.DepthComponent, PixelType.Float, _depthScratch);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            return _depthScratch[0];
        }

        /// <summary>
        /// Blits the resolved depth at a control-space pixel into the 1×1 target and starts an
        /// asynchronous read of it into the pack buffer. When the multisampled framebuffer is not in
        /// use (MSAA off) the read is taken straight from the window's depth buffer instead, so the
        /// asynchronous path is available in both modes. Returns <c>false</c> when a read is already
        /// in flight or the pixel is outside the drawable.
        /// </summary>
        /// <param name="controlX">Pixel X in control coordinates.</param>
        /// <param name="controlY">Pixel Y in control coordinates (top-down).</param>
        /// <param name="viewportWidth">Drawable width, for the window-fallback bounds check.</param>
        /// <param name="viewportHeight">Drawable height, for the window-fallback bounds check.</param>
        public bool BeginDepthRead(int controlX, int controlY, int viewportWidth, int viewportHeight)
        {
            if (_readPending || _pbo == 0) return false;

            int readX, readY;
            if (_fbo != 0)
            {
                if (!BlitDepthToPick(controlX, controlY)) return false;
                readX = 0;
                readY = 0;   // the 1×1 pick framebuffer
            }
            else
            {
                // No off-screen target: read the window's own depth buffer.
                int glY = viewportHeight - 1 - controlY;
                if (controlX < 0 || controlX >= viewportWidth || glY < 0 || glY >= viewportHeight)
                    return false;
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                readX = controlX;
                readY = glY;
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbo);
            GL.ReadPixels(readX, readY, 1, 1, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            _fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
            if (_fbo != 0) GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            _readPending = true;
            return true;
        }

        /// <summary>
        /// Completes a read started by <see cref="BeginDepthRead"/> if the GPU has finished it.
        /// Returns <c>false</c> (without blocking) while the result is not ready yet.
        /// </summary>
        public bool TryEndDepthRead(out float depth)
        {
            depth = 1f;
            if (!_readPending) return false;

            if (_fence != IntPtr.Zero)
            {
                WaitSyncStatus status = GL.ClientWaitSync(_fence, ClientWaitSyncFlags.None, 0L);
                if (status == WaitSyncStatus.TimeoutExpired || status == WaitSyncStatus.WaitFailed)
                    return false;
                GL.DeleteSync(_fence);
                _fence = IntPtr.Zero;
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbo);
            GL.GetBufferSubData(BufferTarget.PixelPackBuffer, IntPtr.Zero, sizeof(float), _depthScratch);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
            depth = _depthScratch[0];
            _readPending = false;
            return true;
        }

        /// <summary>Drops an in-flight read without consuming it (e.g. the mouse button went down).</summary>
        public void CancelDepthRead()
        {
            if (_fence != IntPtr.Zero) { GL.DeleteSync(_fence); _fence = IntPtr.Zero; }
            _readPending = false;
        }

        /// <summary>
        /// Blits one depth texel out of the multisampled attachment into the 1×1 single-sample
        /// target. The depth resolve selects a single sample, which is what makes it readable.
        /// </summary>
        private bool BlitDepthToPick(int controlX, int controlY)
        {
            if (_fbo == 0 || _pickFbo == 0) return false;

            int glY = _height - 1 - controlY;   // control space is top-down, GL is bottom-up.
            if (controlX < 0 || controlX >= _width || glY < 0 || glY >= _height) return false;

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _pickFbo);
            GL.BlitFramebuffer(controlX, glY, controlX + 1, glY + 1, 0, 0, 1, 1,
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            // Make the 1×1 target the read framebuffer so ReadPixels reads it, not the source.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pickFbo);
            return true;
        }

        /// <inheritdoc/>
        public void Dispose() => Destroy();

        /// <summary>
        /// Destroys the multisampled framebuffer and its attachments but keeps the pack buffer, so
        /// the asynchronous depth read keeps working against the window's depth buffer. Used when the
        /// control renders straight to the window (MSAA off).
        /// </summary>
        public void ReleaseFramebuffer()
        {
            if (_pickDepthRbo != 0) { GL.DeleteRenderbuffer(_pickDepthRbo); _pickDepthRbo = 0; }
            if (_pickFbo != 0) { GL.DeleteFramebuffer(_pickFbo); _pickFbo = 0; }
            if (_depthRbo != 0) { GL.DeleteRenderbuffer(_depthRbo); _depthRbo = 0; }
            if (_colorRbo != 0) { GL.DeleteRenderbuffer(_colorRbo); _colorRbo = 0; }
            if (_fbo != 0) { GL.DeleteFramebuffer(_fbo); _fbo = 0; }
            _samples = 0;
            _width = _height = 0;
        }

        private void Destroy()
        {
            if (_fence != IntPtr.Zero) { GL.DeleteSync(_fence); _fence = IntPtr.Zero; }
            _readPending = false;

            if (_pbo != 0) { GL.DeleteBuffer(_pbo); _pbo = 0; }
            if (_pickDepthRbo != 0) { GL.DeleteRenderbuffer(_pickDepthRbo); _pickDepthRbo = 0; }
            if (_pickFbo != 0) { GL.DeleteFramebuffer(_pickFbo); _pickFbo = 0; }
            if (_depthRbo != 0) { GL.DeleteRenderbuffer(_depthRbo); _depthRbo = 0; }
            if (_colorRbo != 0) { GL.DeleteRenderbuffer(_colorRbo); _colorRbo = 0; }
            if (_fbo != 0) { GL.DeleteFramebuffer(_fbo); _fbo = 0; }

            _samples = 0;
            _width = _height = 0;
        }
    }
}
