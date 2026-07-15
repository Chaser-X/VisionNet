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
    /// Renders an array of <see cref="CxCircle2D"/> values as polygon-approximated circles.
    /// All circles are batched into a single scatter with NaN separators.
    /// For circles to appear as true circles, use <see cref="CxDisplay2D.SetAspectLock"/>.
    /// </summary>
    public class CxCircle2DItem : Abstract2DRenderItem
    {
        private const int ApproxSegments = 64;

        private Scatter _plottable;
        private Plot    _plot;

        /// <summary>Gets the circle data being rendered.</summary>
        public CxCircle2D[] Circles { get; private set; }

        /// <summary>Initializes the item with the given circles, colour, and line width.</summary>
        public CxCircle2DItem(CxCircle2D[] circles, Color color, float size = 1f)
        {
            Circles = circles ?? Array.Empty<CxCircle2D>();
            Color   = color;
            Size    = size;
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
            if (Circles.Length == 0) return;

            var xs = new List<double>();
            var ys = new List<double>();

            foreach (var circle in Circles)
            {
                for (int i = 0; i <= ApproxSegments; i++)
                {
                    double angle = 2.0 * Math.PI * i / ApproxSegments;
                    xs.Add(circle.Center.X + circle.Radius * Math.Cos(angle));
                    ys.Add(circle.Center.Y + circle.Radius * Math.Sin(angle));
                }
                xs.Add(double.NaN);
                ys.Add(double.NaN);
            }

            _plottable = _plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            _plottable.MarkerStyle.IsVisible = false;
            _plottable.LineStyle.Width       = Size;
            _plottable.Color                 = ToSPColor(DrawColor);
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            foreach (var c in Circles)
            {
                float dx   = plotPos.X - c.Center.X;
                float dy   = plotPos.Y - c.Center.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (Math.Abs(dist - c.Radius) <= HitThreshold) return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            for (int i = 0; i < Circles.Length; i++)
                Circles[i] = new CxCircle2D(
                    new CxPoint2D(Circles[i].Center.X + dx, Circles[i].Center.Y + dy),
                    Circles[i].Radius);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null && _plottable != null)
                _plot.PlottableList.Remove(_plottable);
        }
    }
}
