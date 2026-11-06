using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Specifies the visual shape used when rendering a set of 3D points.</summary>
    public enum PointShape
    {
        /// <summary>Render each point as an OpenGL point primitive (single pixel or point-size square).</summary>
        Point,

        /// <summary>Render each point as an instanced sphere mesh.</summary>
        Sphere,
    }

    /// <summary>
    /// Renders an array of <see cref="CxPoint3D"/> values as either flat GL_POINTS or
    /// instanced sphere meshes, depending on <see cref="Shape"/>.
    /// </summary>
    public class CxPoint3DItem : AbstractRenderItem
    {
        /// <summary>Gets the point positions to be rendered.</summary>
        public CxPoint3D[] Point3Ds { get; private set; }

        /// <summary>Gets or sets the visual shape (point or sphere).</summary>
        public PointShape Shape { get; set; } = PointShape.Point;

        // Reused by the flat-point path. Point data is updated in place each
        // frame, avoiding a large managed allocation while the view is dragged.
        private float[] _positionsBuffer = new float[0];

        /// <summary>Initializes the item with the given points, colour, size, and shape.</summary>
        /// <param name="points">World-space point positions.</param>
        /// <param name="color">Drawing colour.</param>
        /// <param name="size">Point size in pixels, or sphere radius when <paramref name="shape"/> is <see cref="PointShape.Sphere"/>.</param>
        /// <param name="shape">Visual shape to use for each point.</param>
        public CxPoint3DItem(CxPoint3D[] points, Color color, float size = 1.0f,
            PointShape shape = PointShape.Point) : base(color, size)
        {
            Point3Ds = points;
            Shape    = shape;
        }

        /// <inheritdoc/>
        public override bool SupportsGroupTransform => true;

        /// <inheritdoc/>
        public override void Draw(CxGlContext gl)
        {
            if (Point3Ds == null || Point3Ds.Length == 0) return;

            var positions = FillPositionBuffer();
            var renderer = ModernGLRenderer.For(gl);
            if (Shape == PointShape.Point)
            {
                PushModelMatrix(gl);
                renderer.Draw(PrimitiveType.Points, positions, DrawColor, Size);
                PopModelMatrix(gl);
            }
            else
            {
                // Preserve the historical Size / 2 sphere radius while reusing the context-level
                // mesh and shader. Only the instance-position buffer is streamed per frame.
                // The sphere path applies ModelMatrix through its own uniform, so it must not
                // also push a model matrix onto the shared stack.
                renderer.DrawSpheres(positions, DrawColor, Size / 2f, ModelMatrix);
            }
        }

        /// <summary>Materialises <see cref="Point3Ds"/> into the reused interleaved position buffer.</summary>
        private float[] FillPositionBuffer()
        {
            EnsureExactCapacity(ref _positionsBuffer, Point3Ds.Length * 3);
            var positions = _positionsBuffer;
            for (int i = 0; i < Point3Ds.Length; i++)
            {
                positions[i * 3] = Point3Ds[i].X;
                positions[i * 3 + 1] = Point3Ds[i].Y;
                positions[i * 3 + 2] = Point3Ds[i].Z;
            }
            return positions;
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint3D worldPos)
        {
            if (Point3Ds == null) return false;
            float t2 = HitThreshold * HitThreshold;
            foreach (var p in Point3Ds)
            {
                float dx = p.X - worldPos.X, dy = p.Y - worldPos.Y, dz = p.Z - worldPos.Z;
                if (dx * dx + dy * dy + dz * dz <= t2) return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override void Translate(double dx, double dy, double dz)
        {
            if (Point3Ds == null) return;
            for (int i = 0; i < Point3Ds.Length; i++)
                Point3Ds[i] = new CxPoint3D(
                    (float)(Point3Ds[i].X + dx),
                    (float)(Point3Ds[i].Y + dy),
                    (float)(Point3Ds[i].Z + dz));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Point3Ds = null;
                _positionsBuffer = null;
            }
            base.Dispose(disposing);
        }
    }
}
