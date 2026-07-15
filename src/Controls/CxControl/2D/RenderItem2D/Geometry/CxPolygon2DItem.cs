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
    /// Renders an array of <see cref="CxPolygon2D"/> values as polylines (open or closed).
    /// All polygons are batched into a single scatter with NaN separators.
    /// </summary>
    public class CxPolygon2DItem : Abstract2DRenderItem
    {
        private Scatter _plottable;
        private Plot    _plot;

        /// <summary>Gets the polygon data being rendered.</summary>
        public CxPolygon2D[] Polygons { get; private set; }

        /// <summary>Initializes the item with the given polygons, colour, and line width.</summary>
        public CxPolygon2DItem(CxPolygon2D[] polygons, Color color, float size = 1f)
        {
            Polygons = polygons ?? Array.Empty<CxPolygon2D>();
            Color    = color;
            Size     = size;
        }

        /// <inheritdoc/>
        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            BuildPlottable();
        }

        /// <inheritdoc/>
        public override void RemoveFromPlot(Plot plot)
        {
            if (_plottable != null) { plot.PlottableList.Remove(_plottable); _plottable = null; }
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            if (_plottable != null) _plot.PlottableList.Remove(_plottable);
            BuildPlottable();
        }

        private void BuildPlottable()
        {
            if (Polygons.Length == 0) return;

            var xs = new List<double>();
            var ys = new List<double>();

            foreach (var poly in Polygons)
            {
                if (poly.Points == null || poly.Points.Length == 0) continue;
                foreach (var pt in poly.Points) { xs.Add(pt.X); ys.Add(pt.Y); }
                if (poly.IsClosed && poly.Points.Length > 0) { xs.Add(poly.Points[0].X); ys.Add(poly.Points[0].Y); }
                xs.Add(double.NaN);
                ys.Add(double.NaN);
            }

            if (xs.Count == 0) return;

            _plottable = _plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            _plottable.MarkerStyle.IsVisible = false;
            _plottable.LineStyle.Width       = Size;
            _plottable.Color                 = ToSPColor(DrawColor);
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
            if (disposing && _plot != null && _plottable != null)
                _plot.PlottableList.Remove(_plottable);
        }
    }
}
