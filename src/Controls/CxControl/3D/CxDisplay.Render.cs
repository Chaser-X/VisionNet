using System;
using System.Collections.Generic;
using SharpGL;
using SharpGL.SceneGraph;

namespace VisionNet.Controls
{
    /// <summary>Render pipeline: GL lifecycle callbacks, frame processing, and draw dispatch.</summary>
    public partial class CxDisplay
    {
        /// <summary>
        /// Point size the context starts with. Every overlay point draw sets its own size, so this
        /// only governs anything drawn before the first item runs.
        /// </summary>
        private const float InitialPointSize = 2.0f;

        /// <inheritdoc/>
        protected override void DoOpenGLInitialized()
        {
            base.DoOpenGLInitialized();
            lock (_renderLock)
            {
                _fboWidth = _fboHeight = -1;
                // A context can be recreated on the same OpenGL wrapper. Forget all names
                // belonging to the old context and rebuild active items on the next frame.
                _registry.ForEachPoolHandle(handle => handle.ResetForContextRecreate());
                _registry.DropReleases();
                ModernGLRenderer.ResetForContextRecreate(OpenGL);
                OpenGL.ClearColor(_backColor.R / 255f, _backColor.G / 255f, _backColor.B / 255f, _backColor.A / 255f);
                OpenGL.PointSize(InitialPointSize);
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
                var overlayRenderer = ModernGLRenderer.For(gl);
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

            // CoordinatesChanged is raised here, outside the render lock, so a handler may call back
            // into the display (the contract RenderItemFailed already documents). The tooltip was
            // already updated inside the frame, so it shows the same coordinates.
            RaisePendingCoordinatesChanged();
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
            _registry.DrainReleases(handle => CxGLResourceManager.Release(gl, handle));
        }

        /// <summary>
        /// Iterates the resource pool under full lock.
        /// Creates GL resources for new items and recreates them for items whose CPU data changed.
        /// </summary>
        private void ProcessResourcePool(OpenGL gl)
        {
            _failedItems.Clear();

            _registry.ForEachPoolEntry((item, handle) =>
            {
                if (item.IsDisposed) return;

                // A shader compile failure is deterministic: do not re-upload geometry
                // every frame. Retry only after the item reports new data.
                if (handle.ShaderFailed && !handle.NeedsUpdate) return;

                if (!handle.IsValid || handle.NeedsUpdate)
                {
                    if (handle.IsValid)
                        CxGLResourceManager.Release(gl, handle);

                    // A failed shader compile is reported once the lock has been released.
                    if (!CxGLResourceManager.Create(gl, item, handle))
                        _failedItems.Add(item);
                }
            });

            // Report failures outside the lock and outside the pool enumeration: a handler may
            // legitimately call back into the display (e.g. to drop the failing item), which would
            // otherwise mutate the pool while it is being iterated.
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
        /// Per-frame aggregate of the registered surface items: the union of their data ranges and
        /// whether they all share one colour mode. Computed once in Phase 1 and consumed by the
        /// range propagation (Phase 2/2b) and the colour-bar gate (Phase 5).
        /// </summary>
        private struct FrameRanges
        {
            /// <summary>Union of every item's auto-computed base Z range.</summary>
            public float ZMin;
            public float ZMax;

            /// <summary>Union of every Diff-mode item's auto-computed base diff range.</summary>
            public float DiffMin;
            public float DiffMax;

            /// <summary>The only colour mode seen, when every item agrees; otherwise the first one.</summary>
            public SurfaceColorMode? SingleMode;

            /// <summary>True when every item shares <see cref="SingleMode"/>.</summary>
            public bool UniformMode;
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

            // Fill the frame snapshots (items + index-aligned handles) atomically; they are
            // consumed after the registry lock has been released, so GL calls are issued
            // without holding it.
            _registry.CopyFrameSnapshot(_surfaceSnapshot, _handleSnapshot);
            var snapshot = _surfaceSnapshot;

            var ranges = AggregateFrameRanges(snapshot);
            PropagateFrameRanges(snapshot, ranges);

            bool anyDrawn = DrawSurfaceItems(gl, snapshot);

            DrawOverlays(gl);

            if (!_camera.Enable2DView)
                _coordinationItem.DrawScreenPositionedAxes(gl);

            ResolvePendingPick();

            DrawHud(gl, anyDrawn, ranges);
        }

        /// <summary>
        /// Phase 1 — aggregates the global Z and diff ranges, and tracks whether all items share
        /// a single <see cref="SurfaceColorMode"/> (the colour bar is hidden when modes differ).
        /// Ranges aggregate from each item's auto-computed BASE range (BaseZ/BaseDiff), NOT
        /// its active ColorMin/ColorMax — the latter may hold a manual override that would
        /// otherwise pollute the aggregate and prevent Clear*Range() from restoring the auto range.
        /// </summary>
        private static FrameRanges AggregateFrameRanges(List<ICxObjRenderItem> snapshot)
        {
            var ranges = new FrameRanges
            {
                ZMin = float.MaxValue,
                ZMax = float.MinValue,
                DiffMin = float.MaxValue,
                DiffMax = float.MinValue,
            };

            SurfaceColorMode? singleMode = null;   // the only mode seen, if all items agree
            bool mixedMode = false;                // true once a second distinct mode appears

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
                    if (cur.BaseDiffMin < ranges.DiffMin) ranges.DiffMin = cur.BaseDiffMin;
                    if (cur.BaseDiffMax > ranges.DiffMax) ranges.DiffMax = cur.BaseDiffMax;
                }
                else
                {
                    if (cur.BaseZMin < ranges.ZMin) ranges.ZMin = cur.BaseZMin;
                    if (cur.BaseZMax > ranges.ZMax) ranges.ZMax = cur.BaseZMax;
                }
            }

            ranges.SingleMode = singleMode;
            ranges.UniformMode = !mixedMode && singleMode.HasValue;
            return ranges;
        }

