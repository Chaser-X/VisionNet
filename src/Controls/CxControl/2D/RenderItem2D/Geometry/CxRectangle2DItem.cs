using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    public class CxRectangle2DItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, ResizeVertex, Rotate }

        private readonly List<Polygon> _plottables = new List<Polygon>();
        private readonly List<IPlottable> _handlePlottables = new List<IPlottable>();

        private const float HandlePixelSize = 8f;

        private DragMode _dragMode;
        private int _activeIndex = -1;
        private int _vertexIndex;
        private CxPoint2D _fixedCorner;

        private float _rotateStartAngle;
        private float _rotateStartRectAngle;

        public CxRectangle2D[] Rectangles { get; private set; }
        public bool Filled { get; set; } = false;

        public CxRectangle2DItem(CxRectangle2D[] rects, Color color, float size = 1f, bool filled = false)
        {
            Rectangles = rects ?? Array.Empty<CxRectangle2D>();
            Color = color;
            Size = size;
            Filled = filled;

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

        private void RemoveHandlePlottables(Plot plot)
        {
            foreach (var h in _handlePlottables) plot.PlottableList.Remove(h);
            _handlePlottables.Clear();
        }

        private void BuildPlottables()
        {
            for (int i = 0; i < Rectangles.Length; i++)
            {
                var rect = Rectangles[i];
                var color = i == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);
                var corners = GetCorners(rect);

                var coords = new Coordinates[4];
                for (int j = 0; j < 4; j++)
                    coords[j] = new Coordinates(corners[j].X, corners[j].Y);

                var polygon = _plot.Add.Polygon(coords);
                polygon.LineStyle.Width = Size;
                polygon.LineStyle.Color = color;
                polygon.FillStyle.Color = color;
                polygon.FillStyle.IsVisible = Filled;
                polygon.MarkerStyle.IsVisible = false;
                _plottables.Add(polygon);
            }

            if (_activeIndex >= 0)
            {
                var rect = Rectangles[_activeIndex];
                var hColor = ToSPColor(Color.Lime);

                var corners = GetCorners(rect);
                for (int j = 0; j < 4; j++)
                {
                    var v = _plot.Add.Marker(corners[j].X, corners[j].Y);
                    v.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledSquare;
                    v.MarkerStyle.Size = HandlePixelSize;
                    v.MarkerStyle.FillColor = hColor;
                    v.MarkerStyle.LineColor = hColor;
                    v.MarkerStyle.LineWidth = 1;
                    _handlePlottables.Add(v);
                }

                var handlePos = GetHandlePos(rect);
                var m = _plot.Add.Marker(handlePos.X, handlePos.Y);
                m.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                m.MarkerStyle.Size = HandlePixelSize;
                m.MarkerStyle.FillColor = hColor;
                m.MarkerStyle.LineColor = hColor;
                m.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(m);
            }
        }

        public override bool HitTest(CxPoint2D plotPos)
        {
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW;
            float t2v = t2 * 4f;

            for (int i = 0; i < Rectangles.Length; i++)
            {
                var rect = Rectangles[i];
                var corners = GetCorners(rect);

                for (int j = 0; j < 4; j++)
                {
                    float dx = plotPos.X - corners[j].X;
                    float dy = plotPos.Y - corners[j].Y;
                    if (dx * dx + dy * dy <= t2v) { _activeIndex = i; return true; }
                }

                var hPos = GetHandlePos(rect);
                float hdx = plotPos.X - hPos.X;
                float hdy = plotPos.Y - hPos.Y;
                float handleW = HandlePixelSize * WorldPerPixel();
                float hT2 = handleW * handleW * 4f;
                if (hdx * hdx + hdy * hdy <= hT2) { _activeIndex = i; return true; }

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
                    if (d <= t2) { _activeIndex = i; return true; }
                }

                if (Filled && PointInConvexPolygon(plotPos, corners))
                {
                    _activeIndex = i;
                    return true;
                }
            }

            return false;
        }

        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }

            var rect = Rectangles[_activeIndex];
            var corners = GetCorners(rect);
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW * 4f;

            var hPos = GetHandlePos(rect);
            float hdx = plotPos.X - hPos.X;
            float hdy = plotPos.Y - hPos.Y;
            float handleW = HandlePixelSize * WorldPerPixel();
            float hT2 = handleW * handleW * 4f;
            if (hdx * hdx + hdy * hdy <= hT2)
            {
                _dragMode = DragMode.Rotate;
                _rotateStartAngle = (float)Math.Atan2(plotPos.Y - rect.Center.Y, plotPos.X - rect.Center.X);
                _rotateStartRectAngle = rect.Angle;
                UpdatePlottable();
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                float dx = plotPos.X - corners[i].X;
                float dy = plotPos.Y - corners[i].Y;
                if (dx * dx + dy * dy <= t2)
                {
                    _vertexIndex = i;
                    _fixedCorner = corners[(i + 2) % 4];
                    _dragMode = DragMode.ResizeVertex;
                    UpdatePlottable();
                    return;
                }
            }

            _dragMode = DragMode.Translate;
            UpdatePlottable();
        }

        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeIndex < 0) return;

            var rect = Rectangles[_activeIndex];

            switch (_dragMode)
            {
                case DragMode.ResizeVertex:
                {
                    float newCx = (_fixedCorner.X + plotPos.X) / 2f;
                    float newCy = (_fixedCorner.Y + plotPos.Y) / 2f;

                    float invRad = -rect.Angle * (float)Math.PI / 180f;
                    float cos = (float)Math.Cos(invRad);
                    float sin = (float)Math.Sin(invRad);

                    float fdx = _fixedCorner.X - newCx;
                    float fdy = _fixedCorner.Y - newCy;
                    float lfx = fdx * cos - fdy * sin;
                    float lfy = fdx * sin + fdy * cos;

                    float mdx = plotPos.X - newCx;
                    float mdy = plotPos.Y - newCy;
                    float lmx = mdx * cos - mdy * sin;
                    float lmy = mdx * sin + mdy * cos;

                    float w = Math.Abs(lmx - lfx);
                    float h = Math.Abs(lmy - lfy);
                    if (w < 1f) w = 1f;
                    if (h < 1f) h = 1f;

                    Rectangles[_activeIndex] = new CxRectangle2D(
                        new CxPoint2D(newCx, newCy),
                        new CxSize2D(w, h), rect.Angle);

                    UpdatePlottable();
                    RaiseOnChanged();
                    break;
                }
                case DragMode.Rotate:
                {
                    float currentAngle = (float)Math.Atan2(plotPos.Y - rect.Center.Y, plotPos.X - rect.Center.X);
                    float deltaRad = currentAngle - _rotateStartAngle;
                    while (deltaRad > Math.PI) deltaRad -= 2f * (float)Math.PI;
                    while (deltaRad < -Math.PI) deltaRad += 2f * (float)Math.PI;
                    float deltaDeg = deltaRad * 180f / (float)Math.PI;
                    Rectangles[_activeIndex] = new CxRectangle2D(
                        rect.Center, rect.Size, _rotateStartRectAngle + deltaDeg);
                    UpdatePlottable();
                    RaiseOnChanged();
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
                var r = Rectangles[_activeIndex];
                Rectangles[_activeIndex] = new CxRectangle2D(
                    new CxPoint2D(r.Center.X + dx, r.Center.Y + dy), r.Size, r.Angle);
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

        private static CxPoint2D[] GetCorners(CxRectangle2D rect)
        {
            float hw = rect.Size.Width / 2f;
            float hh = rect.Size.Height / 2f;
            float rad = rect.Angle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);

            float[] lx = { -hw, +hw, +hw, -hw };
            float[] ly = { -hh, -hh, +hh, +hh };

            var corners = new CxPoint2D[4];
            for (int i = 0; i < 4; i++)
            {
                corners[i] = new CxPoint2D(
                    rect.Center.X + lx[i] * cos - ly[i] * sin,
                    rect.Center.Y + lx[i] * sin + ly[i] * cos);
            }
            return corners;
        }

        private static CxPoint2D GetHandlePos(CxRectangle2D rect)
        {
            float hh = rect.Size.Height / 2f;
            float rad = rect.Angle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);

            float ly = -hh;
            return new CxPoint2D(
                rect.Center.X - ly * sin,
                rect.Center.Y + ly * cos);
        }

        private static bool PointInConvexPolygon(CxPoint2D p, CxPoint2D[] corners)
        {
            bool? sign = null;
            for (int i = 0; i < corners.Length; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Length];
                float cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
                if (cross != 0)
                {
                    bool currentSign = cross > 0;
                    if (sign == null) sign = currentSign;
                    else if (sign != currentSign) return false;
                }
            }
            return true;
        }
    }
}
