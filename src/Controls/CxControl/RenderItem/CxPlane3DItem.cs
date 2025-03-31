using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxPlane3DItem : RenderAbstractItem
    {
        public Dictionary<Plane3D, Color> PlaneColors { get; private set; }
        public float PlaneSize { get; set; }

        public CxPlane3DItem(Dictionary<Plane3D, Color> planeColors, float size = 100.0f)
        {
            this.PlaneColors = planeColors;
            this.PlaneSize = size;
        }

        public override void Draw(OpenGL gl)
        {
          //  gl.Enable(OpenGL.GL_BLEND);
          //  gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            foreach (var planeColor in PlaneColors)
            {
                var plane = planeColor.Key;
                var color = planeColor.Value;

                gl.Color(color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0);
                CxVector3D u;
                if (Math.Abs(plane.Normal.X) < 1e-6 && Math.Abs(plane.Normal.Y) < 1e-6)
                {
                    u = new CxVector3D(1, 0, 0).Normalize() * PlaneSize; // 当Normal为(0,0,z)时，选择一个非零向量
                }
                else
                {
                    u = new CxVector3D(plane.Normal.Y, -plane.Normal.X, 0).Normalize() * PlaneSize;
                }
                CxVector3D v = plane.Normal.Cross(u).Normalize() * PlaneSize;
                gl.Begin(OpenGL.GL_QUADS);
                gl.Vertex(plane.Point.X + u.X + v.X, plane.Point.Y + u.Y + v.Y, plane.Point.Z + u.Z + v.Z);
                gl.Vertex(plane.Point.X - u.X + v.X, plane.Point.Y - u.Y + v.Y, plane.Point.Z - u.Z + v.Z);
                gl.Vertex(plane.Point.X - u.X - v.X, plane.Point.Y - u.Y - v.Y, plane.Point.Z - u.Z - v.Z);
                gl.Vertex(plane.Point.X + u.X - v.X, plane.Point.Y + u.Y - v.Y, plane.Point.Z + u.Z - v.Z);
                gl.End();
            }
           // gl.Disable(OpenGL.GL_BLEND);
        }
    }
}
