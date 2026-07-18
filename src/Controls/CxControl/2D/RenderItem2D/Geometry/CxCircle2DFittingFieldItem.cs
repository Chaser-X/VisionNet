using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    public class CxCircle2DFittingFieldItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, DragCenter, DragRadius, DragWidth }

        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly List<IPlottable> _handlePlottables = new List<IPlottable>();

        private const float HandlePixelSize = 8f;

        private DragMode _dragMode;
        private int _activeIndex = -1;

        public CxCircle2DFittingField[] Fields { get; private set; }

        public CxCircle2DFittingFieldItem(CxCircle2DFittingField[] fields, Color color, float size = 1f)
        {
            Fields = fields ?? Array.Empty<CxCircle2DFittingField>();
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
                var circle = field.Axis;
                var fColor = fi == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

                float outerR = circle.Radius + field.Width / 2f;
                float innerR = Math.Max(1f, circle.Radius - field.Width / 2f);

                var roi = _plot.Add.Circle(circle.Center.X, circle.Center.Y, outerR);
                roi.LineStyle.Color = fColor;
                roi.LineStyle.Width = Size;
                roi.FillStyle.Color = fColor.WithAlpha(0.25f);
                roi.FillStyle.IsVisible = true;
                roi.InnerRadiusX = innerR;
                roi.InnerRadiusY = innerR;
                _plottables.Add(roi);

                var axis = _plot.Add.Circle(circle.Center.X, circle.Center.Y, circle.Radius);
                axis.LineStyle.Color = fColor;
                axis.LineStyle.Width = Size;
                axis.FillStyle.IsVisible = false;
                _plottables.Add(axis);
            }

            if (_activeIndex >= 0)
            {
                var field = Fields[_activeIndex];
                var circle = field.Axis;
                var hColor = ToSPColor(Color.Lime);

                var cm = _plot.Add.Marker(circle.Center.X, circle.Center.Y);
                cm.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                cm.MarkerStyle.Size = HandlePixelSize;
                cm.MarkerStyle.FillColor = hColor;
                cm.MarkerStyle.LineColor = hColor;
                cm.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(cm);

                float eastX = circle.Center.X + circle.Radius;
                var rm = _plot.Add.Marker(eastX, circle.Center.Y);
                rm.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                rm.MarkerStyle.Size = HandlePixelSize;
                rm.MarkerStyle.FillColor = hColor;
                rm.MarkerStyle.LineColor = hColor;
                rm.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(rm);

                float outerX = circle.Center.X + circle.Radius + field.Width / 2f;
                var om = _plot.Add.Marker(outerX, circle.Center.Y);
                om.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledSquare;
                om.MarkerStyle.Size = HandlePixelSize;
                om.MarkerStyle.FillColor = hColor;
                om.MarkerStyle.LineColor = hColor;
                om.MarkerStyle.LineWidth = 1;
                _handlePlottables.Add(om);

                float innerX = circle.Center.X + circle.Radius - field.Width / 2f;
                var im = _plot.Add.Marker(innerX, circle.Center.Y);
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
            float hw = HandlePixelSize * WorldPerPixel();
            float hT2 = hw * hw * 4f;

            for (int fi = 0; fi < Fields.Length; fi++)
            {
                var field = Fields[fi];
                var circle = field.Axis;

                // Center marker hit
                float cdx = plotPos.X - circle.Center.X;
                float cdy = plotPos.Y - circle.Center.Y;
                if (cdx * cdx + cdy * cdy <= hT2) { _activeIndex = fi; return true; }

                // East point markers (radius + width)
                float eastX = circle.Center.X + circle.Radius;
                float edx = plotPos.X - eastX;
                float edy = plotPos.Y - circle.Center.Y;
                if (edx * edx + edy * edy <= hT2) { _activeIndex = fi; return true; }

                float outerX = circle.Center.X + circle.Radius + field.Width / 2f;
                float odx = plotPos.X - outerX;
                float ody = plotPos.Y - circle.Center.Y;
                if (odx * odx + ody * ody <= hT2) { _activeIndex = fi; return true; }

                float innerX = circle.Center.X + circle.Radius - field.Width / 2f;
                float idx = plotPos.X - innerX;
                float idy = plotPos.Y - circle.Center.Y;
                if (idx * idx + idy * idy <= hT2) { _activeIndex = fi; return true; }

                float dist = (float)Math.Sqrt(cdx * cdx + cdy * cdy);
                float outerR = circle.Radius + field.Width / 2f;
                float innerR = Math.Max(1f, circle.Radius - field.Width / 2f);

                if (dist >= innerR && dist <= outerR)
                { _activeIndex = fi; return true; }

                if (Math.Abs(dist - circle.Radius) <= hitW)
                { _activeIndex = fi; return true; }
            }

            return false;
        }

        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }

            var field = Fields[_activeIndex];
            var circle = field.Axis;
            float hitW = HitThreshold * WorldPerPixel();
            float t2 = hitW * hitW * 4f;
            float hw = HandlePixelSize * WorldPerPixel();
            float hT2 = hw * hw * 4f;

            float cdx = plotPos.X - circle.Center.X;
            float cdy = plotPos.Y - circle.Center.Y;

            if (cdx * cdx + cdy * cdy <= hT2)
            { _dragMode = DragMode.DragCenter; UpdatePlottable(); return; }

            float eastX = circle.Center.X + circle.Radius;
            float edx = plotPos.X - eastX;
            float edy = plotPos.Y - circle.Center.Y;
            if (edx * edx + edy * edy <= hT2)
            { _dragMode = DragMode.DragRadius; UpdatePlottable(); return; }

            float outerX = circle.Center.X + circle.Radius + field.Width / 2f;
            float odx = plotPos.X - outerX;
            float ody = plotPos.Y - circle.Center.Y;
            if (odx * odx + ody * ody <= hT2)
            { _dragMode = DragMode.DragWidth; UpdatePlottable(); return; }

            float innerX = circle.Center.X + circle.Radius - field.Width / 2f;
            float idx = plotPos.X - innerX;
            float idy = plotPos.Y - circle.Center.Y;
            if (idx * idx + idy * idy <= hT2)
            { _dragMode = DragMode.DragWidth; UpdatePlottable(); return; }

            _dragMode = DragMode.Translate;
            UpdatePlottable();
        }

        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_activeIndex < 0) return;

            var field = Fields[_activeIndex];
            var circle = field.Axis;

            switch (_dragMode)
            {
                case DragMode.DragCenter:
                {
                    Fields[_activeIndex] = new CxCircle2DFittingField(
                        new CxCircle2D(plotPos, circle.Radius), field.Width);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragRadius:
                {
                    float dx = plotPos.X - circle.Center.X;
                    float dy = plotPos.Y - circle.Center.Y;
                    float newR = Math.Max(1f, (float)Math.Sqrt(dx * dx + dy * dy));
                    Fields[_activeIndex] = new CxCircle2DFittingField(
                        new CxCircle2D(circle.Center, newR), field.Width);
                    UpdatePlottable(); RaiseOnChanged();
                    break;
                }
                case DragMode.DragWidth:
                {
                    float dx = plotPos.X - circle.Center.X;
                    float dy = plotPos.Y - circle.Center.Y;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float newW = Math.Max(1f, 2f * Math.Abs(d - circle.Radius));
                    Fields[_activeIndex] = new CxCircle2DFittingField(circle, newW);
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
                var c = f.Axis;
                Fields[_activeIndex] = new CxCircle2DFittingField(
                    new CxCircle2D(new CxPoint2D(c.Center.X + dx, c.Center.Y + dy), c.Radius), f.Width);
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
    }
}
