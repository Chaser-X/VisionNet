using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

namespace VisionNet.Controls
{
    /// <summary>
    /// Creates and releases the OpenGL objects owned by <see cref="CxDisplay"/> on behalf of its
    /// render items: VAOs, VBOs, index buffers, textures and the per-handle shader program.
    /// </summary>
    /// <remarks>
    /// Stateless — every method takes the handle it acts on. The GL context must be current on the
    /// calling thread; this class never makes a context current itself.
    /// </remarks>
    internal static class CxGLResourceManager
    {
        private static void UploadPinned(BufferTarget target, Array data, int bytes, BufferUsageHint usage)
        {
            GCHandle pinned = default(GCHandle);
            try
            {
                pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
                GL.BufferData(target, bytes, pinned.AddrOfPinnedObject(), usage);
            }
            finally
            {
                if (pinned.IsAllocated) pinned.Free();
            }
        }

        /// <summary>
        /// Creates one vertex buffer for <paramref name="handle"/>, uploads <paramref name="data"/>
        /// into it, and wires it to <paramref name="attributeIndex"/>. Buffers are always float
        /// data, so the byte count is derived from the array length.
        /// </summary>
        /// <param name="vboIndex">Slot in <see cref="GLResourceHandle.VboIds"/> to fill.</param>
        /// <param name="componentCount">Components per vertex (3 for positions/colours, 2 for UVs, 1 for scalars).</param>
        /// <param name="stride">Byte stride between vertices; 0 means tightly packed.</param>
        /// <returns>The next free VBO slot index.</returns>
        private static int UploadVertexBuffer(GLResourceHandle handle, int vboIndex,
            Array data, uint attributeIndex, int componentCount, int stride)
        {
            var tempId = new uint[1];
            GL.GenBuffers(1, tempId);
            handle.VboIds[vboIndex] = tempId[0];
            GL.BindBuffer(BufferTarget.ArrayBuffer, handle.VboIds[vboIndex]);
            UploadPinned(BufferTarget.ArrayBuffer, data,
                data.Length * sizeof(float), BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(attributeIndex, componentCount, VertexAttribPointerType.Float, false,
                stride, IntPtr.Zero);
            GL.EnableVertexAttribArray(attributeIndex);
            return vboIndex + 1;
        }

        /// <summary>
        /// Creates or rebuilds every GL object the item needs from its CPU-side render data.
        /// </summary>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="item">Item whose data is being uploaded.</param>
        /// <param name="handle">Handle receiving the created names.</param>
        /// <returns>
        /// <c>false</c> only when the item's shader failed to compile, which marks the handle
        /// invalid so the caller can report the failure. A missing render data set is a no-op
        /// and still reports success.
        /// </returns>
        public static bool Create(CxGlContext gl, ICxObjRenderItem item, GLResourceHandle handle)
        {
            var data = item.PrepareRenderData();
            if (data == null) return true;

            handle.ShaderFailed = false;
            handle.OwnsShader = false;

            // Compile or acquire the shader first: a failing shader aborts before any buffer or
            // texture upload. The three built-in ComplexGeometry items share one program per
            // OpenGL context; custom shader sources retain the old per-handle ownership path.
            if (data.ShaderSource != null)
            {
                bool sharedProgram = item is AbstractCxObjRenderItem builtIn
                                     && builtIn.UsesSharedSurfaceProgram;
                if (sharedProgram)
                {
                    var renderer = ModernGLRenderer.For(gl);
                    if (renderer.EnsureSurfaceProgram())
                    {
                        handle.ShaderProgram = renderer.SurfaceProgram;
                    }
                }
                else
                {
                    handle.ShaderProgram = ShaderProgramCompiler.CompileProgram(
                        data.ShaderSource.VertexSource, data.ShaderSource.FragmentSource);
                    handle.OwnsShader = true;
                }
                handle.HasShader = handle.ShaderProgram != 0;
                handle.UniformLocations.Clear();
                if (!handle.HasShader)
                {
                    // Remember the failure so ProcessResourcePool does not retry every frame.
                    handle.ShaderFailed = true;
                    handle.IsValid = false;
                    handle.NeedsUpdate = false;
                    // Reported by the caller after the resource lock is released.
                    return false;
                }
            }

            if (data.VertexCount > 0)
            {
                var tempId = new uint[1];
                GL.GenVertexArrays(1, tempId);
                handle.VaoId = tempId[0];
                handle.HasVAO = true;
                GL.BindVertexArray(handle.VaoId);

                int vboIndex = 0;

                if (data.Vertices != null && data.Vertices.Length >= data.VertexCount * 3)
                    vboIndex = UploadVertexBuffer(handle, vboIndex, data.Vertices,
                        attributeIndex: 0, componentCount: 3, stride: 3 * sizeof(float));

                if (data.Colors != null && data.Colors.Length >= data.VertexCount * 3)
                    vboIndex = UploadVertexBuffer(handle, vboIndex, data.Colors,
                        attributeIndex: 1, componentCount: 3, stride: 3 * sizeof(float));
                else if (data.UVCoords != null && data.UVCoords.Length >= data.VertexCount * 2)
                    vboIndex = UploadVertexBuffer(handle, vboIndex, data.UVCoords,
                        attributeIndex: 1, componentCount: 2, stride: 2 * sizeof(float));

                if (data.DiffValues != null && data.DiffValues.Length >= data.VertexCount)
                    vboIndex = UploadVertexBuffer(handle, vboIndex, data.DiffValues,
                        attributeIndex: 2, componentCount: 1, stride: 0);

                if (data.IntensityValues != null && data.IntensityValues.Length >= data.VertexCount)
                    vboIndex = UploadVertexBuffer(handle, vboIndex, data.IntensityValues,
                        attributeIndex: 3, componentCount: 1, stride: sizeof(float));

                if (data.Normals != null && data.Normals.Length >= data.VertexCount * 3)
                    vboIndex = UploadVertexBuffer(handle, vboIndex, data.Normals,
                        attributeIndex: 4, componentCount: 3, stride: 3 * sizeof(float));

                handle.VboCount = vboIndex;

                if (data.Indices != null && data.Indices.Length > 0)
                {
                    GL.GenBuffers(1, tempId);
                    handle.ElementBufferId = tempId[0];
                    handle.HasEBO = true;

                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, handle.ElementBufferId);
                    int bytes = data.Indices.Length * sizeof(uint);
                    UploadPinned(BufferTarget.ElementArrayBuffer, data.Indices,
                        bytes, BufferUsageHint.StaticDraw);
                }
            }

            if (data.TextureData?.Data != null)
            {
                handle.TextureId = CreateTexture(data.TextureData);
                handle.HasTexture = true;
            }

            if (handle.HasVAO)
                GL.BindVertexArray(0);

            handle.IsValid = true;
            handle.NeedsUpdate = false;
            return true;
        }

