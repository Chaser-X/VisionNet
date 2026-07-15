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
    /// Renders an array of <see cref="CxSegment2D"/> values as disconnected line segments.
    /// Segments are batched into a single scatter with NaN separators for efficiency.
    /// </summary>
    public class CxSegment2DItem : Abstract2DRenderItem
    {
        private Scatter   _plottable;
        private Plot      _plot;

        /// <summary>Gets the segment data being rendered.</summary>
        public CxSegment2D[] Segments { get; private set; }

        /// <summary>Initializes the item with the given segments, colour, and line width.</summary>
        public CxSegment2DItem(CxSegment2D[] segments, Color color, float size = 1f)
        {
            Segments = segments ?? Array.Empty<CxSegment2D>();
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
            if (Segments.Length == 0) return;

            // Each segment: start → end → NaN (3 entries per segment)
            double[] xs = new double[Segments.Length * 3];
            double[] ys = new double[Segments.Length * 3];
            for (int i = 0; i < Segments.Length; i++)
            {
                xs[i * 3]     = Segments[i].Start.X;
                ys[i * 3]     = Segments[i].Start.Y;
                xs[i * 3 + 1] = Segments[i].End.X;
                ys[i * 3 + 1] = Segments[i].End.Y;
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
            foreach (var seg in Segments)
                if (DistSqToSegment(plotPos, seg) <= t2) return true;
            return false;
        }

        private static float DistSqToSegment(CxPoint2D p, CxSegment2D seg)
        {
            float dx = seg.End.X - seg.Start.X;
            float dy = seg.End.Y - seg.Start.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq == 0f) { float ex = p.X - seg.Start.X; float ey = p.Y - seg.Start.Y; return ex * ex + ey * ey; }
            float t = Math.Max(0, Math.Min(1, ((p.X - seg.Start.X) * dx + (p.Y - seg.Start.Y) * dy) / lenSq));
            float cx = seg.Start.X + t * dx - p.X;
            float cy = seg.Start.Y + t * dy - p.Y;
            return cx * cx + cy * cy;
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            for (int i = 0; i < Segments.Length; i++)
                Segments[i] = new CxSegment2D(
                    new CxPoint2D(Segments[i].Start.X + dx, Segments[i].Start.Y + dy),
                    new CxPoint2D(Segments[i].End.X   + dx, Segments[i].End.Y   + dy));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null && _plottable != null)
                _plot.PlottableList.Remove(_plottable);
        }
    }
}
