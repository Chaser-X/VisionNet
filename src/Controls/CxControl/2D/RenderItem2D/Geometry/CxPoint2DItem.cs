using System;
using System.Drawing;
using System.Linq;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxPoint2D"/> values as scatter markers.
    /// </summary>
    public class CxPoint2DItem : Abstract2DRenderItem
    {
        private Scatter _plottable;
        private Plot    _plot;

        /// <summary>Gets the point data being rendered.</summary>
        public CxPoint2D[] Points { get; private set; }

        /// <summary>Initializes the item with the given points, colour, and marker size.</summary>
        public CxPoint2DItem(CxPoint2D[] points, Color color, float size = 5f)
        {
            Points = points ?? Array.Empty<CxPoint2D>();
            Color  = color;
            Size   = size;
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
            if (Points.Length == 0) return;

            double[] xs = Points.Select(p => (double)p.X).ToArray();
            double[] ys = Points.Select(p => (double)p.Y).ToArray();

            _plottable = _plot.Add.Scatter(xs, ys);
            _plottable.LineStyle.IsVisible   = false;
            _plottable.MarkerStyle.IsVisible = true;
            _plottable.MarkerStyle.Size      = Size;
            _plottable.Color                 = ToSPColor(DrawColor);
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            foreach (var p in Points)
            {
                float dx = p.X - plotPos.X;
                float dy = p.Y - plotPos.Y;
                if (dx * dx + dy * dy <= HitThreshold * HitThreshold) return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            for (int i = 0; i < Points.Length; i++)
                Points[i] = new CxPoint2D(Points[i].X + dx, Points[i].Y + dy);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null && _plottable != null)
                _plot.PlottableList.Remove(_plottable);
        }
    }
}
