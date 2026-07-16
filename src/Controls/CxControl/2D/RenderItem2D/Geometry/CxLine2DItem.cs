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
    /// Renders an array of <see cref="CxLine2D"/> values as infinite lines
    /// (drawn as finite segments spanning a large range in both directions).
    /// Each line is rendered as a separate <see cref="Scatter"/> plottable.
    /// </summary>
    public class CxLine2DItem : Abstract2DRenderItem
    {
        private const float RenderRange = 1e4f;

        private readonly List<Scatter> _plottables = new List<Scatter>();
        private Plot _plot;

        /// <summary>Gets the line data being rendered.</summary>
        public CxLine2D[] Lines { get; private set; }

        /// <summary>Initializes the item with the given lines, colour, and line width.</summary>
        public CxLine2DItem(CxLine2D[] lines, Color color, float size = 1f)
        {
            Lines = lines ?? Array.Empty<CxLine2D>();
            Color = color;
            Size  = size;
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
            foreach (var s in _plottables) plot.PlottableList.Remove(s);
            _plottables.Clear();
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var s in _plottables) _plot.PlottableList.Remove(s);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            var spColor = ToSPColor(DrawColor);
            foreach (var line in Lines)
            {
                float sx = line.Point.X - line.Direction.X * RenderRange;
                float sy = line.Point.Y - line.Direction.Y * RenderRange;
                float ex = line.Point.X + line.Direction.X * RenderRange;
                float ey = line.Point.Y + line.Direction.Y * RenderRange;

                double[] xs = { sx, ex };
                double[] ys = { sy, ey };
                var s = _plot.Add.Scatter(xs, ys);
                s.MarkerStyle.IsVisible = false;
                s.LineStyle.Width       = Size;
                s.Color                 = spColor;
                _plottables.Add(s);
            }
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            float t2 = HitThreshold * HitThreshold;
            foreach (var line in Lines)
                if (DistSqToInfiniteLine(plotPos, line) <= t2) return true;
            return false;
        }

        private static float DistSqToInfiniteLine(CxPoint2D p, CxLine2D line)
        {
            float dx = line.Direction.X;
            float dy = line.Direction.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq == 0f)
            {
                float ex = p.X - line.Point.X;
                float ey = p.Y - line.Point.Y;
                return ex * ex + ey * ey;
            }
            float cx = (p.X - line.Point.X) * dy - (p.Y - line.Point.Y) * dx;
            return (cx * cx) / lenSq;
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            for (int i = 0; i < Lines.Length; i++)
                Lines[i] = new CxLine2D(
                    new CxPoint2D(Lines[i].Point.X + dx, Lines[i].Point.Y + dy),
                    Lines[i].Direction);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var s in _plottables) _plot.PlottableList.Remove(s);
        }
    }
}
