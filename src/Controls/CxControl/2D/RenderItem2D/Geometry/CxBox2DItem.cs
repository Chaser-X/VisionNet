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
    /// Renders an array of <see cref="CxBox2D"/> values as axis-aligned rectangles with optional fill.
    /// Each box is a separate <see cref="Polygon"/> plottable.
    /// </summary>
    public class CxBox2DItem : Abstract2DRenderItem
    {
        private readonly List<Polygon> _plottables = new List<Polygon>();
        private Plot _plot;

        /// <summary>Gets the box data being rendered.</summary>
        public CxBox2D[] Boxes { get; private set; }

        /// <summary>Gets or sets whether boxes are filled with their colour.</summary>
        public bool Filled { get; set; } = false;

        /// <summary>Initializes the item with the given boxes, colour, line width, and optional fill.</summary>
        public CxBox2DItem(CxBox2D[] boxes, Color color, float size = 1f, bool filled = false)
        {
            Boxes  = boxes ?? Array.Empty<CxBox2D>();
            Color  = color;
            Size   = size;
            Filled = filled;
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
            foreach (var p in _plottables) plot.PlottableList.Remove(p);
            _plottables.Clear();
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var p in _plottables) _plot.PlottableList.Remove(p);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            var spColor = ToSPColor(DrawColor);
            foreach (var box in Boxes)
            {
                var coords = new Coordinates[4];
                coords[0] = new Coordinates(box.Left,  box.Top);
                coords[1] = new Coordinates(box.Right, box.Top);
                coords[2] = new Coordinates(box.Right, box.Bottom);
                coords[3] = new Coordinates(box.Left,  box.Bottom);
                var polygon = _plot.Add.Polygon(coords);
                polygon.LineStyle.Width       = Size;
                polygon.LineStyle.Color       = spColor;
                polygon.FillStyle.Color       = spColor;
                polygon.FillStyle.IsVisible   = Filled;
                polygon.MarkerStyle.IsVisible = false;
                _plottables.Add(polygon);
            }
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            float t2 = HitThreshold * HitThreshold;
            foreach (var box in Boxes)
            {
                if (Filled)
                {
                    if (plotPos.X >= box.Left  - HitThreshold &&
                        plotPos.X <= box.Right + HitThreshold &&
                        plotPos.Y >= box.Top   - HitThreshold &&
                        plotPos.Y <= box.Bottom + HitThreshold)
                        return true;
                }

                // Edge hit
                var pts = new[]
                {
                    new CxPoint2D(box.Left,  box.Top),
                    new CxPoint2D(box.Right, box.Top),
                    new CxPoint2D(box.Right, box.Bottom),
                    new CxPoint2D(box.Left,  box.Bottom),
                };
                for (int i = 0; i < 4; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % 4];
                    float dx = b.X - a.X;
                    float dy = b.Y - a.Y;
                    float lenSq = dx * dx + dy * dy;
                    if (lenSq == 0f)
                    {
                        float ex = plotPos.X - a.X;
                        float ey = plotPos.Y - a.Y;
                        if (ex * ex + ey * ey <= t2) return true;
                    }
                    else
                    {
                        float t = Math.Max(0, Math.Min(1, ((plotPos.X - a.X) * dx + (plotPos.Y - a.Y) * dy) / lenSq));
                        float cx = a.X + t * dx - plotPos.X;
                        float cy = a.Y + t * dy - plotPos.Y;
                        if (cx * cx + cy * cy <= t2) return true;
                    }
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            for (int i = 0; i < Boxes.Length; i++)
                Boxes[i] = new CxBox2D(
                    new CxPoint2D(Boxes[i].Center.X + dx, Boxes[i].Center.Y + dy),
                    Boxes[i].Size);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var p in _plottables) _plot.PlottableList.Remove(p);
        }
    }
}
