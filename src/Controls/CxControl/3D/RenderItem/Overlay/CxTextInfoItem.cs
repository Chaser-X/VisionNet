using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxTextInfo"/> values as world-anchored 2D text labels.
    /// Each label is projected from 3D world coordinates to screen space and skipped if
    /// it falls outside the viewport or behind the camera.
    /// </summary>
    public class CxTextInfoItem : AbstractRenderItem
    {
        /// <summary>Gets the text labels to be rendered.</summary>
        public CxTextInfo[] TextInfos { get; private set; }

        /// <summary>Initializes the item with the given text labels, colour, and size.</summary>
        /// <param name="textInfos">World-anchored text labels.</param>
        /// <param name="color">Text colour.</param>
        /// <param name="size">Font size scale used by the GLSL text overlay renderer.</param>
        public CxTextInfoItem(CxTextInfo[] textInfos, Color color, float size = 1.0f) : base(color, size)
        {
            TextInfos = textInfos;
        }

        /// <inheritdoc/>
        public override bool SupportsGroupTransform => true;

        /// <inheritdoc/>
        public override void Draw(CxGlContext gl)
        {
            if (TextInfos == null || TextInfos.Length == 0) return;

            PushModelMatrix(gl);
            var renderer = ModernGLRenderer.For(gl);
            foreach (var info in TextInfos)
            {
                float sx, sy, sz;
                if (!renderer.Project(gl, info.Location.X, info.Location.Y, info.Location.Z,
                    out sx, out sy, out sz)) continue;
                if (sx < 0 || sx > gl.Width || sy < 0 || sy > gl.Height)
                    continue;
                renderer.DrawText(gl, (int)sx, (int)sy, DrawColor, info.Size, info.Text);
            }
            PopModelMatrix(gl);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                TextInfos = null;
            base.Dispose(disposing);
        }
    }
}
