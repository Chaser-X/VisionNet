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
    ///
    /// Only the line that was hit by <see cref="HitTest"/> is drawn in <see cref="Abstract2DRenderItem.SelectedColor"/>;
    /// all others use <see cref="Abstract2DRenderItem.Color"/>.
    /// </summary>
    public class CxLine2DItem : Abstract2DRenderItem
    {
        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private int _activeIndex = -1;

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
            for (int li = 0; li < Lines.Length; li++)
            {
                var line = Lines[li];
                var spColor = li == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

                float sx = line.Point.X - line.Direction.X * 1e4f;
                float sy = line.Point.Y - line.Direction.Y * 1e4f;
                float ex = line.Point.X + line.Direction.X * 1e4f;
                float ey = line.Point.Y + line.Direction.Y * 1e4f;

                var s = _plot.Add.Line(sx, sy, ex, ey);
                s.LineWidth = Size;
                s.Color     = spColor;
                _plottables.Add(s);
            }
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW;
            for (int li = 0; li < Lines.Length; li++)
            {
                if (DistSqToInfiniteLine(plotPos, Lines[li]) <= t2)
                {
                    _activeIndex = li;
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void OnDeselected()
        {
            _activeIndex = -1;
            base.OnDeselected();
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            if (_activeIndex >= 0)
            {
                var l = Lines[_activeIndex];
                Lines[_activeIndex] = new CxLine2D(
                    new CxPoint2D(l.Point.X + dx, l.Point.Y + dy),
                    l.Direction);
            }
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
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var s in _plottables) _plot.PlottableList.Remove(s);
        }
    }
}
