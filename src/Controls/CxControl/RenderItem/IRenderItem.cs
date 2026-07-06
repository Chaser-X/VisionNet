using System;
using System.Drawing;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Minimal interface for simple overlay render items (segments, points, polygons, text, etc.).
    /// Implementations draw directly via the fixed-function OpenGL pipeline.
    /// </summary>
    public interface IRenderItem : IDisposable
    {
        /// <summary>Gets or sets the drawing colour.</summary>
        Color Color { get; set; }

        /// <summary>Gets or sets the point or line size in pixels.</summary>
        float Size { get; set; }

        /// <summary>Issues all OpenGL draw calls for this item.</summary>
        /// <param name="gl">Active OpenGL context.</param>
        void Draw(OpenGL gl);
    }

    /// <summary>
    /// Base class for simple overlay render items.
    /// Provides default implementations of <see cref="IDisposable"/> and common properties.
    /// </summary>
    public abstract class AbstractRenderItem : IRenderItem
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Override to release unmanaged resources.</summary>
        protected virtual void Dispose(bool disposing) { }

        /// <summary>Initializes with white colour and size 1.</summary>
        protected AbstractRenderItem() { }

        /// <summary>Initializes with the specified colour and size.</summary>
        protected AbstractRenderItem(Color color, float size = 1.0f)
        {
            Color = color;
            Size  = size;
        }

        /// <inheritdoc/>
        public float Size { get; set; } = 1.0f;

        /// <inheritdoc/>
        public Color Color { get; set; } = Color.White;

        /// <inheritdoc/>
        public abstract void Draw(OpenGL gl);

        // ── Active-object interaction ─────────────────────────────────────────────

        /// <summary>
        /// Gets or sets whether this item participates in selection and drag interaction.
        /// When <c>false</c> (default), all mouse events are ignored by <see cref="CxDisplay"/>.
        /// </summary>
        public bool IsActiveObj { get; set; } = false;

        /// <summary>Gets or sets whether this item is currently selected.</summary>
        public bool IsSelected { get; set; } = false;

        /// <summary>
        /// World-space proximity threshold used by <see cref="HitTest"/>.
        /// Default is <c>1.0</c> world unit — adjust to match the scene scale.
        /// </summary>
        public float HitThreshold { get; set; } = 1.0f;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="worldPos"/> is within <see cref="HitThreshold"/>
        /// of this item's geometry. Only called when <see cref="IsActiveObj"/> is <c>true</c>.
        /// Override to provide geometry-specific proximity logic. Default: <c>false</c>.
        /// </summary>
        public virtual bool HitTest(CxPoint3D worldPos) => false;

        /// <summary>
        /// Called by <see cref="CxDisplay"/> when this item is hit by a left mouse-down.
        /// Default: sets <see cref="IsSelected"/> to <c>true</c>.
        /// Override for custom selection response.
        /// </summary>
        public virtual void OnMouseDown(CxPoint3D worldPos) { IsSelected = true; }

        /// <summary>
        /// Called by <see cref="CxDisplay"/> while the mouse moves with this item selected.
        /// Default: delegates to <see cref="Translate"/> with the XYZ delta between frames,
        /// then raises <see cref="OnChanged"/>.
        /// Override for custom drag behaviour (e.g., axis-constrained movement, rotation).
        /// </summary>
        public virtual void OnMouseMove(CxPoint3D worldPos, CxPoint3D prevWorldPos)
        {
            Translate(worldPos.X - prevWorldPos.X,
                      worldPos.Y - prevWorldPos.Y,
                      worldPos.Z - prevWorldPos.Z);
            OnChanged?.Invoke(this);
        }

        /// <summary>
        /// Called by <see cref="CxDisplay"/> on mouse-up while this item is selected.
        /// Override to finalise an operation (e.g., snap to grid, raise an event).
        /// </summary>
        public virtual void OnMouseUp() { }

        /// <summary>
        /// Called by <see cref="CxDisplay"/> when this item loses selection.
        /// Default: sets <see cref="IsSelected"/> to <c>false</c>.
        /// </summary>
        public virtual void OnDeselected() { IsSelected = false; }

        /// <summary>
        /// Moves all vertices of this item by the given world-space delta.
        /// Override in subclasses to update vertex data. No-op by default.
        /// </summary>
        public virtual void Translate(double dx, double dy, double dz) { }

        /// <summary>
        /// Raised after this item's geometry is modified via <see cref="OnMouseMove"/>.
        /// The argument is the item itself; cast to the concrete type to read updated data.
        /// </summary>
        public event Action<AbstractRenderItem> OnChanged;
    }
}
