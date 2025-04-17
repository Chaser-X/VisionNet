using SharpGL;
using SharpGL.SceneGraph.Quadrics;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxPoint3DItem : RenderAbstractItem
    {
        public CxPoint3D[] Point3Ds { get;private set; }
        public CxPoint3DItem(CxPoint3D[] points,Color color,float size):base(color, size)
        {
            this.Point3Ds = points;
        }

        public override void Draw(OpenGL gl)
        {
            if (Point3Ds == null || Point3Ds.Length == 0) return;

            gl.PointSize(Size);
            gl.Begin(OpenGL.GL_POINTS);
            foreach (var point in Point3Ds)
            {
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0); // …Ë÷√—’…´
                gl.Vertex(point.X, point.Y, point.Z);
            }
            gl.End();
        }
    }
}

