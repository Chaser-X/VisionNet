using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    public class CxCoordination2DItem : Abstract2DRenderItem
    {
        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly float _xLength;
        private readonly float _yLength;

        public CxCoordination2D[] Frames { get; private set; }

        public CxCoordination2DItem(CxCoordination2D[] frames, float xLength, float yLength, float size = 1f)
        {
            Frames = frames ?? Array.Empty<CxCoordination2D>();
            _xLength = xLength;
            _yLength = yLength;
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
            _plottables.Clear();
            _plot = null;
        }

        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var p in _plottables) _plot.PlottableList.Remove(p);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            foreach (var frame in Frames)
            {
                var xColor = ToSPColor(Color.Red);
                var yColor = ToSPColor(Color.Lime);
                float rad = frame.Angle * (float)Math.PI / 180f;
                float cos = (float)Math.Cos(rad);
                float sin = (float)Math.Sin(rad);

                float xTipX = frame.Origin.X + _xLength * cos;
                float xTipY = frame.Origin.Y + _xLength * sin;
                float yTipX = frame.Origin.X - _yLength * sin;
                float yTipY = frame.Origin.Y + _yLength * cos;

                var xArrow = _plot.Add.Arrow(frame.Origin.X, frame.Origin.Y, xTipX, xTipY);
                xArrow.ArrowLineColor = xColor;
                xArrow.ArrowFillColor = xColor;
                xArrow.ArrowLineWidth = Size;
                _plottables.Add(xArrow);

                var yArrow = _plot.Add.Arrow(frame.Origin.X, frame.Origin.Y, yTipX, yTipY);
                yArrow.ArrowLineColor = yColor;
                yArrow.ArrowFillColor = yColor;
                yArrow.ArrowLineWidth = Size;
                _plottables.Add(yArrow);

                var fColor = ToSPColor(Color.White);
                var marker = _plot.Add.Marker(frame.Origin.X, frame.Origin.Y);
                marker.MarkerStyle.Shape = ScottPlot.MarkerShape.FilledCircle;
                marker.MarkerStyle.Size = 6f;
                marker.MarkerStyle.FillColor = fColor;
                marker.MarkerStyle.LineColor = fColor;
                marker.MarkerStyle.LineWidth = 1;
                _plottables.Add(marker);

                var xLabel = _plot.Add.Text("X", xTipX, xTipY);
                xLabel.LabelStyle.ForeColor = xColor;
                xLabel.LabelStyle.FontSize = 12;
                xLabel.LabelStyle.Bold = true;
                _plottables.Add(xLabel);

                var yLabel = _plot.Add.Text("Y", yTipX, yTipY);
                yLabel.LabelStyle.ForeColor = yColor;
                yLabel.LabelStyle.FontSize = 12;
                yLabel.LabelStyle.Bold = true;
                _plottables.Add(yLabel);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var p in _plottables) _plot.PlottableList.Remove(p);
        }
    }
}
