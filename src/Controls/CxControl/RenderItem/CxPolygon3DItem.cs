using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxPolygon3DItem : RenderAbstractItem
    {
        public Dictionary<Polygon3D, Color> PolygonColors { get; set; } = new Dictionary<Polygon3D, Color>();
        public CxPolygon3DItem(Dictionary<Polygon3D, Color> polygonColors)
        {
            this.PolygonColors = polygonColors;
        }

        public override void Draw(OpenGL gl)
        {
            if (PolygonColors.Count == 0) return;

            gl.LineWidth(LineWidth);
            foreach (var polygonItem in PolygonColors)
            {
                var polygon = polygonItem.Key;
                var color = polygonItem.Value;
                gl.Color(color.R / 255.0, color.G / 255.0, color.B / 255.0); // ������ɫ

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


