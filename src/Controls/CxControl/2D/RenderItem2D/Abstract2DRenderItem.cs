using System;
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

        // ── Active-object interaction ────────────────────────────────────────────

        /// <summary>
        /// Gets or sets whether this item participates in selection and drag interaction.
        /// When <c>false</c> (default), all mouse events are ignored by <see cref="CxDisplay2D"/>.
        /// </summary>
        public bool IsActiveObj { get; set; } = false;

        /// <summary>Gets or sets whether this item is currently selected.</summary>
        public bool IsSelected { get; set; } = false;

        /// <summary>Plot-coordinate proximity threshold used by <see cref="HitTest"/>.</summary>
        public float HitThreshold { get; set; } = 5f;

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
    }
}
