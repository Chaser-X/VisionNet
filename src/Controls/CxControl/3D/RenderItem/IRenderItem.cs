using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Minimal interface for simple overlay render items (segments, points, polygons, text, etc.).
    /// Implementations draw through the modern GLSL overlay renderer.
    /// </summary>
    public interface IRenderItem : IDisposable
    {
        /// <summary>Gets or sets the drawing colour.</summary>
        Color Color { get; set; }

        /// <summary>Gets or sets the point or line size in pixels.</summary>
        float Size { get; set; }

        /// <summary>Issues all OpenGL draw calls for this item.</summary>
        /// <param name="gl">Active OpenGL context.</param>
        void Draw(CxGlContext gl);
    }

    /// <summary>
    /// Base class for simple overlay render items.
    /// Provides default implementations of <see cref="IDisposable"/> and common properties.
    /// </summary>
    public abstract class AbstractRenderItem : IRenderItem, ICxGroupMember
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
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
        public bool IsDisposed { get; private set; }

        // ── Group pose support ───────────────────────────────────────────────────

        private CxMatrix4X4 _modelMatrix = CxMatrix4X4.Identity();

        /// <summary>
        /// Model matrix applied to this item's geometry before the camera transform.
        /// Only honoured when <see cref="SupportsGroupTransform"/> is <c>true</c>;
        /// world-space geometry items wrap their draw calls with this pose, screen-space
        /// HUD items ignore it. Maintained by <see cref="CxDisplay"/> for group members.
        /// </summary>
        /// <remarks>
        /// Treat the matrix as immutable: the inverse used by the picking path is cached and only
        /// recomputed when this property is assigned. Mutating the assigned matrix's
        /// <see cref="CxMatrix4X4.Data"/> array in place would leave that cache stale — assign a new
        /// matrix instead.
        /// </remarks>
        public CxMatrix4X4 ModelMatrix
        {
            get => _modelMatrix;
            set
            {
                _modelMatrix = value;
                _modelMatrixInverseDirty = true;
            }
        }

        // Lazily computed inverse of ModelMatrix. The picking path transforms the query point
        // into the item's local frame, so without this every mouse move inverts the pose of
        // every overlay item.
        private CxMatrix4X4 _modelMatrixInverse = CxMatrix4X4.Identity();
        private bool _modelMatrixInverseDirty = true;

        /// <summary>
        /// Inverse of <see cref="ModelMatrix"/>, computed on first use after each pose change.
        /// </summary>
        internal CxMatrix4X4 ModelMatrixInverse
        {
            get
            {
                if (_modelMatrixInverseDirty)
                {
                    _modelMatrixInverse = (_modelMatrix ?? CxMatrix4X4.Identity()).Inverse();
                    _modelMatrixInverseDirty = false;
                }
                return _modelMatrixInverse;
            }
        }

        /// <summary>
        /// Gets whether this item honours <see cref="ModelMatrix"/> in its <see cref="Draw"/>.
        /// Default <c>false</c>; world-space geometry items override to <c>true</c>.
        /// </summary>
        public virtual bool SupportsGroupTransform => false;

        /// <summary>
        /// Gets whether a non-identity model matrix is currently applied (and supported).
        /// </summary>
        protected bool HasModelMatrix
        {
            get
            {
                var m = ModelMatrix;
                if (!SupportsGroupTransform || m?.Data == null) return false;
                var d = m.Data;
                return !(d[0] == 1f && d[1] == 0f && d[2] == 0f && d[3] == 0f
                      && d[4] == 0f && d[5] == 1f && d[6] == 0f && d[7] == 0f
                      && d[8] == 0f && d[9] == 0f && d[10] == 1f && d[11] == 0f
                      && d[12] == 0f && d[13] == 0f && d[14] == 0f && d[15] == 1f);
            }
        }

        /// <summary>
        /// Makes the item's model matrix available to the modern overlay renderer.
        /// Must be paired with <see cref="PopModelMatrix"/>.
        /// </summary>
        protected void PushModelMatrix(CxGlContext gl)
        {
            ModernGLRenderer.For(gl).PushModel(
                HasModelMatrix ? ModelMatrix : CxMatrix4X4.Identity());
        }

        /// <summary>Pops a matrix pushed by <see cref="PushModelMatrix"/>.</summary>
        protected void PopModelMatrix(CxGlContext gl)
        {
            ModernGLRenderer.For(gl).PopModel();
        }

        /// <inheritdoc/>
        public float Size { get; set; } = 1.0f;

        /// <inheritdoc/>
        public Color Color { get; set; } = Color.White;

        /// <summary>Gets or sets the colour used to render this item when it is selected. Default: bright yellow.</summary>
        public Color SelectedColor { get; set; } = Color.FromArgb(255, 255, 50);

        /// <summary>The effective rendering colour: <see cref="SelectedColor"/> when <see cref="IsSelected"/>, otherwise <see cref="Color"/>.</summary>
        protected Color DrawColor => IsSelected ? SelectedColor : Color;

        /// <inheritdoc/>
        public abstract void Draw(CxGlContext gl);

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
        public float HitThreshold { get; set; } = 0.2f;

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

        /// <summary>Raises <see cref="OnChanged"/>. Call from subclass overrides of mouse methods.</summary>
        protected void RaiseOnChanged() => OnChanged?.Invoke(this);

        // ── Shared geometry helpers ──────────────────────────────────────────────

        /// <summary>
        /// Sizes a reused position buffer to exactly <paramref name="required"/> floats.
        /// The draw calls derive their vertex count from the array length, so the buffer must be
        /// sized <b>exactly</b>: a grow-only buffer would leave stale trailing vertices that the
        /// next draw would submit.
        /// </summary>
        protected static void EnsureExactCapacity(ref float[] buffer, int required)
        {
            if (buffer == null || buffer.Length != required)
                buffer = new float[required];
        }

        /// <summary>
        /// Squared distance from <paramref name="p"/> to the segment <paramref name="a"/>–<paramref name="b"/>.
        /// Used by the geometry items' hit tests; kept here so the segment and polygon items share
        /// one implementation.
        /// </summary>
        protected static float PointToSegDistSq(CxPoint3D p, CxPoint3D a, CxPoint3D b)
        {
            float abX = b.X - a.X, abY = b.Y - a.Y, abZ = b.Z - a.Z;
            float ab2 = abX * abX + abY * abY + abZ * abZ;
            if (ab2 < 1e-10f)
            {
                float dx = p.X - a.X, dy = p.Y - a.Y, dz = p.Z - a.Z;
                return dx * dx + dy * dy + dz * dz;
            }
            float t = Math.Max(0, Math.Min(1,
                ((p.X - a.X) * abX + (p.Y - a.Y) * abY + (p.Z - a.Z) * abZ) / ab2));
            float cx = a.X + t * abX, cy = a.Y + t * abY, cz = a.Z + t * abZ;
            float ex = p.X - cx, ey = p.Y - cy, ez = p.Z - cz;
            return ex * ex + ey * ey + ez * ez;
        }
    }
}
