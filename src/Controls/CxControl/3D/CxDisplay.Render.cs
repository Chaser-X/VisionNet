using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;

namespace VisionNet.Controls
{
    /// <summary>Render pipeline: GL lifecycle callbacks, frame processing, and draw dispatch.</summary>
    public partial class CxDisplay
    {
        /// <summary>
        /// Point size the context starts with. Every overlay point draw sets its own size, so this
        /// only governs anything drawn before the first item runs.
        /// </summary>
        private static readonly float InitialPointSize = CxDisplayDefaults.PointSize;

        /// <summary>
        /// Frame callback. The base control has already made the GL context current. The scene is
        /// drawn into the off-screen multisampled <see cref="CxMsaaRenderTarget"/> and then resolved
        /// to the window's back buffer, which is what gives MSAA on mesh/line edges while keeping a
        /// single-sample depth buffer available for picking. When the target cannot be built the
        /// frame falls back to drawing straight into the window. The GL state is (re)initialised by
        /// <c>InitializeGlState</c> when the handle — and therefore the context — is created or
        /// recreated.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (DesignMode || _camera == null) { base.OnPaint(e); return; }

            // Raise the Paint event first: GLControl.OnPaint validates the control state and may
            // recreate the handle (and therefore the GL context), which must happen before the frame
            // uses the context rather than after the frame has drawn into the old one.
            base.OnPaint(e);

            bool useTarget = false;
            lock (_renderLock)
            {
                var gl = _gl;
                if (gl != null && gl.IsValid)
                {
                    gl.EnsureCurrent();

                    // MSAA on: render into the off-screen multisampled target and resolve to the
                    // window. MSAA off: draw straight to the window (no FBO, no resolve blit), and
                    // the depth read falls back to the window buffer. Also falls back to direct
                    // rendering when the target cannot be built.
                    useTarget = _msaaEnabled && _renderTarget.EnsureCreated(Width, Height);
                    if (useTarget)
                    {
                        _renderTarget.BindForRender();
                    }
                    else
                    {
                        if (_renderTarget.IsCreated) _renderTarget.Release();
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                        GL.Viewport(0, 0, Width, Height);
                    }

                    GL.ClearColor(_backColor.R / 255f, _backColor.G / 255f, _backColor.B / 255f, _backColor.A / 255f);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    // 1. Release GL resources dequeued by Dispose/Replace on another thread.
                    ProcessPendingRelease();

                    // 2. Create or update GL resources (full lock prevents concurrent Dispose).
                    ProcessResourcePool(gl);

                    GL.Enable(EnableCap.DepthTest);
                    GL.DepthFunc(DepthFunction.Less);
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    float[] projection;
                    float[] view;
                    lock (_cameraLock)
                        _camera.GetMatrices(Width, Height, out projection, out view);
                    var overlayRenderer = ModernGLRenderer.For(gl);
                    overlayRenderer.SetSceneMatrices(projection, view);
                    overlayRenderer.BeginFrame();
                    try
                    {
                        // Selects the Mesh lighting model (and binds the IBL textures in Pbr mode)
                        // for every built-in surface item of this frame.
                        overlayRenderer.BeginSurfaceLighting(_surfaceLightingMode, _environmentPreset,
                            _iblIntensity, _pointImpostor, _environmentFollowsCamera);
                        Render(gl);
                    }
                    finally
                    {
                        overlayRenderer.EndFrame();
                    }

                    GL.Disable(EnableCap.DepthTest);
                    GL.Disable(EnableCap.Blend);

                    if (useTarget)
                        _renderTarget.ResolveToDefault();

                    gl.Present();
                }
            }

            // CoordinatesChanged is raised here, outside the render lock, so a handler may call back
            // into the display (the contract RenderItemFailed already documents). The tooltip was
            // already updated inside the frame, so it shows the same coordinates.
            RaisePendingCoordinatesChanged();

            // A pending asynchronous pick needs one more frame to consume its result; route the
            // request through the hover throttle so the async pick cannot drive an unbounded render
            // loop (a frame per pick would run back-to-back while the mouse moves and saturate the
            // GPU). Requested after the lock so the repaint is not raised from inside the paint.
            if (_requestRepaintForPick)
            {
                _requestRepaintForPick = false;
                RequestHoverPickRepaint();
            }
        }

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
        private void ProcessPendingRelease()
        {
            _registry.DrainReleases(handle => CxGLResourceManager.Release(handle));
        }

        /// <summary>
        /// Iterates the resource pool under full lock.
        /// Creates GL resources for new items and recreates them for items whose CPU data changed.
        /// </summary>
        private void ProcessResourcePool(CxGlContext gl)
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
                        CxGLResourceManager.Release(handle);

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
        private void Render(CxGlContext gl)
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
        private bool DrawSurfaceItems(CxGlContext gl, List<ICxObjRenderItem> snapshot)
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
        private void DrawOverlays(CxGlContext gl)
        {
            _registry.CopyOverlayItemsInto(_overlaySnapshot);
            foreach (var item in _overlaySnapshot)
                item.Draw(gl);
        }

        /// <summary>
        /// Phase 4b — advances the hover pick. The scene has just been drawn into the multisampled
        /// target, so a 1-pixel depth read can be started (or a previously started one consumed)
        /// with the GL context current. The read is asynchronous: this frame starts it and a later
        /// frame consumes the result, so the UI thread never waits on the GPU. Phase 5 then draws
        /// the tooltip from the coordinates resolved here.
        /// </summary>
        private void ResolvePendingPick()
        {
            // Consume a result the GPU has already produced.
            if (_hasPendingRead)
            {
                if (_renderTarget.TryEndDepthRead(out float depth))
                {
                    _hasPendingRead = false;
                    ApplyPickResult(_picking.FindNearestFromDepth(
                        _registry, _gl, _readX, _readY, depth, SurfaceColorMode));
                }
                else
                {
                    // Not ready yet: ask for one more frame to poll it.
                    _requestRepaintForPick = true;
                }
            }

            // Start a read for a freshly requested pick.
            if (_hasPendingPick && !_hasPendingRead)
            {
                _hasPendingPick = false;
                _readX = _pendingPickX;
                _readY = _pendingPickY;

                if (_renderTarget.IsCreated && _renderTarget.BeginDepthRead(_readX, _readY))
                {
                    _hasPendingRead = true;
                    _requestRepaintForPick = true;
                }
                else
                {
                    // Fallback (no render target) or the pixel is outside the drawable: resolve it
                    // synchronously so a pick is never silently dropped.
                    ApplyPickResult(_picking.FindNearest(
                        _registry, _gl, _readX, _readY, SurfaceColorMode));
                }
            }
        }

        /// <summary>
        /// Phase 5 — screen-space HUD. The colour bar and coordinate tooltip are emitted after
        /// all scene and interactive geometry, so later depth-tested draws cannot cover their
        /// text or backgrounds.
        /// </summary>
        private void DrawHud(CxGlContext gl, bool anyDrawn, FrameRanges ranges)
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
