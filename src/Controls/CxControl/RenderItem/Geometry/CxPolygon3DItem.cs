using SharpGL;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxPolygon3D"/> values as smooth polylines or closed loops,
    /// depending on <see cref="CxPolygon3D.IsClosed"/>.
    /// </summary>
    public class CxPolygon3DItem : AbstractRenderItem
    {
        /// <summary>Gets the polygons to be rendered.</summary>
        public CxPolygon3D[] Polygon3Ds { get; private set; }

        /// <summary>Initializes the item with the given polygons, colour, and line width.</summary>
        /// <param name="polygons">World-space polygons.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels.</param>
        public CxPolygon3DItem(CxPolygon3D[] polygons, Color color, float size) : base(color, size)
        {
            Polygon3Ds = polygons;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Polygon3Ds == null || Polygon3Ds.Length == 0) return;

            gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0);
            gl.LineWidth(Size);
            gl.Enable(OpenGL.GL_LINE_SMOOTH);
            gl.Hint(OpenGL.GL_LINE_SMOOTH_HINT, OpenGL.GL_NICEST);

            foreach (var polygon in Polygon3Ds)
            {
                gl.Begin(polygon.IsClosed ? OpenGL.GL_LINE_LOOP : OpenGL.GL_LINE_STRIP);
                foreach (var point in polygon.Points)
                    gl.Vertex(point.X, point.Y, point.Z);
                gl.End();
            }

            gl.Disable(OpenGL.GL_LINE_SMOOTH);
        }
    }
}
