using System;
using System.Windows.Forms;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Mouse interaction, coordinate picking, and context-menu event handlers.</summary>
    public partial class CxDisplay
    {
        // Depth-buffer picking (readback, unproject, nearest-surface search) lives here; this
        // partial only decides when to ask it.
        private readonly CxPickingService _picking = new CxPickingService();

        private bool _hasHoverPixel;
        private int _lastHoverX;
        private int _lastHoverY;

        // Hover picking is deferred to the frame: the mouse handler only records the latest
        // position and CxDisplay.Render performs at most one depth readback per frame.
        private bool _hasPendingPick;
        private int _pendingPickX;
        private int _pendingPickY;

        // Result of the pick resolved during the last frame. CoordinatesChanged is raised after the
        // frame lock has been released (see RaisePendingCoordinatesChanged), so a handler may call
        // back into the display.
        private bool _hasCoordinatesResult;
        private CxPoint3D? _coordinatesResultLocation;
        private float? _coordinatesResultIntensity;

        // ── Mouse events ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            _isMouseDown = true;
            _hasHoverPixel = false;
            _coordTagItem.Visible = false;
            // Drop any hover pick recorded before the press: it must not re-show the tooltip.
            CancelPendingPick();
            // Redraw is on demand (RenderTrigger = Manual), and the camera only invalidates from
            // its MouseMove handler, so hiding the tooltip here must request a repaint itself.
            SafeInvalidate();

            // A left click already unprojects this pixel for selection; reuse that world position
            // for the camera pivot so a click costs one depth readback instead of two.
            CxPoint3D? worldPos = null;

            if (e.Button == MouseButtons.Left)
            {
                worldPos = _picking.ScreenToWorld(OpenGL, e.X, e.Y);
                if (worldPos.HasValue)
                {
                    var hit = FindActiveItemHit(worldPos.Value);

                    // Deselect previous if different from hit.
                    var previousSelection = _selectedItem;
                    if (_selectedItem != null && _selectedItem != hit)
                        _selectedItem.OnDeselected();

                    _selectedItem = hit;
                    if (!ReferenceEquals(previousSelection, hit))
                        SelectionChanged?.Invoke(hit);

                    if (hit != null)
                    {
                        hit.OnMouseDown(worldPos.Value);
                        _isDraggingItem   = true;
                        _lastDragWorldPos = worldPos;
                        Invalidate();
                        return;   // suppress camera rotation while dragging an item
                    }

                    // No active item hit — clear selection.
                    ClearSelection();
                }
                else
                {
                    // No world coordinate under mouse — clear selection.
                    ClearSelection();
                }
            }

            // Normal camera interaction.
            _camera.RotationPoint = ResolveRotationPoint(e.X, e.Y, worldPos);
            base.OnMouseDown(e);
        }

        /// <summary>
        /// Camera rotation pivot for a click: the surface point nearest to the clicked pixel.
        /// <paramref name="worldAtPixel"/> lets a caller that has already unprojected this pixel
        /// skip the second depth readback.
        /// </summary>
        private CxPoint3D? ResolveRotationPoint(int x, int y, CxPoint3D? worldAtPixel)
        {
            var world = worldAtPixel ?? _picking.ScreenToWorld(OpenGL, x, y);
            return world.HasValue ? GetNearestSurfacePoint(world.Value).Location : null;
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_isDraggingItem && _selectedItem != null)
                _selectedItem.OnMouseUp();
            _isDraggingItem   = false;
            _lastDragWorldPos = null;
            _isMouseDown      = false;
            _hasHoverPixel    = false;
            CancelPendingPick();
            base.OnMouseUp(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDraggingItem && _isMouseDown && _selectedItem != null)
            {
                _coordTagItem.Visible = false;
                CancelPendingPick();
                SafeInvalidate();
                var worldPos = _picking.ScreenToWorld(OpenGL, e.X, e.Y);
                if (worldPos.HasValue && _lastDragWorldPos.HasValue)
                {
                    var cur  = worldPos.Value;
                    var prev = _lastDragWorldPos.Value;

                    // For posed items, convert the two world-space points into the item's local
                    // frame so Translate() moves the stored (local) geometry by the correct delta.
                    if (_selectedItem.SupportsGroupTransform)
                    {
                        var inv = _selectedItem.ModelMatrixInverse;
                        cur  = inv.TransformPoint3D(cur);
                        prev = inv.TransformPoint3D(prev);
                    }

                    _selectedItem.OnMouseMove(cur, prev);
                    _lastDragWorldPos = worldPos;
                    Invalidate();
                }
                return;
            }

            // Camera rotation/panning already has its own mouse handler. Do not perform a
            // depth readback for the tooltip while the button is held; the result is discarded
            // by the _isMouseDown check below in the old path.
            if (_isMouseDown)
            {
                _coordTagItem.Visible = false;
                SafeInvalidate();
                CoordinatesChanged?.Invoke(null, null);
                base.OnMouseMove(e);
                return;
            }

            if (_hasHoverPixel && _lastHoverX == e.X && _lastHoverY == e.Y)
            {
                base.OnMouseMove(e);
                return;
            }
            _hasHoverPixel = true;
            _lastHoverX = e.X;
            _lastHoverY = e.Y;

            // Record the request only. The depth readback plus the nearest-surface search run
            // once per frame in CxDisplay.Render (Phase 4b): doing it here would stall the GPU
            // pipeline on every mouse move and would also require the GL context to be current
            // in this handler.
            _hasPendingPick = true;
            _pendingPickX = e.X;
            _pendingPickY = e.Y;
            SafeInvalidate();

            base.OnMouseMove(e);
        }

        /// <summary>
        /// Resolves the hover pick requested by <see cref="OnMouseMove"/>. Called inside the
        /// render pass, after the scene has been drawn (so the depth buffer is valid and the GL
        /// context is current), before the tooltip is drawn.
        /// </summary>
        private void ApplyPendingPick()
        {
            var pos = GetNearestSurfacePoint(_pendingPickX, _pendingPickY);
            bool hasHit = pos.Location.HasValue;
            _coordTagItem.Visible = hasHit;
            if (hasHit)
                _coordTagItem.SetCoordinates(pos.Location.Value, pos.Intensity);

            // Stash the result: the tooltip needs it now (Phase 5 draws it), but the event itself is
            // raised after the frame lock has been released.
            _hasCoordinatesResult = true;
            _coordinatesResultLocation = hasHit ? pos.Location : null;
            _coordinatesResultIntensity = hasHit ? pos.Intensity : null;
        }

        /// <summary>
        /// Raises <see cref="CoordinatesChanged"/> for the result resolved by the last frame.
        /// Called by <c>DoOpenGLDraw</c> after the render lock has been released, so a handler may
        /// safely call back into the display (the same contract <c>RenderItemFailed</c> documents).
        /// </summary>
        private void RaisePendingCoordinatesChanged()
        {
            if (!_hasCoordinatesResult) return;
            _hasCoordinatesResult = false;
            CoordinatesChanged?.Invoke(_coordinatesResultLocation, _coordinatesResultIntensity);
        }

        /// <summary>
        /// Drops a hover pick that has not been resolved yet, plus any result the last frame stashed.
        /// Used when the mouse button goes down or up, so a pick recorded before the press cannot
        /// re-show the tooltip on the next frame.
        /// </summary>
        private void CancelPendingPick()
        {
            _hasPendingPick = false;
            _hasCoordinatesResult = false;
        }

        /// <summary>
        /// Returns the first <see cref="AbstractRenderItem"/> in the overlay layer whose
        /// <see cref="AbstractRenderItem.IsActiveObj"/> is <c>true</c> and whose
        /// <see cref="AbstractRenderItem.HitTest"/> returns <c>true</c> for the given world position.
        /// Returns <c>null</c> when no item is hit.
        /// </summary>
        private AbstractRenderItem FindActiveItemHit(CxPoint3D worldPos)
        {
            IRenderItem[] snapshot = _registry.SnapshotOverlayItems();
            foreach (var item in snapshot)
            {
                if (!(item is AbstractRenderItem ar) || !ar.IsActiveObj) continue;

                // Posed items store local geometry: transform the query point into local space.
                var testPos = worldPos;
                if (ar.SupportsGroupTransform)
                    testPos = ar.ModelMatrixInverse.TransformPoint3D(worldPos);

                if (ar.HitTest(testPos)) return ar;
            }
            return null;
        }

        // ── Coordinate picking ───────────────────────────────────────────────────

        /// <summary>
        /// Finds the surface point nearest to the given screen pixel across all active surface items.
        /// </summary>
        /// <returns>Nearest world-space location and its intensity, or <c>(null, null)</c> if no hit.</returns>
        public (CxPoint3D? Location, float? Intensity) GetNearestSurfacePoint(int mouseX, int mouseY)
            => _picking.FindNearest(_registry, OpenGL, mouseX, mouseY, SurfaceColorMode);

        /// <summary>
        /// Finds the surface point nearest to an already-unprojected world position, so a caller
        /// that has already paid for the depth readback at this pixel does not repeat it.
        /// </summary>
        /// <remarks>
        /// The search needs a current GL context for the readback done by the public overload, so
        /// this path is <b>UI-thread only</b>.
        /// </remarks>
        private (CxPoint3D? Location, float? Intensity) GetNearestSurfacePoint(CxPoint3D world)
            => _picking.FindNearest(_registry, world, SurfaceColorMode);

        // ── Context-menu event handlers ──────────────────────────────────────────

        private void d2DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = !mi.Checked;
            _camera.Enable2DView = mi.Checked;
            CxBox3D? combined;
            combined = GetCombinedBoundingBox();
            _camera?.FitView(combined);
        }

        private void toolStripMenuItem_ViewModeClick(object sender, EventArgs e)
        {
            UncheckAllMenuItems(viewModeToolStripMenuItem);
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            _camera.ViewMode = (ViewMode)Enum.Parse(typeof(ViewMode), mi.Text);
            CxBox3D? combined;
            combined = GetCombinedBoundingBox();
            _camera?.FitView(combined);
        }

        private void toolStripMenuItem_SurfaceModeClick(object sender, EventArgs e)
        {
            UncheckAllMenuItems(surfaceModeToolStripMenuItem);
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceMode = (SurfaceMode)Enum.Parse(typeof(SurfaceMode), mi.Text);
        }

        private void toolStripMenuItem_SurfaceColorModeClick(object sender, EventArgs e)
        {
            UncheckAllMenuItems(surfaceColorModeToolStripMenuItem);
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceColorMode = (SurfaceColorMode)Enum.Parse(typeof(SurfaceColorMode), mi.Text);
        }
    }
}
