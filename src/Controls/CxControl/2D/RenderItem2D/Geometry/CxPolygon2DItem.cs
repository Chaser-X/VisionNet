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
    /// </summary>
    public class CxPolygon2DItem : Abstract2DRenderItem
    {
        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private Plot _plot;

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
            var spColor = ToSPColor(DrawColor);

            foreach (var poly in Polygons)
            {
                if (poly.Points == null || poly.Points.Length == 0) continue;

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
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            float t2 = HitThreshold * HitThreshold;
            foreach (var poly in Polygons)
            {
                if (poly.Points == null || poly.Points.Length == 0) continue;

                // Interior hit for filled closed polygons
                if (Filled && poly.IsClosed)
                {
                    if (PointInPolygon(plotPos, poly.Points)) return true;
                }

                // Edge hit for all polygon types
                for (int i = 0; i < poly.Points.Length - 1; i++)
                {
                    if (DistSqToSegment(plotPos, poly.Points[i], poly.Points[i + 1]) <= t2)
                        return true;
                }
                if (poly.IsClosed)
                {
                    int last = poly.Points.Length - 1;
                    if (DistSqToSegment(plotPos, poly.Points[last], poly.Points[0]) <= t2)
                        return true;
                }
            }
            return false;
        }

        private static bool PointInPolygon(CxPoint2D p, CxPoint2D[] pts)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].Y > p.Y) != (pts[j].Y > p.Y) &&
                    p.X < (pts[j].X - pts[i].X) * (p.Y - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)
                    inside = !inside;
            }
            return inside;
        }

        private static float DistSqToSegment(CxPoint2D p, CxPoint2D a, CxPoint2D b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq == 0f) { float ex = p.X - a.X; float ey = p.Y - a.Y; return ex * ex + ey * ey; }
            float t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq));
            float cx = a.X + t * dx - p.X;
            float cy = a.Y + t * dy - p.Y;
            return cx * cx + cy * cy;
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            for (int i = 0; i < Polygons.Length; i++)
            {
                if (Polygons[i].Points == null) continue;
                var pts = Polygons[i].Points;
                for (int j = 0; j < pts.Length; j++)
                    pts[j] = new CxPoint2D(pts[j].X + dx, pts[j].Y + dy);
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
