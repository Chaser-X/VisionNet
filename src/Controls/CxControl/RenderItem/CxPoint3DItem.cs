using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxPoint3DItem : RenderAbstractItem
    {
        public Dictionary<CxPoint3D, Color> PointColors { get; set; } = new Dictionary<CxPoint3D, Color>();
        public CxPoint3DItem(Dictionary<CxPoint3D, Color> pointColors)
        {
            this.PointColors = pointColors;
        }

        public override void Draw(OpenGL gl)
        {
            if (PointColors.Count == 0) return;

            gl.PointSize(LineWidth);
            gl.Begin(OpenGL.GL_POINTS);
            foreach (var kvp in PointColors)
            {
                var point = kvp.Key;
                var color = kvp.Value;

                gl.Color(color.R / 255.0, color.G / 255.0, color.B / 255.0); // …Ë÷√—’…´
                gl.Vertex(point.X, point.Y, point.Z);
            }
            gl.End();
        }
    }
}

