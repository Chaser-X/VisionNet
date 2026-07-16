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
    /// Each segment is rendered as a separate <see cref="Scatter"/> plottable.
    /// </summary>
    public class CxSegment2DItem : Abstract2DRenderItem
    {
        private readonly List<Scatter> _plottables = new List<Scatter>();
        private Plot _plot;

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
            foreach (var seg in Segments)
            {
                double[] xs = { seg.Start.X, seg.End.X };
                double[] ys = { seg.Start.Y, seg.End.Y };
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
            if (disposing && _plot != null)
                foreach (var s in _plottables) _plot.PlottableList.Remove(s);
        }
    }
}