        /// <summary>
        /// Phase 2/2b — propagates the unified ranges so all items share the same colour mapping.
        /// A manually set range (<see cref="_manualColorRange"/> / <see cref="_manualDiffRange"/>)
        /// overrides the auto-aggregated one.
        /// </summary>
        private void PropagateFrameRanges(List<ICxObjRenderItem> snapshot, FrameRanges ranges)
        {
            if (_manualColorRange.HasValue)
                SetGlobalZRangeOnAll(snapshot, _manualColorRange.Value.min, _manualColorRange.Value.max);
            else if (ranges.ZMin < ranges.ZMax)
                SetGlobalZRangeOnAll(snapshot, ranges.ZMin, ranges.ZMax);

            if (_manualDiffRange.HasValue)
                SetGlobalDiffRangeOnAll(snapshot, _manualDiffRange.Value.min, _manualDiffRange.Value.max);
            else if (ranges.DiffMin < ranges.DiffMax)
                SetGlobalDiffRangeOnAll(snapshot, ranges.DiffMin, ranges.DiffMax);
        }

        private static void SetGlobalZRangeOnAll(List<ICxObjRenderItem> snapshot, float min, float max)
        {
            foreach (var cur in snapshot)
                if (cur != null && !cur.IsDisposed)
                    cur.SetGlobalZRange(min, max);
        }

        private static void SetGlobalDiffRangeOnAll(List<ICxObjRenderItem> snapshot, float min, float max)
        {
            foreach (var cur in snapshot)
                if (cur != null && !cur.IsDisposed)
                    cur.SetGlobalDiffRange(min, max);
        }

        /// <summary>
        /// Phase 3 — draws the surface items, opaque first so translucent items blend over a
        /// settled frame. When nothing is translucent the order (and result) is unchanged.
        /// Returns whether at least one item was drawn.
        /// </summary>
        private bool DrawSurfaceItems(OpenGL gl, List<ICxObjRenderItem> snapshot)
        {
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
            return anyDrawn;
        }

        /// <summary>Phase 4 — overlay geometry (segments, points, polygons, etc.).</summary>
        private void DrawOverlays(OpenGL gl)
        {
            _registry.CopyOverlayItemsInto(_overlaySnapshot);
            foreach (var item in _overlaySnapshot)
                item.Draw(gl);
        }

        /// <summary>
        /// Phase 4b — resolves a pending hover pick. It is deferred from the mouse handler so
        /// the depth readback happens at most once per frame, with the scene already drawn
        /// and the GL context guaranteed current. Phase 5 draws the tooltip, so it shows
        /// the coordinates resolved here.
        /// </summary>
        private void ResolvePendingPick()
        {
            if (!_hasPendingPick) return;
            _hasPendingPick = false;
            ApplyPendingPick();
        }

        /// <summary>
        /// Phase 5 — screen-space HUD. The colour bar and coordinate tooltip are emitted after
        /// all scene and interactive geometry, so later depth-tested draws cannot cover their
        /// text or backgrounds.
        /// </summary>
        private void DrawHud(OpenGL gl, bool anyDrawn, FrameRanges ranges)
        {
            if (!anyDrawn) return;

            // The colour bar is drawn only when every item shares one data-mapped mode.
            float barMin, barMax;
            if (ranges.UniformMode && IsColorBarVisible
                && ranges.SingleMode != SurfaceColorMode.Intensity
                && ranges.SingleMode != SurfaceColorMode.Lit
                && TryResolveColorBarRange(ranges, out barMin, out barMax))
            {
                _colorBarItem.SetRange(barMin, barMax);
                _colorBarItem.Draw(gl);
            }

            _coordTagItem.Draw(gl);
        }

        /// <summary>
        /// Picks the range the colour bar should display. Diff mode uses the diff range, every
        /// other mode the Z range; in both cases a manual override wins over the auto-aggregated
        /// range. Returns <c>false</c> when no usable range exists, in which case the bar is skipped.
        /// </summary>
        private bool TryResolveColorBarRange(FrameRanges ranges, out float min, out float max)
        {
            min = max = 0f;

            if (ranges.SingleMode == SurfaceColorMode.Diff)
            {
                if (_manualDiffRange.HasValue)
                {
                    min = _manualDiffRange.Value.min;
                    max = _manualDiffRange.Value.max;
                    return true;
                }
                if (ranges.DiffMin < ranges.DiffMax)
                {
                    min = ranges.DiffMin;
                    max = ranges.DiffMax;
                    return true;
                }
                return false;
            }

            if (_manualColorRange.HasValue)
            {
                min = _manualColorRange.Value.min;
                max = _manualColorRange.Value.max;
                return true;
            }
            if (ranges.ZMin < ranges.ZMax)
            {
                min = ranges.ZMin;
                max = ranges.ZMax;
                return true;
            }
            return false;
        }
    }
}