        /// <summary>
        /// Deletes every GL object named by <paramref name="handle"/> and resets it to the
        /// "no resources" state. Safe to call on a handle that owns nothing.
        /// </summary>
        public static void Release(GLResourceHandle handle)
        {
            if (handle.VboCount > 0)
            {
                GL.DeleteBuffers(handle.VboCount, handle.VboIds);
                for (int i = 0; i < handle.VboIds.Length; i++) handle.VboIds[i] = 0;
                handle.VboCount = 0;
            }

            if (handle.HasVAO && handle.VaoId != 0)
            {
                GL.DeleteVertexArrays(1, new[] { handle.VaoId });
                handle.VaoId = 0;
                handle.HasVAO = false;
            }

            if (handle.HasEBO && handle.ElementBufferId != 0)
            {
                GL.DeleteBuffers(1, new[] { handle.ElementBufferId });
                handle.ElementBufferId = 0;
                handle.HasEBO = false;
            }

            if (handle.HasShader && handle.OwnsShader && handle.ShaderProgram != 0)
            {
                GL.DeleteProgram(handle.ShaderProgram);
            }
            handle.ShaderProgram = 0;
            handle.HasShader = false;
            handle.OwnsShader = false;
            handle.UniformLocations.Clear();

            if (handle.HasTexture && handle.TextureId != 0)
            {
                GL.DeleteTextures(1, new[] { handle.TextureId });
                handle.TextureId = 0;
                handle.HasTexture = false;
            }

            handle.IsValid = false;
        }

        private static uint CreateTexture(TextureData tex)
        {
            int maxSize = GL.GetInteger(GetPName.MaxTextureSize);

            int uploadW = Math.Min(tex.Width,  maxSize);
            int uploadH = Math.Min(tex.Height, maxSize);
            byte[] uploadData = (uploadW != tex.Width || uploadH != tex.Height)
                ? DownsampleTextureRGBA(tex.Data, tex.Width, tex.Height, uploadW, uploadH)
                : tex.Data;

            var ids = new uint[1];
            GL.GenTextures(1, ids);
            GL.BindTexture(TextureTarget.Texture2D, ids[0]);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);

            GCHandle pinned = default(GCHandle);
            try
            {
                pinned = GCHandle.Alloc(uploadData, GCHandleType.Pinned);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                    uploadW, uploadH, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
                    pinned.AddrOfPinnedObject());
            }
            finally
            {
                if (pinned.IsAllocated) pinned.Free();
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }

            return ids[0];
        }

        private static byte[] DownsampleTextureRGBA(
            byte[] src, int srcW, int srcH, int dstW, int dstH)
        {
            float scaleX = (float)srcW / dstW;
            float scaleY = (float)srcH / dstH;
            var dst = new byte[dstW * dstH * 4];

            for (int y = 0; y < dstH; y++)
            {
                int srcY = Math.Min((int)(y * scaleY), srcH - 1);
                for (int x = 0; x < dstW; x++)
                {
                    int srcX = Math.Min((int)(x * scaleX), srcW - 1);
                    int si = (srcY * srcW + srcX) * 4;
                    int di = (y    * dstW + x)    * 4;
                    dst[di]     = src[si];
                    dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2];
                    dst[di + 3] = src[si + 3];
                }
            }

            return dst;
        }
    }
}
