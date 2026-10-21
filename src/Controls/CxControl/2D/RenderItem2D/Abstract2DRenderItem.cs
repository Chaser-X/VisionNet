using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>
    /// Base class for 2D overlay render items.
    /// Provides default IDisposable, colour, size, and selection/drag support.
    /// Analogous to <c>AbstractRenderItem</c> in the 3D control.
    /// </summary>
    public abstract class Abstract2DRenderItem : I2DRenderItem
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Override to release managed plottables.</summary>
        protected virtual void Dispose(bool disposing) { }

        /// <inheritdoc/>
        public Color Color { get; set; } = Color.White;

        /// <inheritdoc/>
        public float Size { get; set; } = 1.0f;

        /// <summary>Gets or sets the colour used when this item is selected. Default: bright yellow.</summary>
        public Color SelectedColor { get; set; } = Color.FromArgb(255, 255, 50);

        /// <summary>The effective colour: <see cref="SelectedColor"/> when selected, otherwise <see cref="Color"/>.</summary>
        protected Color DrawColor => IsSelected ? SelectedColor : Color;

        /// <summary>The ScottPlot <see cref="Plot"/> this item was added to. Set by <see cref="AddToPlot"/>.</summary>
        protected Plot _plot;

        // ── Active-object interaction ────────────────────────────────────────────

        /// <summary>
        /// Gets or sets whether this item participates in selection and drag interaction.
        /// When <c>false</c> (default), all mouse events are ignored by <see cref="CxDisplay2D"/>.
        /// </summary>
        public bool IsActiveObj { get; set; } = false;

        /// <summary>Gets or sets whether this item is currently selected.</summary>
        public bool IsSelected { get; set; } = false;

        /// <summary>Pixel-space click tolerance for <see cref="HitTest"/>. Default: 6 pixels.</summary>
        public float HitThreshold { get; set; } = 6f;

        /// <summary>
        /// Returns the current world‑unit per pixel ratio for the X axis.
        /// Under 1:1 aspect lock this is equivalent to the Y axis ratio.
        /// Returns 1 if <see cref="_plot"/> is null or not yet rendered.
        /// </summary>
        protected float WorldPerPixel()
        {
            if (_plot == null) return 1f;
            var p0 = _plot.GetPixel(new Coordinates(0, 0));
            var p1 = _plot.GetPixel(new Coordinates(1, 0));
            float dx = (float)(p1.X - p0.X);
            return dx != 0 ? Math.Abs(1f / dx) : 1f;
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="plotPos"/> is within <see cref="HitThreshold"/>
        /// of this item's geometry. Only called when <see cref="IsActiveObj"/> is <c>true</c>.
        /// </summary>
        public virtual bool HitTest(CxPoint2D plotPos) => false;

        /// <summary>Called when this item is hit by a left mouse-down. Default: selects the item.</summary>
        public virtual void OnMouseDown(CxPoint2D plotPos) { IsSelected = true; UpdatePlottable(); }

        /// <summary>
        /// Called while the mouse moves with this item captured.
        /// Default: translates geometry by the delta, then raises <see cref="OnChanged"/>.
        /// </summary>
        public virtual void OnMouseMove(CxPoint2D plotPos, CxPoint2D prevPlotPos)
        {
            Translate(plotPos.X - prevPlotPos.X, plotPos.Y - prevPlotPos.Y);
            UpdatePlottable();
            OnChanged?.Invoke(this);
        }

        /// <summary>Called on mouse-up while this item is captured.</summary>
        public virtual void OnMouseUp() { }

        /// <summary>Called when this item loses selection. Default: clears IsSelected and redraws.</summary>
        public virtual void OnDeselected() { IsSelected = false; UpdatePlottable(); }

        /// <summary>Moves all geometry by the given plot-coordinate delta. No-op by default.</summary>
        public virtual void Translate(float dx, float dy) { }

        /// <summary>
        /// Raised after this item's geometry is modified via <see cref="OnMouseMove"/>.
        /// The argument is the item itself; cast to the concrete type to read updated data.
        /// </summary>
        public event Action<Abstract2DRenderItem> OnChanged;

        /// <summary>Raises <see cref="OnChanged"/>.</summary>
        protected void RaiseOnChanged() => OnChanged?.Invoke(this);

        // ── I2DRenderItem ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public abstract void AddToPlot(Plot plot);

        /// <inheritdoc/>
        public abstract void RemoveFromPlot(Plot plot);

        /// <inheritdoc/>
        public abstract void UpdatePlottable();

        // ── Helper ───────────────────────────────────────────────────────────────

        /// <summary>Converts a System.Drawing.Color to a ScottPlot.Color.</summary>
        protected static ScottPlot.Color ToSPColor(Color c) =>
            new ScottPlot.Color(c.R, c.G, c.B, c.A);

        // ── Direction arrow (head only, fixed pixel size) ────────────────────────

        /// <summary>Arrowhead length in pixels for fitting-field search direction indicators.</summary>
        protected const float DirectionArrowHeadPixels = 14f;

        /// <summary>Arrowhead width in pixels.</summary>
        protected const float DirectionArrowHeadWidthPixels = 12f;

        /// <summary>
        /// Adds a solid triangular arrowhead (no shaft) of fixed pixel size, pointing from
        /// <paramref name="baseCenter"/> along (<paramref name="dirX"/>, <paramref name="dirY"/>).
        /// The base edge midpoint sits on <paramref name="baseCenter"/> (the field centre line).
        /// The head is drawn in pixel space, so its size does not change with zoom and its
        /// orientation is correct for any axis aspect ratio.
        /// </summary>
        protected void AddDirectionArrow(List<IPlottable> list, CxPoint2D baseCenter,
                                         float dirX, float dirY, ScottPlot.Color color)
        {
            float len = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
            if (len <= 0f) return;
            dirX /= len;
            dirY /= len;

            // The probe point only defines the arrow direction; its distance is irrelevant
            // because the head geometry is computed in pixel space from ArrowheadLength/Width.
            float probe = 20f * WorldPerPixel();
            var tip = new CxPoint2D(baseCenter.X + dirX * probe, baseCenter.Y + dirY * probe);

            var arrow = _plot.Add.Arrow(
                new ScottPlot.Coordinates(baseCenter.X, baseCenter.Y),
                new ScottPlot.Coordinates(tip.X, tip.Y));
            arrow.ArrowShape = new DirectionArrowShape();
            arrow.ArrowheadLength = DirectionArrowHeadPixels;
            arrow.ArrowheadWidth = DirectionArrowHeadWidthPixels;
            arrow.ArrowFillColor = color;
            arrow.ArrowLineColor = color;
            arrow.ArrowLineWidth = 1;
            list.Add(arrow);
        }
    }

    /// <summary>
    /// ScottPlot arrow shape that draws only a solid triangular arrowhead. The base edge is
    /// centred on the arrow's base pixel and the head extends toward the tip pixel, sized in
    /// pixels so it is invariant under zoom and correct for non-square axis aspect ratios.
    /// </summary>
    internal sealed class DirectionArrowShape : ScottPlot.IArrowShape
    {
        public void Render(ScottPlot.RenderPack rp, ScottPlot.PixelLine arrowLine, ScottPlot.ArrowStyle arrowStyle)
        {
            float dx = arrowLine.DeltaX, dy = arrowLine.DeltaY;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len <= 0f) return;
            dx /= len;
            dy /= len;

            float perpX = -dy, perpY = dx;
            float headLen = arrowStyle.ArrowheadLength;
            float halfW = arrowStyle.ArrowheadWidth / 2f;

            var b = arrowLine.Pixel1;
            var tip = new ScottPlot.Pixel(b.X + dx * headLen, b.Y + dy * headLen);
            var left = new ScottPlot.Pixel(b.X + perpX * halfW, b.Y + perpY * halfW);
            var right = new ScottPlot.Pixel(b.X - perpX * halfW, b.Y - perpY * halfW);

            ScottPlot.Pixel[] pixels = { tip, left, right, tip };
            ScottPlot.Drawing.FillPath(rp.Canvas, rp.Paint, pixels, arrowStyle.FillStyle);
            ScottPlot.Drawing.DrawPath(rp.Canvas, rp.Paint, pixels, arrowStyle.LineStyle);
        }
    }
}
