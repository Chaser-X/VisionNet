using SharpGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxBox3D"/> values as semi-transparent filled faces
    /// with an opaque wireframe outline.
    /// </summary>
    public class CxBox3DItem : AbstractRenderItem
    {
        /// <summary>Gets the bounding boxes to be rendered.</summary>
        public CxBox3D[] Box3Ds { get; private set; }

        /// <summary>Initializes the item with the given boxes, colour, and wireframe line width.</summary>
        /// <param name="box3Ds">Boxes to render. Must not be <c>null</c> or empty.</param>
        /// <param name="color">Fill and wireframe colour.</param>
        /// <param name="size">Wireframe line width in pixels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="box3Ds"/> is null or empty.</exception>
        public CxBox3DItem(CxBox3D[] box3Ds, Color color, float size = 1f) : base(color, size)
        {
            if (box3Ds == null || box3Ds.Length == 0)
                throw new ArgumentNullException(nameof(box3Ds));
            Box3Ds = box3Ds;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Box3Ds == null || Box3Ds.Length == 0) return;

            foreach (var box in Box3Ds)
            {
                float hx = box.Size.Width  / 2;
                float hy = box.Size.Height / 2;
                float hz = box.Size.Depth  / 2;
                float cx = box.Center.X, cy = box.Center.Y, cz = box.Center.Z;

                // --- Semi-transparent filled faces ---
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0, Color.A / 255.0);
                gl.Begin(OpenGL.GL_QUADS);
                // Front (+Z)
                gl.Vertex(cx - hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                // Back (−Z)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.Vertex(cx + hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz - hz);
                // Left (−X)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                gl.Vertex(cx - hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy + hy, cz - hz);
                // Right (+X)
                gl.Vertex(cx + hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                // Top (+Y)
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                // Bottom (−Y)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.End();

                // --- Opaque wireframe edges ---
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0, 1.0);
                gl.LineWidth(Size);
                gl.Begin(OpenGL.GL_LINES);
                // Front face edges
                gl.Vertex(cx - hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                gl.Vertex(cx - hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                // Back face edges
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.Vertex(cx + hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                gl.Vertex(cx + hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz - hz);
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz - hz);
                // Left pillar edges (−X)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                // Right pillar edges (+X)
                gl.Vertex(cx + hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz - hz); gl.Vertex(cx + hx, cy + hy, cz + hz);
                // Top horizontal edges (+Y)
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                gl.Vertex(cx - hx, cy + hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz + hz);
                // Bottom horizontal edges (−Y)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.Vertex(cx - hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.End();
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Box3Ds = null;
            base.Dispose(disposing);
        }
    }
}
