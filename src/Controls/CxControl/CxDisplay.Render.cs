using System.Collections.Generic;
using SharpGL;
using SharpGL.SceneGraph;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
        /// <inheritdoc/>
        protected override void DoOpenGLInitialized()
        {
            base.DoOpenGLInitialized();
            OpenGL.ClearColor(0, 0, 0, 0);
            OpenGL.PointSize(2.0f);
        }

        /// <summary>
        /// Main render callback. Releases deferred GL resources, creates new ones,
        /// then issues all draw calls.
        /// </summary>
        protected override void DoOpenGLDraw(RenderEventArgs e)
        {
            if (DesignMode) return;
            base.DoOpenGLDraw(e);

            var gl = OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            // 1. Release GL resources that were dequeued by Dispose/Replace on another thread.
            ProcessPendingRelease(gl);

            // 2. Create or update GL resources for all items (full lock prevents concurrent Dispose).
            ProcessResourcePool(gl);

            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LESS);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LoadIdentity();

            _camera.LookAtMatrix(gl);
            Render(gl);

            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Disable(OpenGL.GL_BLEND);
        }

        /// <summary>Releases all GL resource handles waiting in the deferred-release queue.</summary>
        private void ProcessPendingRelease(OpenGL gl)
        {
            while (_pendingRelease.TryDequeue(out var handle))
                ReleaseGLResources(gl, handle);
        }

        /// <summary>
        /// Iterates the resource pool under full lock.
        /// Creates GL resources for new items and recreates them for items whose data changed.
        /// </summary>
        private void ProcessResourcePool(OpenGL gl)
        {
            lock (_resourceLock)
            {
                foreach (var kv in _resourcePool)
                {
                    var item   = kv.Key;
                    var handle = kv.Value;

                    if (item.IsDisposed) continue;

                    if (!handle.IsValid || handle.NeedsUpdate)
                    {
                        if (handle.IsValid)
                            ReleaseGLResources(gl, handle);

                        CreateGLResources(gl, item, handle);
                    }
                }
            }
        }

        /// <summary>
        /// Issues all draw calls for one frame: coordinate system, surface items (with unified Z range),
        /// color bar, coord tag, and overlay geometry.
        /// </summary>
        private void Render(OpenGL gl)
        {
            if (!_camera.Enable2DView && ShowCoordinateSystem)
                _coordinationItem.Draw(gl);

            // Take a lock-free snapshot to avoid holding the lock during GL calls.
            List<ICxObjRenderItem> snapshot;
            lock (_resourceLock)
                snapshot = new List<ICxObjRenderItem>(_surfaceItems);

            // Phase 1: compute global Z range across all non-intensity items.
            float globalZMin = float.MaxValue, globalZMax = float.MinValue;
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;
                if (cur.SurfaceColorMode != SurfaceColorMode.Intensity)
                {
                    if (cur.ZMin < globalZMin) globalZMin = cur.ZMin;
                    if (cur.ZMax > globalZMax) globalZMax = cur.ZMax;
                }
            }

            // Phase 2: propagate unified Z range so all items share the same color mapping.
            if (globalZMin < globalZMax)
            {
                foreach (var cur in snapshot)
                {
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalZRange(globalZMin, globalZMax);
                }
            }

            // Phase 3: draw all surface items.
            bool anyDrawn = false;
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                GLResourceHandle handle;
                lock (_resourceLock)
                    _resourcePool.TryGetValue(cur, out handle);

                if (handle?.IsValid != true) continue;

                cur.Draw(gl, handle);
                anyDrawn = true;
            }

            // Phase 4: draw HUD overlays.
            if (anyDrawn && globalZMin < globalZMax)
            {
                _colorBarItem.SetRange(globalZMin, globalZMax);
                _colorBarItem.Draw(gl);
            }

            if (anyDrawn)
                _coordTagItem.Draw(gl);

            // Phase 5: draw overlay geometry (segments, points, etc.).
            var overlays = _renderItems.ToArray();
            foreach (var item in overlays)
                item.Draw(gl);

            if (!_camera.Enable2DView)
                _coordinationItem.DrawScreenPositionedAxes(gl);
        }
    }
}
