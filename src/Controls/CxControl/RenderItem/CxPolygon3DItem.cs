using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxPolygon3DItem : AbstractRenderItem
    {
        public Polygon3D[] Polygon3Ds { get; private set; }
        public CxPolygon3DItem(Polygon3D[] polygons, Color color, float size) : base(color, size)
        {
            Polygon3Ds = polygons;
        }

        public override void Draw(OpenGL gl)
        {
            if (Polygon3Ds == null || Polygon3Ds.Length == 0) return;
            // 设置线宽
            gl.LineWidth(Size);
            gl.Enable(OpenGL.GL_LINE_SMOOTH);
            gl.Hint(OpenGL.GL_LINE_SMOOTH_HINT, OpenGL.GL_NICEST);
            foreach (var polygon in Polygon3Ds)
            {
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0); // 设置颜色

                gl.Begin(polygon.IsClosed ? OpenGL.GL_LINE_LOOP : OpenGL.GL_LINE_STRIP);
                foreach (var point in polygon.Points)
                {
                    gl.Vertex(point.X, point.Y, point.Z);
                }
                gl.End();
            }
            gl.Disable(OpenGL.GL_LINE_SMOOTH);
        }
    }
}


