using OpenTK.Graphics.OpenGL;
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
        public float? Intensity { get; set; } = null;

        /// <summary>Gets or sets whether the tooltip is rendered.</summary>
        public bool Visible { get; set; } = false;

        /// <summary>Gets or sets the text colour used for coordinate labels.</summary>
        public Color TextColor { get; set; } = Color.White;

        private readonly float[] _backgroundPositions = new float[12];
        private readonly float[] _borderPositions = new float[8];

        /// <summary>
        /// Updates the coordinates and intensity value shown by the tooltip.
        /// </summary>
        /// <param name="point">World-space surface point.</param>
        /// <param name="intensity">Per-point intensity (0–255), or <c>null</c> if unavailable.</param>
        public void SetCoordinates(CxPoint3D point, float? intensity = null)
        {
            Point     = point;
            Intensity = intensity;
        }

        /// <inheritdoc/>
        public override void Draw(CxGlContext gl)
        {
            if (!Visible) return;

            var renderer = ModernGLRenderer.For(gl);
            float sx, sy, sz;
            if (!renderer.Project(gl, Point.X, Point.Y, Point.Z, out sx, out sy, out sz)) return;
            if (sx < 0 || sx > gl.Width || sy < 0 || sy > gl.Height)
                return;

            int rectWidth  = 80;
            int rectHeight = Intensity.HasValue ? 90 : 80;
            int startX = (int)sx;
            int startY = (int)sy - 10;
            _backgroundPositions[0] = startX; _backgroundPositions[1] = startY;
            _backgroundPositions[2] = startX + rectWidth; _backgroundPositions[3] = startY;
            _backgroundPositions[4] = startX + rectWidth; _backgroundPositions[5] = startY - rectHeight;
            _backgroundPositions[6] = startX; _backgroundPositions[7] = startY;
            _backgroundPositions[8] = startX + rectWidth; _backgroundPositions[9] = startY - rectHeight;
            _backgroundPositions[10] = startX; _backgroundPositions[11] = startY - rectHeight;
            renderer.DrawScreenGeometry(gl, PrimitiveType.Triangles, _backgroundPositions, Color.FromArgb(128, 0, 0, 0));
            _borderPositions[0] = startX - 1; _borderPositions[1] = startY + 1;
            _borderPositions[2] = startX + rectWidth + 1; _borderPositions[3] = startY + 1;
            _borderPositions[4] = startX + rectWidth + 1; _borderPositions[5] = startY - rectHeight - 1;
            _borderPositions[6] = startX - 1; _borderPositions[7] = startY - rectHeight - 1;
            renderer.DrawScreenGeometry(gl, PrimitiveType.LineLoop, _borderPositions, Color.White);

            // Coordinate text labels.
            int   tx = startX + 10;
            renderer.DrawText(gl, tx, startY - 20, TextColor, 12, $"X: {Point.X:F3}");
            renderer.DrawText(gl, tx, startY - 40, TextColor, 12, $"Y: {Point.Y:F3}");
            renderer.DrawText(gl, tx, startY - 60, TextColor, 12, $"Z: {Point.Z:F3}");
            if (Intensity.HasValue)
                renderer.DrawText(gl, tx, startY - 80, TextColor, 12, $"I: {Intensity.Value}");
        }
    }
}
