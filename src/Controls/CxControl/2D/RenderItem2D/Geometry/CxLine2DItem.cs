using System;
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
    /// All lines are batched into a single scatter with NaN separators for efficiency.
    /// </summary>
    public class CxLine2DItem : Abstract2DRenderItem
    {
        private const float RenderRange = 1e4f;

        private Scatter _plottable;
        private Plot    _plot;

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
            if (Lines.Length == 0) return;

            // Each line: Point - D*R → Point + D*R → NaN (3 entries per line)
            double[] xs = new double[Lines.Length * 3];
            double[] ys = new double[Lines.Length * 3];
            for (int i = 0; i < Lines.Length; i++)
            {
                var line = Lines[i];
                float sx = line.Point.X - line.Direction.X * RenderRange;
                float sy = line.Point.Y - line.Direction.Y * RenderRange;
                float ex = line.Point.X + line.Direction.X * RenderRange;
                float ey = line.Point.Y + line.Direction.Y * RenderRange;

                xs[i * 3]     = sx;
                ys[i * 3]     = sy;
                xs[i * 3 + 1] = ex;
                ys[i * 3 + 1] = ey;
                xs[i * 3 + 2] = double.NaN;
                ys[i * 3 + 2] = double.NaN;
            }

            _plottable = _plot.Add.Scatter(xs, ys);
            _plottable.MarkerStyle.IsVisible = false;
            _plottable.LineStyle.Width       = Size;
            _plottable.Color                 = ToSPColor(DrawColor);
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
            // Cross product magnitude squared / lenSq
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
            if (disposing && _plot != null && _plottable != null)
                _plot.PlottableList.Remove(_plottable);
        }
    }
}
