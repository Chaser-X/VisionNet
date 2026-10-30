using SharpGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxPlane3D"/> values as filled quadrilaterals centred on
    /// <see cref="CxPlane3D.Point"/>, whose half-extent equals <see cref="AbstractRenderItem.Size"/>.
    /// Two orthogonal tangent vectors are computed from the plane normal to form the quad corners.
    /// </summary>
    public class CxPlane3DItem : AbstractRenderItem
    {
        /// <summary>Gets the planes to be rendered.</summary>
        public CxPlane3D[] Planes { get; private set; }
        private float[] _positionsBuffer = new float[0];

        /// <summary>Initializes the item with the given planes, colour, and half-extent size.</summary>
        /// <param name="planes">Planes to render.</param>
        /// <param name="color">Fill colour (alpha channel is respected).</param>
        /// <param name="size">Half-extent of the rendered quad in world units.</param>
        public CxPlane3DItem(CxPlane3D[] planes, Color color, float size = 100.0f) : base(color, size)
        {
            Planes = planes;
        }

        /// <inheritdoc/>
        public override bool SupportsGroupTransform => true;

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Planes == null || Planes.Length == 0) return;

            PushModelMatrix(gl);
            var renderer = ModernGLRenderer.For(gl);
            int required = Planes.Length * 18;
            if (_positionsBuffer.Length != required)
                _positionsBuffer = new float[required];
            int offset = 0;
            foreach (var plane in Planes)
            {
                // Compute two orthogonal tangent vectors lying in the plane.
                CxVector3D u = (Math.Abs(plane.Normal.X) < 1e-6f && Math.Abs(plane.Normal.Y) < 1e-6f)
                    ? new CxVector3D(1, 0, 0).Normalize() * Size                    // Normal ≈ (0,0,z)
                    : new CxVector3D(plane.Normal.Y, -plane.Normal.X, 0).Normalize() * Size;

                CxVector3D v = plane.Normal.Cross(u).Normalize() * Size;

                float p0x = (float)(plane.Point.X + u.X + v.X), p0y = (float)(plane.Point.Y + u.Y + v.Y), p0z = (float)(plane.Point.Z + u.Z + v.Z);
                float p1x = (float)(plane.Point.X - u.X + v.X), p1y = (float)(plane.Point.Y - u.Y + v.Y), p1z = (float)(plane.Point.Z - u.Z + v.Z);
                float p2x = (float)(plane.Point.X - u.X - v.X), p2y = (float)(plane.Point.Y - u.Y - v.Y), p2z = (float)(plane.Point.Z - u.Z - v.Z);
                float p3x = (float)(plane.Point.X + u.X - v.X), p3y = (float)(plane.Point.Y + u.Y - v.Y), p3z = (float)(plane.Point.Z + u.Z - v.Z);
                WritePoint(ref offset, p0x, p0y, p0z); WritePoint(ref offset, p1x, p1y, p1z); WritePoint(ref offset, p2x, p2y, p2z);
                WritePoint(ref offset, p0x, p0y, p0z); WritePoint(ref offset, p2x, p2y, p2z); WritePoint(ref offset, p3x, p3y, p3z);
            }
            renderer.Draw(gl, OpenGL.GL_TRIANGLES, _positionsBuffer, DrawColor);
            PopModelMatrix(gl);
        }

        private void WritePoint(ref int offset, float x, float y, float z)
        {
            _positionsBuffer[offset++] = x;
            _positionsBuffer[offset++] = y;
            _positionsBuffer[offset++] = z;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Planes = null;
                _positionsBuffer = null;
            }
            base.Dispose(disposing);
        }
    }
}
