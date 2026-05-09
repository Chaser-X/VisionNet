using System;
using System.Drawing;
using SharpGL;

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
    }
}
