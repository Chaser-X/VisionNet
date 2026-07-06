using SharpGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="Plane3D"/> values as filled quadrilaterals centred on
    /// <see cref="Plane3D.Point"/>, whose half-extent equals <see cref="AbstractRenderItem.Size"/>.
    /// Two orthogonal tangent vectors are computed from the plane normal to form the quad corners.
    /// </summary>
    public class CxPlane3DItem : AbstractRenderItem
    {
        /// <summary>Gets the planes to be rendered.</summary>
        public Plane3D[] Planes { get; private set; }

        /// <summary>Initializes the item with the given planes, colour, and half-extent size.</summary>
        /// <param name="planes">Planes to render.</param>
        /// <param name="color">Fill colour (alpha channel is respected).</param>
        /// <param name="size">Half-extent of the rendered quad in world units.</param>
        public CxPlane3DItem(Plane3D[] planes, Color color, float size = 100.0f) : base(color, size)
        {
            Planes = planes;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Planes == null || Planes.Length == 0) return;

            gl.Color(DrawColor.R / 255.0, DrawColor.G / 255.0, DrawColor.B / 255.0, DrawColor.A / 255.0);

            foreach (var plane in Planes)
            {
                // Compute two orthogonal tangent vectors lying in the plane.
                CxVector3D u = (Math.Abs(plane.Normal.X) < 1e-6f && Math.Abs(plane.Normal.Y) < 1e-6f)
                    ? new CxVector3D(1, 0, 0).Normalize() * Size                    // Normal ≈ (0,0,z)
                    : new CxVector3D(plane.Normal.Y, -plane.Normal.X, 0).Normalize() * Size;

                CxVector3D v = plane.Normal.Cross(u).Normalize() * Size;

                gl.Begin(OpenGL.GL_QUADS);
                gl.Vertex(plane.Point.X + u.X + v.X, plane.Point.Y + u.Y + v.Y, plane.Point.Z + u.Z + v.Z);
                gl.Vertex(plane.Point.X - u.X + v.X, plane.Point.Y - u.Y + v.Y, plane.Point.Z - u.Z + v.Z);
                gl.Vertex(plane.Point.X - u.X - v.X, plane.Point.Y - u.Y - v.Y, plane.Point.Z - u.Z - v.Z);
                gl.Vertex(plane.Point.X + u.X - v.X, plane.Point.Y + u.Y - v.Y, plane.Point.Z + u.Z - v.Z);
                gl.End();
            }
        }
    }
}
