using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Mouse interaction, coordinate picking, and context-menu event handlers.</summary>
    public partial class CxDisplay
    {
        // ── Mouse events ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            _isMouseDown = true;

            if (e.Button == MouseButtons.Left)
            {
                var worldPos = ScreenToWorldCoordinate(e.X, e.Y);
                if (worldPos.HasValue)
                {
                    var hit = FindActiveItemHit(worldPos.Value);

                    // Deselect previous if different from hit.
                    if (_selectedItem != null && _selectedItem != hit)
                        _selectedItem.OnDeselected();

                    _selectedItem = hit;

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
            }

            // Normal camera interaction.
            _camera.RotationPoint = GetNearestSurfacePoint(e.X, e.Y).Location;
            base.OnMouseDown(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_isDraggingItem && _selectedItem != null)
                _selectedItem.OnMouseUp();
            _isDraggingItem   = false;
            _lastDragWorldPos = null;
            _isMouseDown      = false;
            base.OnMouseUp(e);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDraggingItem && _isMouseDown && _selectedItem != null)
            {
                var worldPos = ScreenToWorldCoordinate(e.X, e.Y);
                if (worldPos.HasValue && _lastDragWorldPos.HasValue)
                {
                    _selectedItem.OnMouseMove(worldPos.Value, _lastDragWorldPos.Value);
                    _lastDragWorldPos = worldPos;
                    Invalidate();
                }
                return;
            }

            var pos = GetNearestSurfacePoint(e.X, e.Y);
            if (pos.Location.HasValue && !_isMouseDown)
            {
                _coordTagItem.Visible = true;
                _coordTagItem.SetCoordinates(pos.Location.Value, pos.Intensity);
            }
            else
            {
                _coordTagItem.Visible = false;
            }
            base.OnMouseMove(e);
        }

        /// <summary>
        /// Returns the first <see cref="AbstractRenderItem"/> in the overlay layer whose
        /// <see cref="AbstractRenderItem.IsActiveObj"/> is <c>true</c> and whose
        /// <see cref="AbstractRenderItem.HitTest"/> returns <c>true</c> for the given world position.
        /// Returns <c>null</c> when no item is hit.
        /// </summary>
        private AbstractRenderItem FindActiveItemHit(CxPoint3D worldPos)
        {
            foreach (var item in _renderItems.ToArray())
                if (item is AbstractRenderItem ar && ar.IsActiveObj && ar.HitTest(worldPos))
                    return ar;
            return null;
        }

        // ── Coordinate picking ───────────────────────────────────────────────────

        /// <summary>
        /// Reads the depth buffer at the given screen pixel and un-projects it to a world-space point.
        /// Returns <c>null</c> when the pixel hits the far plane (no geometry present).
        /// </summary>
        private CxPoint3D? ScreenToWorldCoordinate(int mouseX, int mouseY)
        {
            var gl = OpenGL;
            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            int adjustedY = viewport[3] - mouseY;   // OpenGL Y is bottom-up.

            byte[] depthBytes = new byte[4];
            gl.ReadPixels(mouseX, adjustedY, 1, 1,
                OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, depthBytes);
            float depth = BitConverter.ToSingle(depthBytes, 0);

            if (Math.Abs(depth - 1.0f) < 0.00001f) return null;

            var obj = gl.UnProject(mouseX, adjustedY, depth);
            return new CxPoint3D((float)obj[0], (float)obj[1], (float)obj[2]);
        }

        /// <summary>
        /// Finds the surface point nearest to the given screen pixel across all active surface items.
        /// Structured surfaces use a 5×5 neighbourhood grid search; mesh-type items
        /// and unordered point clouds contribute the un-projected world coordinate directly.
        /// </summary>
        /// <returns>Nearest world-space location and its intensity, or <c>(null, null)</c> if no hit.</returns>
        public (CxPoint3D? Location, byte? Intensity) GetNearestSurfacePoint(int mouseX, int mouseY)
        {
            var pos = ScreenToWorldCoordinate(mouseX, mouseY);
            if (!pos.HasValue) return (null, null);
            var world = pos.Value;

            List<ICxObjRenderItem> snapshot;
            lock (_resourceLock)
                snapshot = new List<ICxObjRenderItem>(_surfaceItems);

            if (snapshot.Count == 0) return (null, null);

            CxPoint3D? bestPoint    = null;
            byte?      bestIntensity = null;
            float      bestDist     = float.MaxValue;

            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                // Mesh items: no per-point grid; use the world coordinate directly.
                if (cur is CxMeshItem || cur is CxMeshAdvancedItem)
                {
                    if (0f < bestDist) { bestDist = 0f; bestPoint = world; bestIntensity = null; }
                    continue;
                }

                // Point cloud items: no per-point grid; use world coordinate directly.
                if (cur is CxPointCloudItem || cur is CxPointCloudAdvancedItem)
                {
                    if (0f < bestDist) { bestDist = 0f; bestPoint = world; bestIntensity = null; }
                    continue;
                }

                // Resolve the underlying CxSurface for structured surface search.
                CxSurface surface = null;
                if      (cur is CxSurfaceItem        si) surface = si.Surface;
                else if (cur is CxSurfaceAdvancedItem ai) surface = ai.Surface;
                if (surface == null) continue;

                // Structured surface: 5×5 neighbourhood search.
                int xi = (int)((world.X - surface.XOffset) / surface.XScale);
                int yi = (int)((world.Y - surface.YOffset) / surface.YScale);
                if (xi < 0 || xi >= surface.Width || yi < 0 || yi >= surface.Length) continue;

                float threshold = 5 * (surface.XScale * surface.XScale
                                     + surface.YScale * surface.YScale
                                     + surface.ZScale * surface.ZScale);

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = xi + dx, ny = yi + dy;
                        if (nx < 0 || nx >= surface.Width || ny < 0 || ny >= surface.Length) continue;

                        int idx = ny * surface.Width + nx;
                        float z = surface.Data[idx] == -32768
                            ? float.NegativeInfinity
                            : surface.ZOffset + surface.Data[idx] * surface.ZScale;
                        if (float.IsInfinity(z)) continue;

                        float px = surface.XOffset + nx * surface.XScale;
                        float py = surface.YOffset + ny * surface.YScale;
                        float d  = (px - world.X) * (px - world.X)
                                 + (py - world.Y) * (py - world.Y)
                                 + (z  - world.Z) * (z  - world.Z);

                        if (d < bestDist && d < threshold)
                        {
                            bestDist      = d;
                            bestPoint     = new CxPoint3D(px, py, z);
                            bestIntensity = (surface.Intensity != null && surface.Intensity.Length > idx)
                                ? surface.Intensity[idx] : (byte?)null;
                        }
                    }
                }
            }

            return (bestPoint, bestIntensity);
        }

        // ── Context-menu event handlers ──────────────────────────────────────────

        private void d2DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = !mi.Checked;
            _camera.Enable2DView = mi.Checked;
            Box3D? combined;
            lock (_resourceLock) combined = GetCombinedBoundingBox();
            _camera?.FitView(combined);
        }

        private void toolStripMenuItem_ViewModeClick(object sender, EventArgs e)
        {
            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            _camera.ViewMode = (ViewMode)Enum.Parse(typeof(ViewMode), mi.Text);
            Box3D? combined;
            lock (_resourceLock) combined = GetCombinedBoundingBox();
            _camera?.FitView(combined);
        }

        private void toolStripMenuItem_SurfaceModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceMode = (SurfaceMode)Enum.Parse(typeof(SurfaceMode), mi.Text);
        }

        private void toolStripMenuItem_SurfaceColorModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceColorMode = (SurfaceColorMode)Enum.Parse(typeof(SurfaceColorMode), mi.Text);
        }
    }
}
