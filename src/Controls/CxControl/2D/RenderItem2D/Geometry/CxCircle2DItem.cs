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
    /// Renders an array of <see cref="CxCircle2D"/> values as circles with optional fill.
    /// Each circle is a separate <see cref="Ellipse"/> plottable.
    /// For circles to appear as true circles, use <see cref="CxDisplay2D.SetAspectLock"/>.
    ///
    /// Drag interaction:
    /// <list type="bullet">
    ///   <item>Click near the circumference → drag to resize radius.</item>
    ///   <item>Click inside the circle (away from edge) → drag to translate.</item>
    /// </list>
    /// Only the circle that was hit by <see cref="HitTest"/> is drawn in <see cref="Abstract2DRenderItem.SelectedColor"/>;
    /// all others use <see cref="Abstract2DRenderItem.Color"/>.
    /// </summary>
    public class CxCircle2DItem : Abstract2DRenderItem
    {
        private enum DragMode { None, Translate, Resize }

        private readonly List<Ellipse> _plottables = new List<Ellipse>();
        private Plot _plot;

        private DragMode  _dragMode;
        private int       _activeCircleIndex = -1;
        private CxPoint2D _dragCenter;

        /// <summary>Gets the circle data being rendered.</summary>
        public CxCircle2D[] Circles { get; private set; }

        /// <summary>Gets or sets whether circles are filled with their colour.</summary>
        public bool Filled { get; set; } = false;

        /// <summary>Initializes the item with the given circles, colour, line width, and optional fill.</summary>
        public CxCircle2DItem(CxCircle2D[] circles, Color color, float size = 1f, bool filled = false)
        {
            Circles = circles ?? Array.Empty<CxCircle2D>();
            Color   = color;
            Size    = size;
            Filled  = filled;
            if (Circles.Length > 0)
                HitThreshold = Math.Max(1f, Circles[0].Radius * 0.05f);
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
            foreach (var e in _plottables) plot.PlottableList.Remove(e);
            _plottables.Clear();
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var e in _plottables) _plot.PlottableList.Remove(e);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            for (int i = 0; i < Circles.Length; i++)
            {
                var circle = Circles[i];
                var color = i == _activeCircleIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);
                var ellipse = _plot.Add.Circle(circle.Center.X, circle.Center.Y, circle.Radius);
                ellipse.LineStyle.Width     = Size;
                ellipse.LineStyle.Color     = color;
                ellipse.FillStyle.Color     = color;
                ellipse.FillStyle.IsVisible = Filled;
                _plottables.Add(ellipse);
            }
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint2D plotPos)
        {
            for (int i = 0; i < Circles.Length; i++)
            {
                var c = Circles[i];
                float dx = plotPos.X - c.Center.X;
                float dy = plotPos.Y - c.Center.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                bool hit = Filled
                    ? dist <= c.Radius + HitThreshold
                    : Math.Abs(dist - c.Radius) <= HitThreshold;
                if (!hit) continue;
                _activeCircleIndex = i;
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeCircleIndex >= 0)
            {
                var c = Circles[_activeCircleIndex];
                float dx = plotPos.X - c.Center.X;
                float dy = plotPos.Y - c.Center.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                _dragCenter = c.Center;
                _dragMode = Math.Abs(dist - c.Radius) <= HitThreshold * 2f
                    ? DragMode.Resize : DragMode.Translate;
            }
            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            if (_dragMode == DragMode.Resize && _activeCircleIndex >= 0)
            {
                float dx = plotPos.X - _dragCenter.X;
                float dy = plotPos.Y - _dragCenter.Y;
                float newR = (float)Math.Sqrt(dx * dx + dy * dy);
                if (newR < 1f) newR = 1f;
                Circles[_activeCircleIndex] = new CxCircle2D(Circles[_activeCircleIndex].Center, newR);
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
            _activeCircleIndex = -1;
            _dragMode = DragMode.None;
            UpdatePlottable();
        }

        /// <inheritdoc/>
        public override void Translate(float dx, float dy)
        {
            if (_activeCircleIndex >= 0)
            {
                var c = Circles[_activeCircleIndex];
                Circles[_activeCircleIndex] = new CxCircle2D(
                    new CxPoint2D(c.Center.X + dx, c.Center.Y + dy), c.Radius);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
                foreach (var e in _plottables) _plot.PlottableList.Remove(e);
        }
    }
}
