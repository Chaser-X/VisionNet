using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    public class CxArc2DItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, DragStart, DragSweep, DragRadius }

        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly List<IPlottable> _controlPoints = new List<IPlottable>();
        private Plot _plot;

        private DragMode _dragMode;
        private int _activeIndex = -1;
        private bool _dragFrozen;
        private float _dragInitialSide;

        public CxArc2D[] Arcs { get; private set; }

        public CxArc2DItem(CxArc2D[] arcs, Color color, float size = 1f)
        {
            Arcs = arcs ?? Array.Empty<CxArc2D>();
            Color = color;
            Size = size;
            if (Arcs.Length > 0)
                HitThreshold = Math.Max(1f, Arcs[0].Radius * 0.05f);
        }

        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            BuildPlottables();
        }

        public override void RemoveFromPlot(Plot plot)
        {
            foreach (var p in _plottables) plot.PlottableList.Remove(p);
            foreach (var c in _controlPoints) plot.PlottableList.Remove(c);
            _plottables.Clear();
            _controlPoints.Clear();
            _plot = null;
        }

        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var p in _plottables) _plot.PlottableList.Remove(p);
            foreach (var c in _controlPoints) _plot.PlottableList.Remove(c);
            _plottables.Clear();
            _controlPoints.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            for (int i = 0; i < Arcs.Length; i++)
            {
                var arc = Arcs[i];
                var color = i == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

                var a = _plot.Add.Arc(arc.Center.X, arc.Center.Y, arc.Radius,
                    Angle.FromDegrees(-arc.StartAngle), Angle.FromDegrees(-arc.SweepAngle));
                a.LineStyle.Color = color;
                a.LineStyle.Width = Size;
                _plottables.Add(a);
            }

            if (_activeIndex >= 0)
            {
                var arc = Arcs[_activeIndex];
                float cpRadius = Math.Max(3f, Size + 1f);
                var spColor = ToSPColor(Color.Lime);

                var sp = GetPointOnArc(arc, arc.StartAngle);
                var cp = _plot.Add.Circle(sp.X, sp.Y, cpRadius);
                cp.FillStyle.Color = spColor;
                cp.FillStyle.IsVisible = true;
                cp.LineStyle.Color = spColor;
                cp.LineStyle.Width = 1;
                _controlPoints.Add(cp);

                var ep = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                cp = _plot.Add.Circle(ep.X, ep.Y, cpRadius);
                cp.FillStyle.Color = spColor;
                cp.FillStyle.IsVisible = true;
                cp.LineStyle.Color = spColor;
                cp.LineStyle.Width = 1;
                _controlPoints.Add(cp);

                float midAngle = arc.StartAngle + arc.SweepAngle * 0.5f;
                var mp = GetPointOnArc(arc, midAngle);
                float midRad = midAngle * (float)Math.PI / 180f;
                float sign = Math.Sign(arc.SweepAngle);
                if (sign == 0f) sign = 1f;
                float tx = -(float)Math.Sin(midRad) * sign;
                float ty = (float)Math.Cos(midRad) * sign;
                float arrowLen = Math.Max(10f, cpRadius * 3f);
                var arrowBase = new CxPoint2D(mp.X - tx * (arrowLen / 2f), mp.Y - ty * (arrowLen / 2f));
                var arrowTip  = new CxPoint2D(mp.X + tx * (arrowLen / 2f), mp.Y + ty * (arrowLen / 2f));
                var arrow = _plot.Add.Arrow(arrowBase.X, arrowBase.Y, arrowTip.X, arrowTip.Y);
                arrow.ArrowLineColor = spColor;
                arrow.ArrowFillColor = spColor;
                arrow.ArrowLineWidth = Size;
                _controlPoints.Add(arrow);
            }
        }

        public override bool HitTest(CxPoint2D plotPos)
        {
            float t2 = HitThreshold * HitThreshold;
            float t2Cp = t2 * 4f;

            for (int i = 0; i < Arcs.Length; i++)
            {
                var arc = Arcs[i];

                var sp = GetPointOnArc(arc, arc.StartAngle);
                float dx = plotPos.X - sp.X, dy = plotPos.Y - sp.Y;
                if (dx * dx + dy * dy <= t2Cp) { _activeIndex = i; return true; }

                var ep = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                dx = plotPos.X - ep.X; dy = plotPos.Y - ep.Y;
                if (dx * dx + dy * dy <= t2Cp) { _activeIndex = i; return true; }

                var mp = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle * 0.5f);
                dx = plotPos.X - mp.X; dy = plotPos.Y - mp.Y;
                if (dx * dx + dy * dy <= t2Cp) { _activeIndex = i; return true; }

                if (DistSqToArc(plotPos, arc) <= t2) { _activeIndex = i; return true; }
            }

            return false;
        }

        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }

            var arc = Arcs[_activeIndex];
            float t2 = HitThreshold * HitThreshold * 4f;

            _dragMode = DragMode.Translate;

            var sp = GetPointOnArc(arc, arc.StartAngle);
            float dx = plotPos.X - sp.X, dy = plotPos.Y - sp.Y;
            if (dx * dx + dy * dy <= t2)
                _dragMode = DragMode.DragStart;
            else
            {
                var ep = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);
                dx = plotPos.X - ep.X; dy = plotPos.Y - ep.Y;
                if (dx * dx + dy * dy <= t2)
                    _dragMode = DragMode.DragSweep;
                else
                {
                    var mp = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle * 0.5f);
                    dx = plotPos.X - mp.X; dy = plotPos.Y - mp.Y;
                if (dx * dx + dy * dy <= t2)
                {
                    _dragMode = DragMode.DragRadius;
                    float cross0 = (ep.X - sp.X) * (plotPos.Y - sp.Y) - (ep.Y - sp.Y) * (plotPos.X - sp.X);
                    _dragInitialSide = Math.Sign(cross0);
                    _dragFrozen = false;
                }
                }
            }

            UpdatePlottable();
        }

        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeIndex < 0) return;

            var arc = Arcs[_activeIndex];

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
                    Arcs[_activeIndex] = new CxArc2D(new CxPoint2D(ux, uy), newR, newStart, newSweep);
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
                    Arcs[_activeIndex] = new CxArc2D(new CxPoint2D(ux, uy), newR, newStart, newSweep);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragRadius:
                {
                    var ps = GetPointOnArc(arc, arc.StartAngle);
                    var pe = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle);

                    float crossNow = (pe.X - ps.X) * (plotPos.Y - ps.Y) - (pe.Y - ps.Y) * (plotPos.X - ps.X);
                    float sideNow = Math.Sign(crossNow);

                    if (_dragFrozen)
                    {
                        if (sideNow == _dragInitialSide && Math.Abs(crossNow) > 1f)
                            _dragFrozen = false;
                        else
                        { UpdatePlottable(); break; }
                    }

                    if (sideNow != 0 && sideNow * _dragInitialSide < 0)
                    { _dragFrozen = true; UpdatePlottable(); break; }

                    if (!CircumCircle(ps.X, ps.Y, pe.X, pe.Y, plotPos.X, plotPos.Y,
                                      out var ux, out var uy, out var newR))
                    { _dragFrozen = true; UpdatePlottable(); break; }

                    float newStart = AngleTo(ps.X, ps.Y, ux, uy);
                    float newEnd   = AngleTo(pe.X, pe.Y, ux, uy);
                    float newSweep = newEnd - newStart;
                    if (arc.SweepAngle > 0 && newSweep < 0) newSweep += 360f;
                    if (arc.SweepAngle < 0 && newSweep > 0) newSweep -= 360f;
                    Arcs[_activeIndex] = new CxArc2D(new CxPoint2D(ux, uy), newR, newStart, newSweep);
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
                var a = Arcs[_activeIndex];
                Arcs[_activeIndex] = new CxArc2D(
                    new CxPoint2D(a.Center.X + dx, a.Center.Y + dy),
                    a.Radius, a.StartAngle, a.SweepAngle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
            {
                foreach (var p in _plottables) _plot.PlottableList.Remove(p);
                foreach (var c in _controlPoints) _plot.PlottableList.Remove(c);
            }
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

        private static CxPoint2D GetPointOnArc(CxArc2D arc, float angleDeg)
        {
            float rad = angleDeg * (float)Math.PI / 180f;
            return new CxPoint2D(
                arc.Center.X + arc.Radius * (float)Math.Cos(rad),
                arc.Center.Y + arc.Radius * (float)Math.Sin(rad));
        }

        private static float DistSqToArc(CxPoint2D p, CxArc2D arc)
        {
            float best = float.MaxValue;
            int numSamples = 32;
            for (int i = 0; i <= numSamples; i++)
            {
                float t = (float)i / numSamples;
                var pt = GetPointOnArc(arc, arc.StartAngle + arc.SweepAngle * t);
                float dx = p.X - pt.X, dy = p.Y - pt.Y;
                float d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            return best;
        }
    }
}
