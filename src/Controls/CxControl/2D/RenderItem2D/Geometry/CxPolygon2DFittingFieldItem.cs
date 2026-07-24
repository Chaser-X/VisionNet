using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;
namespace VisionNet.Controls
{
    public class CxPolygon2DFittingFieldItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, DragVertex, DragWidth }
        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly List<IPlottable> _handlePlottables = new List<IPlottable>();
        private const float HandlePixelSize = 8f;
        private const float ArrowPixelLen = 16f;
        private DragMode _dragMode;
        private int _activeIndex = -1;
        private int _vertexIndex;
        public CxPolygon2DFittingField[] Fields { get; private set; }
        public CxPolygon2DFittingFieldItem(CxPolygon2DFittingField[] fields, Color color, float size = 1f)
        {
            Fields = fields ?? Array.Empty<CxPolygon2DFittingField>();
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
                var pts = field.Axis.Points;
                if (pts == null || pts.Length < 2) continue;
                var fColor = fi == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);
                float hw = field.Width / 2f;
                var bandVerts = ComputeBandVertices(pts, hw, field.Axis.IsClosed);
                if (bandVerts != null && bandVerts.Length >= 3)
                {
                    var bCoords = new Coordinates[bandVerts.Length];
                    for (int i = 0; i < bandVerts.Length; i++)
                        bCoords[i] = new Coordinates(bandVerts[i].X, bandVerts[i].Y);
                    var band = _plot.Add.Polygon(bCoords);
                    band.LineStyle.Width = Size;
                    band.LineStyle.Color = fColor;
                    band.FillStyle.Color = fColor.WithAlpha(0.25f);
                    band.FillStyle.IsVisible = true;
                    band.MarkerStyle.IsVisible = false;
                    _plottables.Add(band);
                }
                if (field.Axis.IsClosed)
                {
                    var aCoords = new Coordinates[pts.Length];
                    for (int i = 0; i < pts.Length; i++)
                        aCoords[i] = new Coordinates(pts[i].X, pts[i].Y);
                    var ap = _plot.Add.Polygon(aCoords);
                    ap.LineStyle.Width = Size;
                    ap.LineStyle.Color = fColor;
                    ap.FillStyle.IsVisible = false;
                    ap.MarkerStyle.IsVisible = false;
                    _plottables.Add(ap);
                }
                else
                {
                    double[] xs = new double[pts.Length];
                    double[] ys = new double[pts.Length];
                    for (int i = 0; i < pts.Length; i++)
                    { xs[i] = pts[i].X; ys[i] = pts[i].Y; }
                    var as_ = _plot.Add.Scatter(xs, ys);
                    as_.MarkerStyle.IsVisible = false;
                    as_.LineStyle.Width = Size;
                    as_.Color = fColor;
                    _plottables.Add(as_);
                }
            }
            if (_activeIndex >= 0)
            {
                var field = Fields[_activeIndex];
                var pts = field.Axis.Points;
                if (pts == null || pts.Length < 2) return;
                var hColor = ToSPColor(Color.Lime);
                for (int j = 0; j < pts.Length; j++)
                {
                    var v = _plot.Add.Marker(pts[j].X, pts[j].Y);
                    v.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                    v.MarkerStyle.Size = HandlePixelSize;
                    v.MarkerStyle.FillColor = hColor;
                    v.MarkerStyle.LineColor = hColor;
                    v.MarkerStyle.LineWidth = 1;
                    _handlePlottables.Add(v);
                }
                int last = pts.Length - 1;
                CxPoint2D endPt = pts[last];
                CxPoint2D endDir = last > 0
                    ? new CxPoint2D(pts[last].X - pts[last - 1].X, pts[last].Y - pts[last - 1].Y)
                    : new CxPoint2D(0, 0);
                float eLen = (float)Math.Sqrt(endDir.X * endDir.X + endDir.Y * endDir.Y);
                if (eLen > 0) { endDir = new CxPoint2D(endDir.X / eLen, endDir.Y / eLen); }
                float wp = WorldPerPixel();
                if (eLen > 0)
                {
                    float aLen = ArrowPixelLen * wp;
                    var endArrow = _plot.Add.Arrow(
                        endPt.X - endDir.X * aLen, endPt.Y - endDir.Y * aLen,
                        endPt.X, endPt.Y);
                    endArrow.ArrowLineColor = hColor;
                    endArrow.ArrowFillColor = hColor;
                    endArrow.ArrowLineWidth = Size;
                    _handlePlottables.Add(endArrow);
                }
            }
        }
        public override bool HitTest(CxPoint2D plotPos)
        {
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW;
            float hw = HandlePixelSize * WorldPerPixel();
            float hT2 = hw * hw * 4f;
            for (int fi = 0; fi < Fields.Length; fi++)
            {
                var field = Fields[fi];
                var pts = field.Axis.Points;
                if (pts == null || pts.Length < 2) continue;
                var band = ComputeBandVertices(pts, field.Width / 2f, field.Axis.IsClosed);
                if (band != null && band.Length >= 2)
                {
                    int npts2 = pts.Length;
                    for (int e = 0; e < npts2 - 1; e++)
                    {
                        VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(band[e], band[e + 1]), out float d2);
                        if (d2 * d2 <= t2) { _activeIndex = fi; return true; }
                    }
                    for (int e = npts2; e < 2 * npts2 - 1; e++)
                    {
                        VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(band[e], band[e + 1]), out float d2);
                        if (d2 * d2 <= t2) { _activeIndex = fi; return true; }
                    }
                }
                bool hitVtx = false;
                for (int j = 0; j < pts.Length; j++)
                {
                    float dx = plotPos.X - pts[j].X;
                    float dy = plotPos.Y - pts[j].Y;
                    if (dx * dx + dy * dy <= t2 * 4f) { hitVtx = true; break; }
                }
                if (hitVtx) { _activeIndex = fi; return true; }
                if (band != null && band.Length >= 3 && VisionOperator.IsPointInPolygon2D(plotPos, new CxPolygon2D(band, true)))
                { _activeIndex = fi; return true; }
                for (int j = 0; j < pts.Length - 1; j++)
                {
                    VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(pts[j], pts[j + 1]), out float d2);
                    if (d2 * d2 <= t2) { _activeIndex = fi; return true; }
                }
                if (field.Axis.IsClosed)
                {
                    int last = pts.Length - 1;
                    VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(pts[last], pts[0]), out float d2);
                    if (d2 * d2 <= t2) { _activeIndex = fi; return true; }
                }
            }
            return false;
        }
        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }
            var field = Fields[_activeIndex];
            var pts = field.Axis.Points;
            if (pts == null || pts.Length == 0) { UpdatePlottable(); return; }
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW * 4f;
            _dragMode = DragMode.Translate;
            var band = ComputeBandVertices(pts, field.Width / 2f, field.Axis.IsClosed);
            if (band != null && band.Length >= 2)
            {
                int npts2 = pts.Length;
                for (int e = 0; e < npts2 - 1; e++)
                {
                    VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(band[e], band[e + 1]), out float d2);
                    if (d2 * d2 <= t2 * 4f)
                    { _dragMode = DragMode.DragWidth; break; }
                }
                if (_dragMode != DragMode.DragWidth)
                {
                    for (int e = npts2; e < 2 * npts2 - 1; e++)
                    {
                        VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(band[e], band[e + 1]), out float d2);
                        if (d2 * d2 <= t2 * 4f)
                        { _dragMode = DragMode.DragWidth; break; }
                    }
                }
            }
            if (_dragMode == DragMode.DragWidth) { UpdatePlottable(); return; }
            for (int i = 0; i < pts.Length; i++)
            {
                float dx = plotPos.X - pts[i].X;
                float dy = plotPos.Y - pts[i].Y;
                if (dx * dx + dy * dy <= t2)
                {
                    _vertexIndex = i;
                    _dragMode = DragMode.DragVertex;
                    break;
                }
            }
            UpdatePlottable();
        }
        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeIndex < 0) return;
            var field = Fields[_activeIndex];
            switch (_dragMode)
            {
                case DragMode.DragVertex:
                {
                    var pts = Fields[_activeIndex].Axis.Points;
                    if (pts != null && _vertexIndex >= 0 && _vertexIndex < pts.Length)
                    {
                        pts[_vertexIndex] = plotPos;
                        UpdatePlottable();
                        RaiseOnChanged();
                    }
                    break;
                }
                case DragMode.DragWidth:
                {
                    var pts = field.Axis.Points;
                    float minDist2 = float.MaxValue;
                    for (int i = 0; i < pts.Length - 1; i++)
                    {
                        VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(pts[i], pts[i + 1]), out float d2);
                        if (d2 * d2 < minDist2) minDist2 = d2 * d2;
                    }
                    if (field.Axis.IsClosed)
                    {
                        int last = pts.Length - 1;
                        VisionOperator.DistancePointToSegment2D(plotPos, new CxSegment2D(pts[last], pts[0]), out float d2);
                        float d2Sq = d2 * d2;
                        if (d2Sq < minDist2) minDist2 = d2Sq;
                    }
                    float newW = Math.Max(1f, 2f * (float)Math.Sqrt(minDist2));
                    Fields[_activeIndex] = new CxPolygon2DFittingField(field.Axis, newW);
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
                var pts = Fields[_activeIndex].Axis.Points;
                if (pts == null) return;
                for (int j = 0; j < pts.Length; j++)
                    pts[j] = new CxPoint2D(pts[j].X + dx, pts[j].Y + dy);
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
        private static CxPoint2D[] ComputeBandVertices(CxPoint2D[] pts, float halfW, bool closed)
        {
            int n = pts.Length;
            if (n < 2) return null;
            const float mitreLimit = 4f;
            var outer = new CxPoint2D[n];
            var inner = new CxPoint2D[n];
            for (int i = 0; i < n; i++)
            {
                CxPoint2D tIn, tOut;
                if (closed)
                {
                    tIn  = new CxPoint2D(pts[i].X - pts[(i - 1 + n) % n].X, pts[i].Y - pts[(i - 1 + n) % n].Y);
                    tOut = new CxPoint2D(pts[(i + 1) % n].X - pts[i].X, pts[(i + 1) % n].Y - pts[i].Y);
                }
                else
                {
                    tIn  = i > 0
                        ? new CxPoint2D(pts[i].X - pts[i - 1].X, pts[i].Y - pts[i - 1].Y)
                        : new CxPoint2D(pts[1].X - pts[0].X, pts[1].Y - pts[0].Y);
                    tOut = i < n - 1
                        ? new CxPoint2D(pts[i + 1].X - pts[i].X, pts[i + 1].Y - pts[i].Y)
                        : new CxPoint2D(pts[n - 1].X - pts[n - 2].X, pts[n - 1].Y - pts[n - 2].Y);
                }
                float inLen  = (float)Math.Sqrt(tIn.X  * tIn.X  + tIn.Y  * tIn.Y);
                float outLen = (float)Math.Sqrt(tOut.X * tOut.X + tOut.Y * tOut.Y);
                if (inLen  > 0) { tIn  = new CxPoint2D(tIn.X  / inLen,  tIn.Y  / inLen); }
                else { tIn  = new CxPoint2D(1, 0); }
                if (outLen > 0) { tOut = new CxPoint2D(tOut.X / outLen, tOut.Y / outLen); }
                else { tOut = new CxPoint2D(1, 0); }
                float dot = tIn.X * tOut.X + tIn.Y * tOut.Y;
                if (dot > 1f) dot = 1f; else if (dot < -1f) dot = -1f;
                float cosHalf = (float)Math.Sqrt((1f + dot) / 2f);
                if (cosHalf < 1e-4f) cosHalf = 1e-4f;
                float mitreLen = halfW / cosHalf;
                if (mitreLen > mitreLimit * halfW) mitreLen = mitreLimit * halfW;
                float perpInX  = -tIn.Y,  perpInY  = tIn.X;
                float perpOutX = -tOut.Y, perpOutY = tOut.X;
                float bx = perpInX + perpOutX;
                float by = perpInY + perpOutY;
                float bLen = (float)Math.Sqrt(bx * bx + by * by);
                if (bLen > 0) { bx /= bLen; by /= bLen; }
                else { bx = perpInX; by = perpInY; }
                outer[i] = new CxPoint2D(pts[i].X + bx * mitreLen, pts[i].Y + by * mitreLen);
                inner[i] = new CxPoint2D(pts[i].X - bx * mitreLen, pts[i].Y - by * mitreLen);
            }
            int total = 2 * n;
            var band = new CxPoint2D[total];
            for (int i = 0; i < n; i++) band[i] = outer[i];
            for (int i = 0; i < n; i++) band[n + i] = inner[n - 1 - i];
            return band;
        }
    }
}
