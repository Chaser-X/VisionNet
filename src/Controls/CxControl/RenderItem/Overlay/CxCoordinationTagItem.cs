using SharpGL;
using SharpGL.SceneGraph;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a semi-transparent 2D tooltip that displays the world-space X, Y, Z coordinates
    /// (and optionally intensity) of the surface point nearest to the mouse cursor.
    /// The tooltip is only drawn when <see cref="Visible"/> is <c>true</c>.
    /// </summary>
    public class CxCoordinationTagItem : AbstractRenderItem
    {
        /// <summary>Gets or sets the world-space position shown in the tooltip.</summary>
        public CxPoint3D Point { get; set; } = new CxPoint3D();

        /// <summary>Gets or sets the intensity value shown in the tooltip, or <c>null</c> if unavailable.</summary>
        public byte? Intensity { get; set; } = null;

        /// <summary>Gets or sets whether the tooltip is rendered.</summary>
        public bool Visible { get; set; } = false;

        /// <summary>Gets or sets the text colour used for coordinate labels.</summary>
        public Color TextColor { get; set; } = Color.White;

        /// <summary>
        /// Updates the coordinates and intensity value shown by the tooltip.
        /// </summary>
        /// <param name="point">World-space surface point.</param>
        /// <param name="intensity">Per-point intensity (0–255), or <c>null</c> if unavailable.</param>
        public void SetCoordinates(CxPoint3D point, byte? intensity = null)
        {
            Point     = point;
            Intensity = intensity;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (!Visible) return;

            var objCoord    = new Vertex(Point.X, Point.Y, Point.Z);
            var screenCoord = gl.Project(objCoord);

            // Skip if the point projects outside the viewport or behind the camera.
            if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
                return;
            if (screenCoord.Z < 0 || screenCoord.Z > 1)
                return;

            int rectWidth  = 80;
            int rectHeight = Intensity.HasValue ? 90 : 80;
            int startX     = (int)screenCoord.X;
            int startY     = (int)screenCoord.Y - 10;

            // Switch to 2D orthographic projection.
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height, -1, 1);
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            // Semi-transparent dark background.
            gl.Color(0.0f, 0.0f, 0.0f, 0.5f);
            gl.Begin(OpenGL.GL_QUADS);
            gl.Vertex(startX,             startY);
            gl.Vertex(startX + rectWidth, startY);
            gl.Vertex(startX + rectWidth, startY - rectHeight);
            gl.Vertex(startX,             startY - rectHeight);
            gl.End();

            // White border outline.
            gl.Color(1.0f, 1.0f, 1.0f, 1.0f);
            gl.LineWidth(1.0f);
            gl.Begin(OpenGL.GL_LINE_LOOP);
            gl.Vertex(startX - 1,             startY + 1);
            gl.Vertex(startX + rectWidth + 1, startY + 1);
            gl.Vertex(startX + rectWidth + 1, startY - rectHeight - 1);
            gl.Vertex(startX - 1,             startY - rectHeight - 1);
            gl.End();

            // Coordinate text labels.
            float r = TextColor.R / 255.0f;
            float g = TextColor.G / 255.0f;
            float b = TextColor.B / 255.0f;
            int   tx = startX + 10;
            gl.DrawText(tx, startY - 20, r, g, b, "Helvetica", 12, $"X: {Point.X:F3}");
            gl.DrawText(tx, startY - 40, r, g, b, "Helvetica", 12, $"Y: {Point.Y:F3}");
            gl.DrawText(tx, startY - 60, r, g, b, "Helvetica", 12, $"Z: {Point.Z:F3}");
            if (Intensity.HasValue)
                gl.DrawText(tx, startY - 80, r, g, b, "Helvetica", 12, $"I: {Intensity.Value}");

            // Restore matrices and depth test.
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }
    }
}
