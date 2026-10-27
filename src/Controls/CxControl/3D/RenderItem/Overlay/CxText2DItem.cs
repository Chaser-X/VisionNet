using SharpGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxText2D"/> values as screen-space text overlays
    /// using an orthographic 2D projection. Y coordinates are measured from the top of the window
    /// (matching conventional UI coordinates).
    /// </summary>
    public class CxText2DItem : AbstractRenderItem
    {
        /// <summary>Gets the text items to be rendered.</summary>
        public CxText2D[] TextItems { get; private set; }

        /// <summary>Initializes the item with the given text items, colour, and font size scale.</summary>
        /// <param name="textItems">Text items to render. Must not be <c>null</c> or empty.</param>
        /// <param name="color">Text colour.</param>
        /// <param name="size">Font size scale factor used by the GLSL text overlay renderer.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="textItems"/> is null or empty.</exception>
        public CxText2DItem(CxText2D[] textItems, Color color, float size = 1f) : base(color, size)
        {
            if (textItems == null || textItems.Length == 0)
                throw new ArgumentNullException(nameof(textItems));
            TextItems = textItems;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (TextItems == null || TextItems.Length == 0) return;

            int vpHeight = gl.RenderContextProvider.Height;
            var renderer = ModernOverlayRenderer.For(gl);
            foreach (var item in TextItems)
            {
                // Convert Y from top-origin to bottom-origin (OpenGL convention).
                int screenY = vpHeight - (int)item.Location.Y;
                renderer.DrawText(gl, (int)item.Location.X, screenY, DrawColor, item.FontSize, item.Text);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                TextItems = null;
            base.Dispose(disposing);
        }
    }
}
