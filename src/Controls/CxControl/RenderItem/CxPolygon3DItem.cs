using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxPolygon3DItem : RenderAbstractItem
    {
        public Polygon3D[] Polygon3Ds { get; private set; }
        public CxPolygon3DItem(Polygon3D[] polygons, Color color, float size) : base(color, size)
        {
            Polygon3Ds = polygons;
        }

        public override void Draw(OpenGL gl)
        {
            if (Polygon3Ds == null || Polygon3Ds.Length == 0) return;

            gl.LineWidth(Size);
            foreach (var polygon in Polygon3Ds)
            {
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0); // …Ë÷√—’…´

                gl.Begin(polygon.IsClosed ? OpenGL.GL_LINE_LOOP : OpenGL.GL_LINE_STRIP);
                foreach (var point in polygon.Points)
                {
                    gl.Vertex(point.X, point.Y, point.Z);
                }
                gl.End();
            }
        }
    }
}


