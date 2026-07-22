using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    public class CxArc2DFittingFieldItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, DragStart, DragSweep, DragRadius, DragWidth }

        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly List<IPlottable> _handlePlottables = new List<IPlottable>();

        private const float HandlePixelSize = 8f;
        private const float ArrowPixelLen = 16f;

        private DragMode _dragMode;
        private int _activeIndex = -1;
        private bool _dragFrozen;
        private float _dragInitialSide;

        public CxArc2DFittingField[] Fields { get; private set; }

        public CxArc2DFittingFieldItem(CxArc2DFittingField[] fields, Color color, float size = 1f)
        {
            Fields = fields ?? Array.Empty<CxArc2DFittingField>();
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
                var arc = field.Axis;
                var fColor = fi == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

                float outerR = arc.Radius + field.Width / 2f;
                float innerR = Math.Max(1f, arc.Radius - field.Width / 2f);

                var roi = _plot.Add.Arc(arc.Center.X, arc.Center.Y, outerR,
                    Angle.FromDegrees(-arc.StartAngle), Angle.FromDegrees(-arc.SweepAngle));
                roi.LineStyle.Color = fColor;
                roi.LineStyle.Width = Size;
                roi.FillStyle.Color = fColor.WithAlpha(0.25f);
                roi.FillStyle.IsVisible = true;
                roi.InnerRadiusX = innerR;
                roi.InnerRadiusY = innerR;
                _plottables.Add(roi);

                var axis = _plot.Add.Arc(arc.Center.X, arc.Center.Y, arc.Radius,
                    Angle.FromDegrees(-arc.StartAngle), Angle.FromDegrees(-arc.SweepAngle));
                axis.LineStyle.Color = fColor;
                axis.LineStyle.Width = Size;
                axis.FillStyle.IsVisible = false;
                _plottables.Add(axis);
            }

            if (_activeIndex >= 0)
            {
                var field = Fields[_activeIndex];
                var arc = field.Axis;
                var hColor = ToSPColor(Color.Lime);
                float wp = WorldPerPixel();

                var sp = GetPointOnArc(arc, arc.StartAngle);
                var sm = _plot.Add.Marker(sp.X, sp.Y);
                sm.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                sm.MarkerStyle.Size = HandlePixelSize;
                sm.MarkerStyle.FillColor = hColor;
                sm.MarkerStyle.LineColor = hColor;
                sm.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(sm);

                var ep = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                var em = _plot.Add.Marker(ep.X, ep.Y);
                em.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                em.MarkerStyle.Size = HandlePixelSize;
                em.MarkerStyle.FillColor = hColor;
                em.MarkerStyle.LineColor = hColor;
                em.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(em);

                float endRad = (arc.StartAngle + arc.SweepAngle) * (float)Math.PI / 180f;
                float sign = Math.Sign(arc.SweepAngle);
                if (sign == 0f) sign = 1f;
                float etx = -(float)Math.Sin(endRad) * sign;
                float ety = (float)Math.Cos(endRad) * sign;
                float eArrowLen = ArrowPixelLen * wp;
                var endArrow = _plot.Add.Arrow(
                    ep.X - etx * eArrowLen, ep.Y - ety * eArrowLen,
                    ep.X, ep.Y);
                endArrow.ArrowLineColor = hColor;
                endArrow.ArrowFillColor = hColor;
                endArrow.ArrowLineWidth = Size;
                _handlePlottables.Add(endArrow);

                float midAngle = arc.StartAngle + arc.SweepAngle * 0.5f;
                var mp = GetPointOnArc(arc, midAngle);
                var mm = _plot.Add.Marker(mp.X, mp.Y);
                mm.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                mm.MarkerStyle.Size = HandlePixelSize;
                mm.MarkerStyle.FillColor = hColor;
                mm.MarkerStyle.LineColor = hColor;
                mm.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(mm);

                float midRad = midAngle * (float)Math.PI / 180f;
                float halfW = field.Width / 2f;
                float cosM = (float)Math.Cos(midRad);
                float sinM = (float)Math.Sin(midRad);

                var ow = new CxPoint2D(
                    arc.Center.X + (arc.Radius + halfW) * cosM,
                    arc.Center.Y + (arc.Radius + halfW) * sinM);
                var om = _plot.Add.Marker(ow.X, ow.Y);
                om.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledSquare;
                om.MarkerStyle.Size = HandlePixelSize;
                om.MarkerStyle.FillColor = hColor;
                om.MarkerStyle.LineColor = hColor;
                om.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(om);

                var iw = new CxPoint2D(
                    arc.Center.X + (arc.Radius - halfW) * cosM,
                    arc.Center.Y + (arc.Radius - halfW) * sinM);
                var im = _plot.Add.Marker(iw.X, iw.Y);
                im.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledSquare;
                im.MarkerStyle.Size = HandlePixelSize;
                im.MarkerStyle.FillColor = hColor;
                im.MarkerStyle.LineColor = hColor;
                im.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(im);
            }
        }

        public override bool HitTest(CxPoint2D plotPos)
        {
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW;
            float t2Cp = t2 * 4f;
            float hw = HandlePixelSize * WorldPerPixel();
            float hT2 = hw * hw * 4f;

            for (int fi = 0; fi < Fields.Length; fi++)
            {
                var field = Fields[fi];
                var arc = field.Axis;

                var (w1, w2) = GetWidthHandlePos(field);
                float wdx = plotPos.X - w1.X; float wdy = plotPos.Y - w1.Y;
                if (wdx * wdx + wdy * wdy <= hT2) { _activeIndex = fi; return true; }
                wdx = plotPos.X - w2.X; wdy = plotPos.Y - w2.Y;
                if (wdx * wdx + wdy * wdy <= hT2) { _activeIndex = fi; return true; }

                var mp = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle * 0.5f);
                float mdx = plotPos.X - mp.X; float mdy = plotPos.Y - mp.Y;
                if (mdx * mdx + mdy * mdy <= t2Cp) { _activeIndex = fi; return true; }

                var sp = GetPointOnArc(arc, arc.StartAngle);
                float dx = plotPos.X - sp.X; float dy = plotPos.Y - sp.Y;
                if (dx * dx + dy * dy <= t2Cp) { _activeIndex = fi; return true; }

                var ep = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                dx = plotPos.X - ep.X; dy = plotPos.Y - ep.Y;
                if (dx * dx + dy * dy <= t2Cp) { _activeIndex = fi; return true; }

                if (VisionOperator.IsPointInAnnularSector2D(plotPos, arc, field.Width)) { _activeIndex = fi; return true; }

                VisionOperator.DistancePointToArc2D(plotPos, arc, out float dArc);
                if (dArc * dArc <= t2) { _activeIndex = fi; return true; }
            }

            return false;
        }

        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }

            var field = Fields[_activeIndex];
            var arc = field.Axis;
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW * 4f;
            float hw = HandlePixelSize * WorldPerPixel();
            float hT2 = hw * hw * 4f;

            var (w1, w2) = GetWidthHandlePos(field);
            float wdx = plotPos.X - w1.X; float wdy = plotPos.Y - w1.Y;
            if (wdx * wdx + wdy * wdy <= hT2)
            { _dragMode = DragMode.DragWidth; UpdatePlottable(); return; }
            wdx = plotPos.X - w2.X; wdy = plotPos.Y - w2.Y;
            if (wdx * wdx + wdy * wdy <= hT2)
            { _dragMode = DragMode.DragWidth; UpdatePlottable(); return; }

            _dragMode = DragMode.Translate;

            var mp = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle * 0.5f);
            float mdx = plotPos.X - mp.X; float mdy = plotPos.Y - mp.Y;
            if (mdx * mdx + mdy * mdy <= t2)
            {
                _dragMode = DragMode.DragRadius;
                var ps = GetPointOnArc(arc, arc.StartAngle);
                var pe = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                float cross0 = (pe.X - ps.X) * (plotPos.Y - ps.Y) - (pe.Y - ps.Y) * (plotPos.X - ps.X);
                _dragInitialSide = Math.Sign(cross0);
                _dragFrozen = false;
                UpdatePlottable(); return;
            }

            var sp = GetPointOnArc(arc, arc.StartAngle);
            float dx = plotPos.X - sp.X; float dy = plotPos.Y - sp.Y;
            if (dx * dx + dy * dy <= t2)
                _dragMode = DragMode.DragStart;
            else
            {
                var ep = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                dx = plotPos.X - ep.X; dy = plotPos.Y - ep.Y;
                if (dx * dx + dy * dy <= t2)
                    _dragMode = DragMode.DragSweep;
            }

            UpdatePlottable();
        }

        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeIndex < 0) return;

            var field = Fields[_activeIndex];
            var arc = field.Axis;

            switch (_dragMode)
            {
                case DragMode.DragStart:
                {
                    var pe = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                    if (!ProjectCenterOntoBisector(plotPos.X, plotPos.Y, pe.X, pe.Y,
                                                   arc.Center.X, arc.Center.Y, out var ux, out var uy))
                    { UpdatePlottable(); break; }

                    float newR = (float)Math.Sqrt((plotPos.X - ux) * (plotPos.X - ux) + (plotPos.Y - uy) * (plotPos.Y - uy));
                    if (newR < 1f) newR = 1f;
                    float newStart = AngleTo(plotPos.X, plotPos.Y, ux, uy);
                    float newEnd   = AngleTo(pe.X, pe.Y, ux, uy);
                    float newSweep = newEnd - newStart;
                    if (arc.SweepAngle > 0 && newSweep < 0) newSweep += 360f;
                    if (arc.SweepAngle < 0 && newSweep > 0) newSweep -= 360f;
                    Fields[_activeIndex] = new CxArc2DFittingField(
                        new CxArc2D(new CxPoint2D(ux, uy), newR, newStart, newSweep), field.Width);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragSweep:
                {
                    var ps = GetPointOnArc(arc, arc.StartAngle);
                    if (!ProjectCenterOntoBisector(ps.X, ps.Y, plotPos.X, plotPos.Y,
                                                   arc.Center.X, arc.Center.Y, out var ux, out var uy))
                    { UpdatePlottable(); break; }

                    float newR = (float)Math.Sqrt((plotPos.X - ux) * (plotPos.X - ux) + (plotPos.Y - uy) * (plotPos.Y - uy));
                    if (newR < 1f) newR = 1f;
                    float newStart = AngleTo(ps.X, ps.Y, ux, uy);
                    float newEnd   = AngleTo(plotPos.X, plotPos.Y, ux, uy);
                    float newSweep = newEnd - newStart;
                    if (arc.SweepAngle > 0 && newSweep < 0) newSweep += 360f;
                    if (arc.SweepAngle < 0 && newSweep > 0) newSweep -= 360f;
                    Fields[_activeIndex] = new CxArc2DFittingField(
                        new CxArc2D(new CxPoint2D(ux, uy), newR, newStart, newSweep), field.Width);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragRadius:
                {
                    var ps = GetPointOnArc(arc, arc.StartAngle);
                    var pe = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);

                    float crossNow = (pe.X - ps.X) * (plotPos.Y - ps.Y) - (pe.Y - ps.Y) * (plotPos.X - ps.X);
                    float sideNow = Math.Sign(crossNow);

                    float chordDx = pe.X - ps.X, chordDy = pe.Y - ps.Y;
                    float chordLen = (float)Math.Sqrt(chordDx * chordDx + chordDy * chordDy);
                    float perpDist = chordLen > 0 ? Math.Abs(crossNow) / chordLen : float.MaxValue;
                    float wp = WorldPerPixel();
                    float freezeDist = 3f * wp;
                    float unfreezeDist = 6f * wp;

                    if (_dragFrozen)
                    {
                        if (sideNow == _dragInitialSide && perpDist > unfreezeDist)
                            _dragFrozen = false;
                        else { UpdatePlottable(); break; }
                    }

                    if ((sideNow != 0 && sideNow * _dragInitialSide < 0) || perpDist < freezeDist)
                    { _dragFrozen = true; UpdatePlottable(); break; }

                    if (!CircumCircle(ps.X, ps.Y, pe.X, pe.Y, plotPos.X, plotPos.Y,
                                      out var ux, out var uy, out var newR))
                    { _dragFrozen = true; UpdatePlottable(); break; }

                    float newStart = AngleTo(ps.X, ps.Y, ux, uy);
                    float newEnd   = AngleTo(pe.X, pe.Y, ux, uy);
                    float newSweep = newEnd - newStart;
                    if (arc.SweepAngle > 0 && newSweep < 0) newSweep += 360f;
                    if (arc.SweepAngle < 0 && newSweep > 0) newSweep -= 360f;
                    Fields[_activeIndex] = new CxArc2DFittingField(
                        new CxArc2D(new CxPoint2D(ux, uy), newR, newStart, newSweep), field.Width);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragWidth:
                {
                    float dx = plotPos.X - arc.Center.X;
                    float dy = plotPos.Y - arc.Center.Y;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float newW = Math.Max(1f, 2f * Math.Abs(d - arc.Radius));
                    Fields[_activeIndex] = new CxArc2DFittingField(arc, newW);
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
            _dragFrozen = false;
            _dragInitialSide = 0;
        }

        public override void OnDeselected()
        {
            _activeIndex = -1;
            _dragMode = DragMode.None;
            _dragFrozen = false;
            _dragInitialSide = 0;
            UpdatePlottable();
        }

        public override void Translate(float dx, float dy)
        {
            if (_activeIndex >= 0)
            {
                var f = Fields[_activeIndex];
                var a = f.Axis;
                Fields[_activeIndex] = new CxArc2DFittingField(
                    new CxArc2D(new CxPoint2D(a.Center.X + dx, a.Center.Y + dy),
                                a.Radius, a.StartAngle, a.SweepAngle), f.Width);
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

        private static (CxPoint2D w1, CxPoint2D w2) GetWidthHandlePos(CxArc2DFittingField field)
        {
            var arc = field.Axis;
            float midAngle = arc.StartAngle + arc.SweepAngle * 0.5f;
            float midRad = midAngle * (float)Math.PI / 180f;
            float halfW = field.Width / 2f;
            float cosM = (float)Math.Cos(midRad);
            float sinM = (float)Math.Sin(midRad);

            return (
                new CxPoint2D(arc.Center.X + (arc.Radius + halfW) * cosM,
                              arc.Center.Y + (arc.Radius + halfW) * sinM),
                new CxPoint2D(arc.Center.X + (arc.Radius - halfW) * cosM,
                              arc.Center.Y + (arc.Radius - halfW) * sinM));
        }

        private static CxPoint2D GetPointOnArc(CxArc2D arc, float angleDeg)
        {
            float rad = angleDeg * (float)Math.PI / 180f;
            return new CxPoint2D(
                arc.Center.X + arc.Radius * (float)Math.Cos(rad),
                arc.Center.Y + arc.Radius * (float)Math.Sin(rad));
        }

        private static bool ProjectCenterOntoBisector(
            float px, float py, float qx, float qy, float ox, float oy,
            out float ux, out float uy)
        {
            float segDx = qx - px, segDy = qy - py;
            float segSq = segDx * segDx + segDy * segDy;
            if (segSq < 1e-3f) { ux = uy = 0f; return false; }
            float mx = (px + qx) * 0.5f, my = (py + qy) * 0.5f;
            float nx = -segDy, ny = segDx;
            float vx = mx - ox, vy = my - oy;
            float vDotN = vx * nx + vy * ny;
            float t = -vDotN / segSq;
            ux = mx + t * nx;
            uy = my + t * ny;
            return true;
        }

        private static bool CircumCircle(
            float ax, float ay, float bx, float by, float cx, float cy,
            out float ux, out float uy, out float r)
        {
            float d = 2f * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-3f) { ux = uy = r = 0f; return false; }
            ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
            uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
            r = (float)Math.Sqrt((ux - ax) * (ux - ax) + (uy - ay) * (uy - ay));
            if (r < 1f) r = 1f;
            return true;
        }

        private static float AngleTo(float px, float py, float cx, float cy)
            => (float)(Math.Atan2(py - cy, px - cx) * 180.0 / Math.PI);
    }
}
