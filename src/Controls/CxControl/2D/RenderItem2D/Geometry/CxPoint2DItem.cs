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
    /// All points share a single <see cref="Scatter"/> plottable.
    /// Only the point that was hit by <see cref="HitTest"/> is moved during drag;
    /// colour always follows <see cref="Abstract2DRenderItem.DrawColor"/>.
    /// </summary>
    public class CxPoint2DItem : Abstract2DRenderItem
    {
        private Scatter _plottable;
        private Plot    _plot;
        private int     _activeIndex = -1;

        /// <summary>Gets the point data being rendered.</summary>
        public CxPoint2D[] Points { get; private set; }

        /// <summary>Initializes the item with the given points, colour, and marker size.</summary>
        public CxPoint2DItem(CxPoint2D[] points, Color color, float size = 5f)
        {
            Points = points ?? Array.Empty<CxPoint2D>();
            Color  = color;
            Size   = size;
            if (Points.Length > 0)
                HitThreshold = Math.Max(1f, Size * 0.6f);
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
            float t2 = HitThreshold * HitThreshold;
            for (int pi = 0; pi < Points.Length; pi++)
            {
                float dx = Points[pi].X - plotPos.X;
                float dy = Points[pi].Y - plotPos.Y;
                if (dx * dx + dy * dy <= t2)
                {
                    _activeIndex = pi;
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }
            IsSelected = true;
            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void OnMouseUp()
        {
        }

        /// <inheritdoc/>
        public override void OnDeselected()
        {
            _activeIndex = -1;
            IsSelected = false;
            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            if (_activeIndex >= 0 && _activeIndex < Points.Length)
                Points[_activeIndex] = new CxPoint2D(
                    Points[_activeIndex].X + dx,
                    Points[_activeIndex].Y + dy);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null && _plottable != null)
                _plot.PlottableList.Remove(_plottable);
        }
    }
}
