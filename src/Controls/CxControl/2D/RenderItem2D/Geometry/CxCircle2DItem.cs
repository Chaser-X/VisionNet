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
    /// Renders an array of <see cref="CxCircle2D"/> values as circles with optional fill.
    /// Each circle is a separate <see cref="Ellipse"/> plottable.
    /// For circles to appear as true circles, use <see cref="CxDisplay2D.SetAspectLock"/>.
    /// </summary>
    public class CxCircle2DItem : Abstract2DRenderItem
    {
        private readonly List<Ellipse> _plottables = new List<Ellipse>();
        private Plot _plot;

        /// <summary>Gets the circle data being rendered.</summary>
        public CxCircle2D[] Circles { get; private set; }

        /// <summary>Gets or sets whether circles are filled with their colour.</summary>
        public bool Filled { get; set; } = false;

        /// <summary>Initializes the item with the given circles, colour, line width, and optional fill.</summary>
        public CxCircle2DItem(CxCircle2D[] circles, Color color, float size = 1f, bool filled = false)
        {
            Circles = circles ?? Array.Empty<CxCircle2D>();
            Color   = color;
            Size    = size;
            Filled  = filled;
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
            foreach (var e in _plottables) plot.PlottableList.Remove(e);
            _plottables.Clear();
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var e in _plottables) _plot.PlottableList.Remove(e);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            var spColor = ToSPColor(DrawColor);
            foreach (var circle in Circles)
            {
                var ellipse = _plot.Add.Circle(circle.Center.X, circle.Center.Y, circle.Radius);
                ellipse.LineStyle.Width     = Size;
                ellipse.LineStyle.Color     = spColor;
                ellipse.FillStyle.Color     = spColor;
                ellipse.FillStyle.IsVisible = Filled;
                _plottables.Add(ellipse);
            }
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            foreach (var c in Circles)
            {
                float dx = plotPos.X - c.Center.X;
                float dy = plotPos.Y - c.Center.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (Filled)
                {
                    if (dist <= c.Radius + HitThreshold) return true;
                }
                else
                {
                    if (Math.Abs(dist - c.Radius) <= HitThreshold) return true;
                }
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
            if (disposing && _plot != null)
                foreach (var e in _plottables) _plot.PlottableList.Remove(e);
        }
    }
}
