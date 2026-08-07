using System;
using System.Collections.Generic;
using SharpGL;
using SharpGL.SceneGraph;

namespace VisionNet.Controls
{
    /// <summary>Render pipeline: GL lifecycle callbacks, frame processing, and draw dispatch.</summary>
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
        /// Main render callback invoked each frame. Releases deferred GL resources,
        /// creates/updates new ones, then issues all draw calls.
        /// </summary>
        protected override void DoOpenGLDraw(RenderEventArgs e)
        {
            if (DesignMode) return;
            base.DoOpenGLDraw(e);

            var rcp = OpenGL.RenderContextProvider;
            if (rcp != null && Width > 0 && Height > 0)
            {
                OpenGL.SetDimensions(Width, Height);
                OpenGL.Viewport(0, 0, Width, Height);
            }

            var gl = OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            // 1. Release GL resources dequeued by Dispose/Replace on another thread.
            ProcessPendingRelease(gl);

            // 2. Create or update GL resources (full lock prevents concurrent Dispose).
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

        /// <inheritdoc/>
        protected override void DoGDIDraw(RenderEventArgs e) => base.DoGDIDraw(e);

        /// <inheritdoc/>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated && !IsDisposed && Width > 0 && Height > 0)
            {
                _camera?.LookAtMatrix(OpenGL);
                Invalidate();
            }
        }

        /// <summary>Releases all GL resource handles waiting in the deferred-release queue.</summary>
        private void ProcessPendingRelease(OpenGL gl)
        {
            while (_pendingRelease.TryDequeue(out var handle))
                ReleaseGLResources(gl, handle);
        }

        /// <summary>
        /// Iterates the resource pool under full lock.
        /// Creates GL resources for new items and recreates them for items whose CPU data changed.
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
        /// Issues all draw calls for one frame:
        /// world-space coordinate system → surface items (unified Z range) →
        /// color bar → coord tag → overlay geometry → screen-space axes indicator.
        /// </summary>
        private void Render(OpenGL gl)
        {
            if (!_camera.Enable2DView && ShowCoordinateSystem)
                _coordinationItem.Draw(gl);

            // Snapshot under lock so GL calls are issued without holding it.
            List<ICxObjRenderItem> snapshot;
            lock (_resourceLock)
                snapshot = new List<ICxObjRenderItem>(_surfaceItems);

            // Phase 1 — compute global Z range and global diff range separately.
            float globalZMin = float.MaxValue, globalZMax = float.MinValue;
            float globalDiffMin = float.MaxValue, globalDiffMax = float.MinValue;
            int zCount = 0, diffCount = 0;
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;
                if (cur.SurfaceColorMode == SurfaceColorMode.Diff)
                {
                    if (cur.ZMin < globalDiffMin) globalDiffMin = cur.ZMin;
                    if (cur.ZMax > globalDiffMax) globalDiffMax = cur.ZMax;
                    diffCount++;
                }
                else if (cur.SurfaceColorMode != SurfaceColorMode.Intensity)
                {
                    if (cur.ZMin < globalZMin) globalZMin = cur.ZMin;
                    if (cur.ZMax > globalZMax) globalZMax = cur.ZMax;
                    zCount++;
                }
            }

            // Phase 2 — propagate unified Z range so all items share the same colour mapping.
            if (globalZMin < globalZMax)
            {
                foreach (var cur in snapshot)
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalZRange(globalZMin, globalZMax);
            }

            // Phase 3 — draw surface items.
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

            // Phase 4 — HUD overlays.
            bool hybrid = zCount > 0 && diffCount > 0;
            bool pureDiff = diffCount > 0 && zCount == 0;
            if (anyDrawn && !hybrid)
            {
                if (pureDiff && globalDiffMin < globalDiffMax)
                {
                    _colorBarItem.SetRange(globalDiffMin, globalDiffMax);
                    _colorBarItem.Draw(gl);
                }
                else if (globalZMin < globalZMax)
                {
                    _colorBarItem.SetRange(globalZMin, globalZMax);
                    _colorBarItem.Draw(gl);
                }
            }
            if (anyDrawn)
                _coordTagItem.Draw(gl);

            // Phase 5 — overlay geometry (segments, points, polygons, etc.).
            foreach (var item in _renderItems.ToArray())
                item.Draw(gl);

            if (!_camera.Enable2DView)
                _coordinationItem.DrawScreenPositionedAxes(gl);
        }
    }
}
