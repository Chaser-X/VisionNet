using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxPlane3DItem : AbstractRenderItem
    {
        public Plane3D[] Planes { get; private set; }
        public CxPlane3DItem(Plane3D[] planes, Color color, float size = 100.0f) : base(color, size)
        {
            this.Planes = planes;
        }

        public override void Draw(OpenGL gl)
        {
            if (Planes == null || Planes.Length == 0) return;

            foreach (var plane in Planes)
            {
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0, Color.A / 255.0);
                CxVector3D u;
                if (Math.Abs(plane.Normal.X) < 1e-6 && Math.Abs(plane.Normal.Y) < 1e-6)
                {
                    u = new CxVector3D(1, 0, 0).Normalize() * Size; // 当Normal为(0,0,z)时，选择一个非零向量
                }
                else
                {
                    u = new CxVector3D(plane.Normal.Y, -plane.Normal.X, 0).Normalize() * Size;
                }
                CxVector3D v = plane.Normal.Cross(u).Normalize() * Size;
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
