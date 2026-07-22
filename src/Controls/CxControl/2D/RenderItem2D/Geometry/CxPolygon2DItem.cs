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
    /// Renders an array of <see cref="CxPolygon2D"/> values.
    /// Closed polygons use <see cref="Polygon"/> plottables with optional fill;
    /// open polylines use <see cref="Scatter"/> plottables (no fill).
    ///
    /// Drag interaction:
    /// <list type="bullet">
    ///   <item>Click near a vertex → drag to reshape (that vertex follows the mouse).</item>
    ///   <item>Click near an edge or inside a filled polygon → drag to translate.</item>
    /// </list>
    /// Only the polygon that was hit by <see cref="HitTest"/> is drawn in <see cref="Abstract2DRenderItem.SelectedColor"/>;
    /// all others use <see cref="Abstract2DRenderItem.Color"/>.
    /// </summary>
    public class CxPolygon2DItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, DragVertex }

        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly List<IPlottable> _handlePlottables = new List<IPlottable>();

        private const float HandlePixelSize = 8f;

        private DragMode _dragMode;
        private int      _activePolygonIndex = -1;
        private int      _vertexIndex;

        /// <summary>Gets the polygon data being rendered.</summary>
        public CxPolygon2D[] Polygons { get; private set; }

        /// <summary>Gets or sets whether closed polygons are filled with their colour.</summary>
        public bool Filled { get; set; } = false;

        /// <summary>Initializes the item with the given polygons, colour, line width, and optional fill.</summary>
        public CxPolygon2DItem(CxPolygon2D[] polygons, Color color, float size = 1f, bool filled = false)
        {
            Polygons = polygons ?? Array.Empty<CxPolygon2D>();
            Color    = color;
            Size     = size;
            Filled   = filled;

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
            RemoveHandlePlottables(plot);
            _plottables.Clear();
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var p in _plottables) _plot.PlottableList.Remove(p);
            RemoveHandlePlottables(_plot);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            for (int pi = 0; pi < Polygons.Length; pi++)
            {
                var poly = Polygons[pi];
                if (poly.Points == null || poly.Points.Length == 0) continue;

                var spColor = pi == _activePolygonIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

                if (poly.IsClosed)
                {
                    var coords = new Coordinates[poly.Points.Length];
                    for (int i = 0; i < poly.Points.Length; i++)
                        coords[i] = new Coordinates(poly.Points[i].X, poly.Points[i].Y);
                    var polygon = _plot.Add.Polygon(coords);
                    polygon.LineStyle.Width       = Size;
                    polygon.LineStyle.Color       = spColor;
                    polygon.FillStyle.Color       = spColor;
                    polygon.FillStyle.IsVisible   = Filled;
                    polygon.MarkerStyle.IsVisible = false;
                    _plottables.Add(polygon);
                }
                else
                {
                    double[] xs = new double[poly.Points.Length];
                    double[] ys = new double[poly.Points.Length];
                    for (int i = 0; i < poly.Points.Length; i++)
                    { xs[i] = poly.Points[i].X; ys[i] = poly.Points[i].Y; }
                    var scatter = _plot.Add.Scatter(xs, ys);
                    scatter.MarkerStyle.IsVisible = false;
                    scatter.LineStyle.Width       = Size;
                    scatter.Color                 = spColor;
                    _plottables.Add(scatter);
                }
            }

            if (_activePolygonIndex >= 0)
            {
                var poly = Polygons[_activePolygonIndex];
                if (poly.Points == null) return;
                var hColor = ToSPColor(Color.Lime);

                for (int j = 0; j < poly.Points.Length; j++)
                {
                    var v = _plot.Add.Marker(poly.Points[j].X, poly.Points[j].Y);
                    v.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                    v.MarkerStyle.Size = HandlePixelSize;
                    v.MarkerStyle.FillColor = hColor;
                    v.MarkerStyle.LineColor = hColor;
                    v.MarkerStyle.LineWidth = 1;
                    _handlePlottables.Add(v);
                }
            }
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            for (int pi = 0; pi < Polygons.Length; pi++)
            {
                var poly = Polygons[pi];
                if (poly.Points == null || poly.Points.Length == 0) continue;

                // Interior hit for filled closed polygons
                if (Filled && poly.IsClosed)
                {
                    if (VisionOperator.IsPointInPolygon2D(plotPos, Polygons[pi]))
                    {
                        _activePolygonIndex = pi;
                        return true;
                    }
                }

                // Edge hit for all polygon types
                float hitW = HitThreshold * WorldPerPixel();
                float t2 = hitW * hitW;
                for (int i = 0; i < poly.Points.Length - 1; i++)
                {
                    VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(poly.Points[i], poly.Points[i + 1]), out float d);
                    if (d * d <= t2)
                    {
                        _activePolygonIndex = pi;
                        return true;
                    }
                }
                if (poly.IsClosed)
                {
                    int last = poly.Points.Length - 1;
                    VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(poly.Points[last], poly.Points[0]), out float d);
                    if (d * d <= t2)
                    {
                        _activePolygonIndex = pi;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activePolygonIndex < 0) { UpdatePlottable(); return; }

            var poly = Polygons[_activePolygonIndex];
            if (poly.Points == null || poly.Points.Length == 0)
            {
                _dragMode = DragMode.None;
                UpdatePlottable();
                return;
            }

            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW * 4f;
            _dragMode = DragMode.Translate;
            for (int i = 0; i < poly.Points.Length; i++)
            {
                float dx = plotPos.X - poly.Points[i].X;
                float dy = plotPos.Y - poly.Points[i].Y;
                if (dx * dx + dy * dy <= t2)
                {
                    _vertexIndex = i;
                    _dragMode = DragMode.DragVertex;
                    break;
                }
            }

            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activePolygonIndex < 0) return;

            if (_dragMode == DragMode.DragVertex)
            {
                var pts = Polygons[_activePolygonIndex].Points;
                if (pts != null && _vertexIndex >= 0 && _vertexIndex < pts.Length)
                {
                    pts[_vertexIndex] = new CxPoint2D((float)plotPos.X, (float)plotPos.Y);
                    UpdatePlottable();
                    RaiseOnChanged();
                }
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
            _activePolygonIndex = -1;
            _dragMode = DragMode.None;
            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            if (_activePolygonIndex >= 0 && Polygons[_activePolygonIndex].Points != null)
            {
                var pts = Polygons[_activePolygonIndex].Points;
                for (int j = 0; j < pts.Length; j++)
                    pts[j] = new CxPoint2D(pts[j].X + dx, pts[j].Y + dy);
            }
        }

        /// <inheritdoc/>
        private void RemoveHandlePlottables(Plot plot)
        {
            foreach (var h in _handlePlottables) plot.PlottableList.Remove(h);
            _handlePlottables.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
            {
                foreach (var p in _plottables) _plot.PlottableList.Remove(p);
                RemoveHandlePlottables(_plot);
            }
        }
    }
}
