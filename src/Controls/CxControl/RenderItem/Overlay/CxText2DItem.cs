using SharpGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="Text2D"/> values as screen-space text overlays
    /// using an orthographic 2D projection. Y coordinates are measured from the top of the window
    /// (matching conventional UI coordinates).
    /// </summary>
    public class CxText2DItem : AbstractRenderItem
    {
        /// <summary>Gets the text items to be rendered.</summary>
        public Text2D[] TextItems { get; private set; }

        /// <summary>Initializes the item with the given text items, colour, and font size scale.</summary>
        /// <param name="textItems">Text items to render. Must not be <c>null</c> or empty.</param>
        /// <param name="color">Text colour.</param>
        /// <param name="size">Font size scale factor (passed directly to <c>gl.DrawText</c>).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="textItems"/> is null or empty.</exception>
        public CxText2DItem(Text2D[] textItems, Color color, float size = 1f) : base(color, size)
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

            // Switch to 2D orthographic projection.
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, gl.RenderContextProvider.Width, 0, vpHeight, -1, 1);
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            float r = Color.R / 255.0f;
            float g = Color.G / 255.0f;
            float b = Color.B / 255.0f;

            foreach (var item in TextItems)
            {
                // Convert Y from top-origin to bottom-origin (OpenGL convention).
                int screenY = vpHeight - (int)item.Location.Y;
                gl.DrawText((int)item.Location.X, screenY, r, g, b, "Arial", item.FontSize, item.Text);
            }

            // Restore matrices and depth test.
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
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
