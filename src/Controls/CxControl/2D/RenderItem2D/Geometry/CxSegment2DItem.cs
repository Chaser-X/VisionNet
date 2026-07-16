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
    ///
    /// Drag interaction:
    /// <list type="bullet">
    ///   <item>Click near a vertex (Start/End) → drag to reshape that endpoint.</item>
    ///   <item>Click near the edge → drag to translate the whole segment.</item>
    /// </list>
    /// Only the segment that was hit by <see cref="HitTest"/> is drawn in <see cref="Abstract2DRenderItem.SelectedColor"/>;
    /// all others use <see cref="Abstract2DRenderItem.Color"/>.
    /// </summary>
    public class CxSegment2DItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, DragVertex }

        private readonly List<Scatter> _plottables = new List<Scatter>();
        private Plot _plot;

        private DragMode _dragMode;
        private int      _activeIndex = -1;
        private int      _vertexIndex;   // 0 = Start, 1 = End

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
            for (int si = 0; si < Segments.Length; si++)
            {
                var seg = Segments[si];
                var spColor = si == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

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
            for (int si = 0; si < Segments.Length; si++)
            {
                if (DistSqToSegment(plotPos, Segments[si]) <= t2)
                {
                    _activeIndex = si;
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }

            var seg = Segments[_activeIndex];
            float t2 = HitThreshold * HitThreshold * 4f;

            _dragMode = DragMode.Translate;

            float dx0 = plotPos.X - seg.Start.X;
            float dy0 = plotPos.Y - seg.Start.Y;
            if (dx0 * dx0 + dy0 * dy0 <= t2)
            {
                _vertexIndex = 0;
                _dragMode = DragMode.DragVertex;
            }
            else
            {
                float dx1 = plotPos.X - seg.End.X;
                float dy1 = plotPos.Y - seg.End.Y;
                if (dx1 * dx1 + dy1 * dy1 <= t2)
                {
                    _vertexIndex = 1;
                    _dragMode = DragMode.DragVertex;
                }
            }

            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeIndex < 0) return;

            if (_dragMode == DragMode.DragVertex)
            {
                var seg = Segments[_activeIndex];
                Segments[_activeIndex] = _vertexIndex == 0
                    ? new CxSegment2D(new CxPoint2D((float)plotPos.X, (float)plotPos.Y), seg.End)
                    : new CxSegment2D(seg.Start, new CxPoint2D((float)plotPos.X, (float)plotPos.Y));

                UpdatePlottable();
                RaiseOnChanged();
            }
            else
            {
                base.OnMouseMove(plotPos, prevPlotPos);
            }
        }

        /// <inheritdoc/>
        public override void OnMouseUp()
        {
            _dragMode = DragMode.None;
        }

        /// <inheritdoc/>
        public override void OnDeselected()
        {
            _activeIndex = -1;
            _dragMode = DragMode.None;
            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            if (_activeIndex >= 0)
            {
                var s = Segments[_activeIndex];
                Segments[_activeIndex] = new CxSegment2D(
                    new CxPoint2D(s.Start.X + dx, s.Start.Y + dy),
                    new CxPoint2D(s.End.X   + dx, s.End.Y   + dy));
            }
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
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var s in _plottables) _plot.PlottableList.Remove(s);
        }
    }
}
