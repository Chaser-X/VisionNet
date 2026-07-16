using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxBox2D"/> values as axis-aligned rectangles with optional fill.
    /// Each box is a separate <see cref="Polygon"/> plottable.
    ///
    /// Drag interaction:
    /// <list type="bullet">
    ///   <item>Click near a vertex → drag to resize (opposite corner stays fixed).</item>
    ///   <item>Click on an edge or interior → drag to translate.</item>
    /// </list>
    /// Only the box that was hit by <see cref="HitTest"/> is drawn in <see cref="Abstract2DRenderItem.SelectedColor"/>;
    /// all others use <see cref="Abstract2DRenderItem.Color"/>.
    /// </summary>
    public class CxBox2DItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, ResizeVertex }

        private readonly List<Polygon> _plottables = new List<Polygon>();
        private Plot _plot;

        private DragMode  _dragMode;
        private int       _activeBoxIndex = -1;
        private int       _vertexIndex;
        private CxPoint2D _fixedCorner;

        /// <summary>Gets the box data being rendered.</summary>
        public CxBox2D[] Boxes { get; private set; }

        /// <summary>Gets or sets whether boxes are filled with their colour.</summary>
        public bool Filled { get; set; } = false;

        /// <summary>Initializes the item with the given boxes, colour, line width, and optional fill.</summary>
        public CxBox2DItem(CxBox2D[] boxes, Color color, float size = 1f, bool filled = false)
        {
            Boxes  = boxes ?? Array.Empty<CxBox2D>();
            Color  = color;
            Size   = size;
            Filled = filled;
            if (Boxes.Length > 0)
                HitThreshold = Math.Max(1f, (float)Math.Sqrt(Boxes[0].Size.Width * Boxes[0].Size.Width + Boxes[0].Size.Height * Boxes[0].Size.Height) * 0.02f);
        }

        /// <inheritdoc/>
        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            BuildPlottables();
        }

        /// <inheritdoc/>
        public override void RemoveFromPlot(Plot plot)
        {
            foreach (var p in _plottables) plot.PlottableList.Remove(p);
            _plottables.Clear();
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var p in _plottables) _plot.PlottableList.Remove(p);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            for (int bi = 0; bi < Boxes.Length; bi++)
            {
                var box = Boxes[bi];
                var color = bi == _activeBoxIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

                var coords = new Coordinates[4];
                coords[0] = new Coordinates(box.Left,  box.Top);
                coords[1] = new Coordinates(box.Right, box.Top);
                coords[2] = new Coordinates(box.Right, box.Bottom);
                coords[3] = new Coordinates(box.Left,  box.Bottom);
                var polygon = _plot.Add.Polygon(coords);
                polygon.LineStyle.Width       = Size;
                polygon.LineStyle.Color       = color;
                polygon.FillStyle.Color       = color;
                polygon.FillStyle.IsVisible   = Filled;
                polygon.MarkerStyle.IsVisible = false;
                _plottables.Add(polygon);
            }
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            for (int bi = 0; bi < Boxes.Length; bi++)
            {
                var box = Boxes[bi];

                // Interior hit (filled)
                if (Filled &&
                    plotPos.X >= box.Left  - HitThreshold &&
                    plotPos.X <= box.Right + HitThreshold &&
                    plotPos.Y >= box.Top   - HitThreshold &&
                    plotPos.Y <= box.Bottom + HitThreshold)
                {
                    _activeBoxIndex = bi;
                    return true;
                }

                // Edge hit (4 edges)
                var pts = new[]
                {
                    new CxPoint2D(box.Left,  box.Top),
                    new CxPoint2D(box.Right, box.Top),
                    new CxPoint2D(box.Right, box.Bottom),
                    new CxPoint2D(box.Left,  box.Bottom),
                };

                float t2 = HitThreshold * HitThreshold;
                for (int i = 0; i < 4; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % 4];
                    float dx = b.X - a.X;
                    float dy = b.Y - a.Y;
                    float lenSq = dx * dx + dy * dy;
                    float d;
                    if (lenSq == 0f)
                    {
                        float ex = plotPos.X - a.X;
                        float ey = plotPos.Y - a.Y;
                        d = ex * ex + ey * ey;
                    }
                    else
                    {
                        float t = Math.Max(0, Math.Min(1, ((plotPos.X - a.X) * dx + (plotPos.Y - a.Y) * dy) / lenSq));
                        float cx = a.X + t * dx - plotPos.X;
                        float cy = a.Y + t * dy - plotPos.Y;
                        d = cx * cx + cy * cy;
                    }
                    if (d <= t2)
                    {
                        _activeBoxIndex = bi;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeBoxIndex < 0) { UpdatePlottable(); return; }

            var box = Boxes[_activeBoxIndex];
            var corners = new[]
            {
                new CxPoint2D(box.Left,  box.Top),
                new CxPoint2D(box.Right, box.Top),
                new CxPoint2D(box.Right, box.Bottom),
                new CxPoint2D(box.Left,  box.Bottom),
            };

            float t2 = HitThreshold * HitThreshold * 4f;
            _dragMode = DragMode.Translate;
            for (int i = 0; i < 4; i++)
            {
                float dx = plotPos.X - corners[i].X;
                float dy = plotPos.Y - corners[i].Y;
                if (dx * dx + dy * dy <= t2)
                {
                    _vertexIndex = i;
                    _fixedCorner = corners[(i + 2) % 4];
                    _dragMode = DragMode.ResizeVertex;
                    break;
                }
            }

            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeBoxIndex < 0) return;

            if (_dragMode == DragMode.ResizeVertex)
            {
                float left   = Math.Min(_fixedCorner.X, plotPos.X);
                float right  = Math.Max(_fixedCorner.X, plotPos.X);
                float top    = Math.Min(_fixedCorner.Y, plotPos.Y);
                float bottom = Math.Max(_fixedCorner.Y, plotPos.Y);

                float w = right - left;
                float h = bottom - top;
                if (w < 1f) w = 1f;
                if (h < 1f) h = 1f;

                Boxes[_activeBoxIndex] = new CxBox2D(
                    new CxPoint2D((left + right) / 2f, (top + bottom) / 2f),
                    new CxSize2D(w, h));

                UpdatePlottable();
                RaiseOnChanged();
            }
            else
            {
                base.OnMouseMove(plotPos, prevPlotPos);
            }
        }

        /// <inheritdoc/>
        public override void OnMouseUp()
        {
            _dragMode = DragMode.None;
        }

        /// <inheritdoc/>
        public override void OnDeselected()
        {
            _activeBoxIndex = -1;
            _dragMode = DragMode.None;
            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            if (_activeBoxIndex >= 0)
            {
                var b = Boxes[_activeBoxIndex];
                Boxes[_activeBoxIndex] = new CxBox2D(
                    new CxPoint2D(b.Center.X + dx, b.Center.Y + dy), b.Size);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var p in _plottables) _plot.PlottableList.Remove(p);
        }
    }
}
