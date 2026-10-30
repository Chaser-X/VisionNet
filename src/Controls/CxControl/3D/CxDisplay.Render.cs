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
            lock (_renderLock)
            {
                _fboWidth = _fboHeight = -1;
                // A context can be recreated on the same OpenGL wrapper. Forget all names
                // belonging to the old context and rebuild active items on the next frame.
                lock (_resourceLock)
                {
                    foreach (var handle in _resourcePool.Values)
                        handle.ResetForContextRecreate();
                }
                while (_pendingRelease.TryDequeue(out _)) { }
                ModernOverlayRenderer.ResetForContextRecreate(OpenGL);
                OpenGL.ClearColor(_backColor.R / 255f, _backColor.G / 255f, _backColor.B / 255f, _backColor.A / 255f);
                OpenGL.PointSize(2.0f);
            }
        }

        /// <summary>
        /// Main render callback invoked each frame. Releases deferred GL resources,
        /// creates/updates new ones, then issues all draw calls.
        /// </summary>
        protected override void DoOpenGLDraw(RenderEventArgs e)
        {
            if (DesignMode) return;
            lock (_renderLock)
            {
                base.DoOpenGLDraw(e);

                var rcp = OpenGL.RenderContextProvider;
                if (rcp != null && Width > 0 && Height > 0)
                {
                    if (_fboWidth != Width || _fboHeight != Height)
                    {
                        OpenGL.SetDimensions(Width, Height);
                        _fboWidth = Width;
                        _fboHeight = Height;
                    }
                    OpenGL.Viewport(0, 0, Width, Height);
                }

                var gl = OpenGL;
                gl.ClearColor(_backColor.R / 255f, _backColor.G / 255f, _backColor.B / 255f, _backColor.A / 255f);
                gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

                // 1. Release GL resources dequeued by Dispose/Replace on another thread.
                ProcessPendingRelease(gl);

                // 2. Create or update GL resources (full lock prevents concurrent Dispose).
                ProcessResourcePool(gl);

                gl.Enable(OpenGL.GL_DEPTH_TEST);
                gl.DepthFunc(OpenGL.GL_LESS);
                gl.Enable(OpenGL.GL_BLEND);
                gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
                float[] projection;
                float[] view;
                lock (_cameraLock)
                    _camera.GetMatrices(Width, Height, out projection, out view);
                var overlayRenderer = ModernOverlayRenderer.For(gl);
                overlayRenderer.SetSceneMatrices(projection, view);
                overlayRenderer.BeginFrame(gl);
                try
                {
                    Render(gl);
                }
                finally
                {
                    overlayRenderer.EndFrame(gl);
                }

                gl.Disable(OpenGL.GL_DEPTH_TEST);
                gl.Disable(OpenGL.GL_BLEND);
            }
        }

        /// <inheritdoc/>
        protected override void DoGDIDraw(RenderEventArgs e) => base.DoGDIDraw(e);

        /// <inheritdoc/>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated && !IsDisposed && Width > 0 && Height > 0)
            {
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
            _failedItems.Clear();

            lock (_resourceLock)
            {
                foreach (var kv in _resourcePool)
                {
                    var item   = kv.Key;
                    var handle = kv.Value;

                    if (item.IsDisposed) continue;

                    // A shader compile failure is deterministic: do not re-upload geometry
                    // every frame. Retry only after the item reports new data.
                    if (handle.ShaderFailed && !handle.NeedsUpdate) continue;

                    if (!handle.IsValid || handle.NeedsUpdate)
                    {
                        if (handle.IsValid)
                            ReleaseGLResources(gl, handle);

                        CreateGLResources(gl, item, handle);
                    }
                }
            }

            // Report failures outside the lock and outside the pool enumeration: a handler may
            // legitimately call back into the display (e.g. to drop the failing item), which would
            // otherwise mutate _resourcePool while it is being iterated.
            if (_failedItems.Count == 0) return;

            foreach (var failed in _failedItems)
                RenderItemFailed?.Invoke(failed);
            _failedItems.Clear();
        }

        /// <summary>
        /// True when a surface item is translucent, i.e. it must be drawn in the second
        /// (blended, non-depth-writing) pass of <see cref="Render"/>. Items that are not
        /// built-in surface items are treated as opaque.
        /// </summary>
        private static bool IsTranslucentItem(ICxObjRenderItem item)
            => item is AbstractCxObjRenderItem surfaceItem && surfaceItem.Opacity < 1f;

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
            {
                _surfaceSnapshot.Clear();
                _surfaceSnapshot.AddRange(_surfaceItems);

                // Resolve every handle once per frame (index-aligned) instead of locking per item.
                _handleSnapshot.Clear();
                foreach (var item in _surfaceSnapshot)
                    _handleSnapshot.Add(_resourcePool.TryGetValue(item, out var h) ? h : null);

                snapshot = _surfaceSnapshot;
            }

            // Phase 1 — aggregate global Z and diff ranges, and track whether all items share
            // a single SurfaceColorMode (the colour bar is hidden when modes differ).
            // Ranges aggregate from each item's auto-computed BASE range (BaseZ/BaseDiff), NOT
            // its active ColorMin/ColorMax — the latter may hold a manual override that would
            // otherwise pollute the aggregate and prevent Clear*Range() from restoring the auto range.
            float globalZMin = float.MaxValue, globalZMax = float.MinValue;
            float globalDiffMin = float.MaxValue, globalDiffMax = float.MinValue;
            SurfaceColorMode? singleMode = null;   // the only mode seen, if all items agree
            bool mixedMode = false;                 // true once a second distinct mode appears
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                var mode = cur.SurfaceColorMode;
                if (!mixedMode)
                {
                    if (singleMode == null) singleMode = mode;
                    else if (singleMode != mode) mixedMode = true;
                }

                if (mode == SurfaceColorMode.Diff)
                {
                    if (cur.BaseDiffMin < globalDiffMin) globalDiffMin = cur.BaseDiffMin;
                    if (cur.BaseDiffMax > globalDiffMax) globalDiffMax = cur.BaseDiffMax;
                }
                else
                {
                    if (cur.BaseZMin < globalZMin) globalZMin = cur.BaseZMin;
                    if (cur.BaseZMax > globalZMax) globalZMax = cur.BaseZMax;
                }
            }
            bool uniformMode = !mixedMode && singleMode.HasValue;

            // Phase 2 — propagate unified Z range so all items share the same colour mapping.
            // A manually set colour range (_manualColorRange) overrides the auto-aggregated one.
            if (_manualColorRange.HasValue)
            {
                float mMin = _manualColorRange.Value.min;
                float mMax = _manualColorRange.Value.max;
                foreach (var cur in snapshot)
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalZRange(mMin, mMax);
            }
            else if (globalZMin < globalZMax)
            {
                foreach (var cur in snapshot)
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalZRange(globalZMin, globalZMax);
            }

            // Phase 2b — propagate the unified diff range to Diff-mode items.
            // A manually set diff range (_manualDiffRange) overrides the auto-aggregated one.
            if (_manualDiffRange.HasValue)
            {
                float dMin = _manualDiffRange.Value.min;
                float dMax = _manualDiffRange.Value.max;
                foreach (var cur in snapshot)
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalDiffRange(dMin, dMax);
            }
            else if (globalDiffMin < globalDiffMax)
            {
                foreach (var cur in snapshot)
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalDiffRange(globalDiffMin, globalDiffMax);
            }

            // Phase 3 — draw surface items, opaque first so translucent items blend over a
            // settled frame. When nothing is translucent the order (and result) is unchanged.
            bool anyDrawn = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool translucentPass = pass == 1;
                for (int i = 0; i < snapshot.Count; i++)
                {
                    var cur = snapshot[i];
                    if (cur == null || cur.IsDisposed) continue;

                    var handle = _handleSnapshot[i];
                    if (handle?.IsValid != true) continue;

                    if (IsTranslucentItem(cur) != translucentPass) continue;

                    cur.Draw(gl, handle);
                    anyDrawn = true;
                }
            }

            // Phase 4 — overlay geometry (segments, points, polygons, etc.).
            lock (_resourceLock)
            {
                _overlaySnapshot.Clear();
                _overlaySnapshot.AddRange(_renderItems);
            }
            foreach (var item in _overlaySnapshot)
                item.Draw(gl);

            if (!_camera.Enable2DView)
                _coordinationItem.DrawScreenPositionedAxes(gl);

            // Phase 4b — resolve a pending hover pick. It is deferred from the mouse handler so
            // the depth readback happens at most once per frame, with the scene already drawn
            // and the GL context guaranteed current. Phase 5 draws the tooltip, so it shows
            // the coordinates resolved here.
            if (_hasPendingPick)
            {
                _hasPendingPick = false;
                ApplyPendingPick();
            }

            // Phase 5 — screen-space HUD. The colour bar and coordinate tooltip are
            // emitted after all scene and interactive geometry, so later depth-tested
            // draws cannot cover their text or backgrounds.
            // The colour bar is drawn only when every item shares one data-mapped mode.
            if (anyDrawn && uniformMode && singleMode != SurfaceColorMode.Intensity
                && singleMode != SurfaceColorMode.Lit && IsColorBarVisible)
            {
                if (singleMode == SurfaceColorMode.Diff && _manualDiffRange.HasValue)
                {
                    _colorBarItem.SetRange(_manualDiffRange.Value.min, _manualDiffRange.Value.max);
                    _colorBarItem.Draw(gl);
                }
                else if (singleMode == SurfaceColorMode.Diff && globalDiffMin < globalDiffMax)
                {
                    _colorBarItem.SetRange(globalDiffMin, globalDiffMax);
                    _colorBarItem.Draw(gl);
                }
                else if (singleMode != SurfaceColorMode.Diff && _manualColorRange.HasValue)
                {
                    _colorBarItem.SetRange(_manualColorRange.Value.min, _manualColorRange.Value.max);
                    _colorBarItem.Draw(gl);
                }
                else if (singleMode != SurfaceColorMode.Diff && globalZMin < globalZMax)
                {
                    _colorBarItem.SetRange(globalZMin, globalZMax);
                    _colorBarItem.Draw(gl);
                }
            }
            if (anyDrawn)
                _coordTagItem.Draw(gl);
        }
    }
}
