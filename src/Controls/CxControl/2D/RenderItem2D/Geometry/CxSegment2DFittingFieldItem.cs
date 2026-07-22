using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    public class CxSegment2DFittingFieldItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, DragStart, DragEnd, DragWidth }

        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly List<IPlottable> _handlePlottables = new List<IPlottable>();

        private const float HandlePixelSize = 8f;
        private const float ArrowPixelLen = 16f;

        private DragMode _dragMode;
        private int _activeIndex = -1;
        public CxSegment2DFittingField[] Fields { get; private set; }

        public CxSegment2DFittingFieldItem(CxSegment2DFittingField[] fields, Color color, float size = 1f)
        {
            Fields = fields ?? Array.Empty<CxSegment2DFittingField>();
            Color = color;
            Size = size;
        }

        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            BuildPlottables();
        }

        public override void RemoveFromPlot(Plot plot)
        {
            foreach (var p in _plottables) plot.PlottableList.Remove(p);
            RemoveHandlePlottables(plot);
            _plottables.Clear();
            _plot = null;
        }

        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var p in _plottables) _plot.PlottableList.Remove(p);
            RemoveHandlePlottables(_plot);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            for (int fi = 0; fi < Fields.Length; fi++)
            {
                var field = Fields[fi];
                var fColor = fi == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);
                var corners = GetRodCorners(field);

                var coords = new Coordinates[4];
                for (int j = 0; j < 4; j++)
                    coords[j] = new Coordinates(corners[j].X, corners[j].Y);

                var polygon = _plot.Add.Polygon(coords);
                polygon.LineStyle.Width = Size;
                polygon.LineStyle.Color = fColor;
                polygon.FillStyle.Color = fColor.WithAlpha(0.25f);
                polygon.FillStyle.IsVisible = true;
                polygon.MarkerStyle.IsVisible = false;
                _plottables.Add(polygon);

                float midX = (field.Axis.Start.X + field.Axis.End.X) / 2f;
                float midY = (field.Axis.Start.Y + field.Axis.End.Y) / 2f;
                var midLine = _plot.Add.Line(field.Axis.Start.X, field.Axis.Start.Y, field.Axis.End.X, field.Axis.End.Y);
                midLine.LineStyle.Width = Size;
                midLine.LineStyle.Color = fColor;
                _plottables.Add(midLine);
            }

            if (_activeIndex >= 0)
            {
                var field = Fields[_activeIndex];
                var hColor = ToSPColor(Color.Lime);

                var startMarker = _plot.Add.Marker(field.Axis.Start.X, field.Axis.Start.Y);
                startMarker.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                startMarker.MarkerStyle.Size = HandlePixelSize;
                startMarker.MarkerStyle.FillColor = hColor;
                startMarker.MarkerStyle.LineColor = hColor;
                startMarker.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(startMarker);

                float wp = WorldPerPixel();
                float axisDx = field.Axis.End.X - field.Axis.Start.X;
                float axisDy = field.Axis.End.Y - field.Axis.Start.Y;
                float axisLen = (float)Math.Sqrt(axisDx * axisDx + axisDy * axisDy);
                if (axisLen > 0) { axisDx /= axisLen; axisDy /= axisLen; }

                float arrowW = ArrowPixelLen * wp;
                var endArrow = _plot.Add.Arrow(
                    field.Axis.End.X - axisDx * arrowW, field.Axis.End.Y - axisDy * arrowW,
                    field.Axis.End.X, field.Axis.End.Y);
                endArrow.ArrowLineColor = hColor;
                endArrow.ArrowFillColor = hColor;
                endArrow.ArrowLineWidth = Size;
                _handlePlottables.Add(endArrow);

                float perpX = -axisDy, perpY = axisDx;
                float halfW = field.Width / 2f;
                float midX = (field.Axis.Start.X + field.Axis.End.X) / 2f;
                float midY = (field.Axis.Start.Y + field.Axis.End.Y) / 2f;

                var w1 = new CxPoint2D(midX + perpX * halfW, midY + perpY * halfW);
                var w2 = new CxPoint2D(midX - perpX * halfW, midY - perpY * halfW);

                var wh1 = _plot.Add.Marker(w1.X, w1.Y);
                wh1.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledSquare;
                wh1.MarkerStyle.Size = HandlePixelSize;
                wh1.MarkerStyle.FillColor = hColor;
                wh1.MarkerStyle.LineColor = hColor;
                wh1.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(wh1);

                var wh2 = _plot.Add.Marker(w2.X, w2.Y);
                wh2.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledSquare;
                wh2.MarkerStyle.Size = HandlePixelSize;
                wh2.MarkerStyle.FillColor = hColor;
                wh2.MarkerStyle.LineColor = hColor;
                wh2.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(wh2);
            }
        }

        public override bool HitTest(CxPoint2D plotPos)
        {
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW;
            float t2v = t2 * 4f;

            for (int fi = 0; fi < Fields.Length; fi++)
            {
                var field = Fields[fi];
                var corners = GetRodCorners(field);

                for (int j = 0; j < 4; j++)
                {
                    float dx = plotPos.X - corners[j].X;
                    float dy = plotPos.Y - corners[j].Y;
                    if (dx * dx + dy * dy <= t2v) { _activeIndex = fi; return true; }
                }

                var (w1, w2) = GetWidthHandles(field);
                float hw = HandlePixelSize * WorldPerPixel();
                float hT2 = hw * hw * 4f;
                float wdx1 = plotPos.X - w1.X, wdy1 = plotPos.Y - w1.Y;
                if (wdx1 * wdx1 + wdy1 * wdy1 <= hT2) { _activeIndex = fi; return true; }
                float wdx2 = plotPos.X - w2.X, wdy2 = plotPos.Y - w2.Y;
                if (wdx2 * wdx2 + wdy2 * wdy2 <= hT2) { _activeIndex = fi; return true; }

                for (int j = 0; j < 4; j++)
                {
                    var a = corners[j];
                    var b = corners[(j + 1) % 4];
                    float edx = b.X - a.X;
                    float edy = b.Y - a.Y;
                    float lenSq = edx * edx + edy * edy;
                    float d;
                    if (lenSq == 0f)
                    {
                        float ex = plotPos.X - a.X;
                        float ey = plotPos.Y - a.Y;
                        d = ex * ex + ey * ey;
                    }
                    else
                    {
                        float t = Math.Max(0, Math.Min(1, ((plotPos.X - a.X) * edx + (plotPos.Y - a.Y) * edy) / lenSq));
                        float cx = a.X + t * edx - plotPos.X;
                        float cy = a.Y + t * edy - plotPos.Y;
                        d = cx * cx + cy * cy;
                    }
                    if (d <= t2) { _activeIndex = fi; return true; }
                }

                if (VisionOperator.IsPointInPolygon2D(plotPos, new CxPolygon2D(corners, true)))
                {
                    _activeIndex = fi;
                    return true;
                }
            }

            return false;
        }

        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }

            var field = Fields[_activeIndex];
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW * 4f;
            float hw = HandlePixelSize * WorldPerPixel();
            float hT2 = hw * hw * 4f;

            var (w1, w2) = GetWidthHandles(field);
            float wdx1 = plotPos.X - w1.X, wdy1 = plotPos.Y - w1.Y;
            if (wdx1 * wdx1 + wdy1 * wdy1 <= hT2)
            { _dragMode = DragMode.DragWidth; UpdatePlottable(); return; }
            float wdx2 = plotPos.X - w2.X, wdy2 = plotPos.Y - w2.Y;
            if (wdx2 * wdx2 + wdy2 * wdy2 <= hT2)
            { _dragMode = DragMode.DragWidth; UpdatePlottable(); return; }

            _dragMode = DragMode.Translate;

            float dStartX = plotPos.X - field.Axis.Start.X;
            float dStartY = plotPos.Y - field.Axis.Start.Y;
            if (dStartX * dStartX + dStartY * dStartY <= t2)
            { _dragMode = DragMode.DragStart; }
            else
            {
                float dEndX = plotPos.X - field.Axis.End.X;
                float dEndY = plotPos.Y - field.Axis.End.Y;
                if (dEndX * dEndX + dEndY * dEndY <= t2)
                    _dragMode = DragMode.DragEnd;
            }

            UpdatePlottable();
        }

        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeIndex < 0) return;

            var field = Fields[_activeIndex];

            switch (_dragMode)
            {
                case DragMode.DragStart:
                {
                    Fields[_activeIndex] = new CxSegment2DFittingField(
                        new CxSegment2D(plotPos, field.Axis.End), field.Width);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragEnd:
                {
                    Fields[_activeIndex] = new CxSegment2DFittingField(
                        new CxSegment2D(field.Axis.Start, plotPos), field.Width);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragWidth:
                {
                    float dx = field.Axis.End.X - field.Axis.Start.X;
                    float dy = field.Axis.End.Y - field.Axis.Start.Y;
                    float len = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (len == 0) return;
                    dx /= len; dy /= len;
                    float perpX = -dy, perpY = dx;

                    float midX = (field.Axis.Start.X + field.Axis.End.X) / 2f;
                    float midY = (field.Axis.Start.Y + field.Axis.End.Y) / 2f;

                    float dist = (plotPos.X - midX) * perpX + (plotPos.Y - midY) * perpY;
                    float newW = Math.Max(1f, Math.Abs(dist) * 2f);
                    Fields[_activeIndex] = new CxSegment2DFittingField(field.Axis, newW);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                default:
                    base.OnMouseMove(plotPos, prevPlotPos);
                    break;
            }
        }

        public override void OnMouseUp()
        {
            _dragMode = DragMode.None;
        }

        public override void OnDeselected()
        {
            _activeIndex = -1;
            _dragMode = DragMode.None;
            UpdatePlottable();
        }

        public override void Translate(float dx, float dy)
        {
            if (_activeIndex >= 0)
            {
                var f = Fields[_activeIndex];
                Fields[_activeIndex] = new CxSegment2DFittingField(
                    new CxSegment2D(
                        new CxPoint2D(f.Axis.Start.X + dx, f.Axis.Start.Y + dy),
                        new CxPoint2D(f.Axis.End.X + dx, f.Axis.End.Y + dy)),
                    f.Width);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
            {
                foreach (var p in _plottables) _plot.PlottableList.Remove(p);
                RemoveHandlePlottables(_plot);
            }
        }

        private void RemoveHandlePlottables(Plot plot)
        {
            foreach (var h in _handlePlottables) plot.PlottableList.Remove(h);
            _handlePlottables.Clear();
        }

        private static CxPoint2D[] GetRodCorners(CxSegment2DFittingField field)
        {
            float dx = field.Axis.End.X - field.Axis.Start.X;
            float dy = field.Axis.End.Y - field.Axis.Start.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 0) { dx /= len; dy /= len; }

            float perpX = -dy, perpY = dx;
            float halfW = field.Width / 2f;

            return new[]
            {
                new CxPoint2D(field.Axis.Start.X + perpX * halfW, field.Axis.Start.Y + perpY * halfW),
                new CxPoint2D(field.Axis.End.X   + perpX * halfW, field.Axis.End.Y   + perpY * halfW),
                new CxPoint2D(field.Axis.End.X   - perpX * halfW, field.Axis.End.Y   - perpY * halfW),
                new CxPoint2D(field.Axis.Start.X - perpX * halfW, field.Axis.Start.Y - perpY * halfW),
            };
        }

        private static (CxPoint2D w1, CxPoint2D w2) GetWidthHandles(CxSegment2DFittingField field)
        {
            float dx = field.Axis.End.X - field.Axis.Start.X;
            float dy = field.Axis.End.Y - field.Axis.Start.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 0) { dx /= len; dy /= len; }

            float perpX = -dy, perpY = dx;
            float halfW = field.Width / 2f;
            float midX = (field.Axis.Start.X + field.Axis.End.X) / 2f;
            float midY = (field.Axis.Start.Y + field.Axis.End.Y) / 2f;

            return (
                new CxPoint2D(midX + perpX * halfW, midY + perpY * halfW),
                new CxPoint2D(midX - perpX * halfW, midY - perpY * halfW));
    }
}
}
