using SharpGL;
using SharpGL.SceneGraph;
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
        /// <param name="size">Font size scale (passed to <c>gl.DrawText</c>).</param>
        public CxTextInfoItem(CxTextInfo[] textInfos, Color color, float size = 1.0f) : base(color, size)
        {
            TextInfos = textInfos;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (TextInfos == null || TextInfos.Length == 0) return;

            float r = Color.R / 255.0f;
            float g = Color.G / 255.0f;
            float b = Color.B / 255.0f;

            foreach (var info in TextInfos)
            {
                var objCoord    = new Vertex(info.Location.X, info.Location.Y, info.Location.Z);
                var screenCoord = gl.Project(objCoord);

                // Skip labels that project outside the viewport or behind the camera.
                if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                    screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
                    continue;
                if (screenCoord.Z < 0 || screenCoord.Z > 1)
                    continue;

                gl.DrawText((int)screenCoord.X, (int)screenCoord.Y, r, g, b, "Arial", info.Size, info.Text);
            }
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
