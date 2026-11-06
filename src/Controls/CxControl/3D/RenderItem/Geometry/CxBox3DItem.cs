using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxBox3D"/> values as semi-transparent filled faces
    /// with an opaque wireframe outline.
    /// </summary>
    public class CxBox3DItem : AbstractRenderItem
    {
        /// <summary>Gets the bounding boxes to be rendered.</summary>
        public CxBox3D[] Box3Ds { get; private set; }

        /// <summary>Initializes the item with the given boxes, colour, and wireframe line width.</summary>
        /// <param name="box3Ds">Boxes to render. Must not be <c>null</c> or empty.</param>
        /// <param name="color">Fill and wireframe colour.</param>
        /// <param name="size">Wireframe line width in pixels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="box3Ds"/> is null or empty.</exception>
        public CxBox3DItem(CxBox3D[] box3Ds, Color color, float size = 1f) : base(color, size)
        {
            if (box3Ds == null || box3Ds.Length == 0)
                throw new ArgumentNullException(nameof(box3Ds));
            Box3Ds = box3Ds;
        }

        // ── Interaction state ────────────────────────────────────────────────────
        private int _hitBoxIndex    = -1;
        private int _hitVertexIndex = -1;

        private static readonly int[] OppositeVertex = { 6, 7, 4, 5, 2, 3, 0, 1 };
        private static readonly int[] TriangleVertexIndices =
        {
            2,3,0, 2,0,1, 6,4,7, 6,5,4,
            6,2,5, 5,1,2, 7,0,3, 7,4,0,
            6,3,2, 6,7,3, 4,5,1, 4,1,0
        };
        private static readonly int[] EdgeVertexIndices =
        {
            0,1, 1,2, 2,3, 3,0,
            4,5, 5,6, 6,7, 7,4,
            0,4, 1,5, 2,6, 3,7
        };
        private readonly float[] _vertexScratch = new float[24];
        private float[] _trianglesBuffer = new float[0];
        private float[] _edgesBuffer = new float[0];
        private float[] _selectedBuffer = new float[24];

        /// <inheritdoc/>
        public override bool HitTest(CxPoint3D worldPos) => FindHit(worldPos).box >= 0;

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint3D worldPos)
        {
            base.OnMouseDown(worldPos);
            (_hitBoxIndex, _hitVertexIndex) = FindHit(worldPos);
        }

        /// <inheritdoc/>
        public override void OnMouseMove(CxPoint3D worldPos, CxPoint3D prevWorldPos)
        {
            if (_hitBoxIndex < 0) return;
            double dx = worldPos.X - prevWorldPos.X;
            double dy = worldPos.Y - prevWorldPos.Y;
            double dz = worldPos.Z - prevWorldPos.Z;
            if (_hitVertexIndex < 0)
            {
                TranslateBox(_hitBoxIndex, dx, dy, dz);
                RaiseOnChanged();
            }
            else
            {
                ResizeBox(_hitBoxIndex, _hitVertexIndex, worldPos, prevWorldPos);
                RaiseOnChanged();
            }
        }

        /// <inheritdoc/>
        public override void OnMouseUp()
        {
            _hitBoxIndex    = -1;
            _hitVertexIndex = -1;
        }

        /// <inheritdoc/>
        public override void Translate(double dx, double dy, double dz)
        {
            for (int i = 0; i < Box3Ds.Length; i++)
                TranslateBox(i, dx, dy, dz);
        }

        private void TranslateBox(int idx, double dx, double dy, double dz)
        {
            Box3Ds[idx] = new CxBox3D(
                new CxPoint3D((float)(Box3Ds[idx].Center.X + dx),
                              (float)(Box3Ds[idx].Center.Y + dy),
                              (float)(Box3Ds[idx].Center.Z + dz)),
                Box3Ds[idx].Size);
        }

        private (int box, int vertex) FindHit(CxPoint3D worldPos)
        {
            float t2 = HitThreshold * HitThreshold;
            for (int b = 0; b < Box3Ds.Length; b++)
            {
                var verts = GetVertices(Box3Ds[b]);
                for (int v = 0; v < 8; v++)
                {
                    float dx = verts[v].X - worldPos.X, dy = verts[v].Y - worldPos.Y, dz = verts[v].Z - worldPos.Z;
                    if (dx * dx + dy * dy + dz * dz <= t2) return (b, v);
                }
                var box = Box3Ds[b];
                if (Math.Abs(worldPos.X - box.Center.X) <= box.Size.Width  / 2 + HitThreshold &&
                    Math.Abs(worldPos.Y - box.Center.Y) <= box.Size.Height / 2 + HitThreshold &&
                    Math.Abs(worldPos.Z - box.Center.Z) <= box.Size.Depth  / 2 + HitThreshold)
                    return (b, -1);
            }
            return (-1, -1);
        }

        private void ResizeBox(int boxIdx, int vertexIdx, CxPoint3D cur, CxPoint3D prev)
        {
            var verts    = GetVertices(Box3Ds[boxIdx]);
            var dragged  = verts[vertexIdx];
            var opposite = verts[OppositeVertex[vertexIdx]];

            float nx = dragged.X + (float)(cur.X - prev.X);
            float ny = dragged.Y + (float)(cur.Y - prev.Y);
            float nz = dragged.Z + (float)(cur.Z - prev.Z);

            Box3Ds[boxIdx] = new CxBox3D(
                new CxPoint3D((nx + opposite.X) / 2, (ny + opposite.Y) / 2, (nz + opposite.Z) / 2),
                new CxSize3D(Math.Max(0.01f, Math.Abs(nx - opposite.X)),
                             Math.Max(0.01f, Math.Abs(ny - opposite.Y)),
                             Math.Max(0.01f, Math.Abs(nz - opposite.Z))));
        }

        private static CxPoint3D[] GetVertices(CxBox3D box)
        {
            float hx = box.Size.Width / 2, hy = box.Size.Height / 2, hz = box.Size.Depth / 2;
            float cx = box.Center.X,       cy = box.Center.Y,        cz = box.Center.Z;
            return new[]
            {
                new CxPoint3D(cx + hx, cy + hy, cz + hz), new CxPoint3D(cx - hx, cy + hy, cz + hz),
                new CxPoint3D(cx - hx, cy - hy, cz + hz), new CxPoint3D(cx + hx, cy - hy, cz + hz),
                new CxPoint3D(cx + hx, cy + hy, cz - hz), new CxPoint3D(cx - hx, cy + hy, cz - hz),
                new CxPoint3D(cx - hx, cy - hy, cz - hz), new CxPoint3D(cx + hx, cy - hy, cz - hz),
            };
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public override bool SupportsGroupTransform => true;

        /// <inheritdoc/>
        public override void Draw(CxGlContext gl)
        {
            if (Box3Ds == null || Box3Ds.Length == 0) return;

            PushModelMatrix(gl);
            var renderer = ModernGLRenderer.For(gl);
            int triangleLength = Box3Ds.Length * TriangleVertexIndices.Length * 3;
            int edgeLength = Box3Ds.Length * EdgeVertexIndices.Length * 3;
            if (_trianglesBuffer.Length != triangleLength)
                _trianglesBuffer = new float[triangleLength];
            if (_edgesBuffer.Length != edgeLength)
                _edgesBuffer = new float[edgeLength];
            int triangleOffset = 0;
            int edgeOffset = 0;
            foreach (var box in Box3Ds)
            {
                FillVertexScratch(box);
                for (int i = 0; i < TriangleVertexIndices.Length; i++)
                {
                    int source = TriangleVertexIndices[i] * 3;
                    _trianglesBuffer[triangleOffset++] = _vertexScratch[source];
                    _trianglesBuffer[triangleOffset++] = _vertexScratch[source + 1];
                    _trianglesBuffer[triangleOffset++] = _vertexScratch[source + 2];
                }
                for (int i = 0; i < EdgeVertexIndices.Length; i++)
                {
                    int source = EdgeVertexIndices[i] * 3;
                    _edgesBuffer[edgeOffset++] = _vertexScratch[source];
                    _edgesBuffer[edgeOffset++] = _vertexScratch[source + 1];
                    _edgesBuffer[edgeOffset++] = _vertexScratch[source + 2];
                }
            }
            renderer.Draw(gl, PrimitiveType.Triangles, _trianglesBuffer,
                Color.FromArgb(Color.A, Color.R, Color.G, Color.B));
            // The wireframe outline stays opaque even when the fill is translucent.
            Color opaque = Color.FromArgb(255, DrawColor.R, DrawColor.G, DrawColor.B);
            renderer.Draw(gl, PrimitiveType.Lines, _edgesBuffer, opaque, Size);

            // Vertex markers: show the 8 corners of the hit box when active and selected.
            if (IsActiveObj && IsSelected && _hitBoxIndex >= 0 && _hitBoxIndex < Box3Ds.Length)
            {
                FillVertexScratch(Box3Ds[_hitBoxIndex]);
                Array.Copy(_vertexScratch, _selectedBuffer, _vertexScratch.Length);
                renderer.Draw(gl, PrimitiveType.Points, _selectedBuffer, opaque, 10f);
            }
            PopModelMatrix(gl);
        }

        private void FillVertexScratch(CxBox3D box)
        {
            float hx = box.Size.Width / 2f, hy = box.Size.Height / 2f, hz = box.Size.Depth / 2f;
            float cx = box.Center.X, cy = box.Center.Y, cz = box.Center.Z;
            WriteVertex(0, cx + hx, cy + hy, cz + hz);
            WriteVertex(1, cx - hx, cy + hy, cz + hz);
            WriteVertex(2, cx - hx, cy - hy, cz + hz);
            WriteVertex(3, cx + hx, cy - hy, cz + hz);
            WriteVertex(4, cx + hx, cy + hy, cz - hz);
            WriteVertex(5, cx - hx, cy + hy, cz - hz);
            WriteVertex(6, cx - hx, cy - hy, cz - hz);
            WriteVertex(7, cx + hx, cy - hy, cz - hz);
        }

        private void WriteVertex(int index, float x, float y, float z)
        {
            int offset = index * 3;
            _vertexScratch[offset] = x;
            _vertexScratch[offset + 1] = y;
            _vertexScratch[offset + 2] = z;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Box3Ds = null;
                _trianglesBuffer = null;
                _edgesBuffer = null;
                _selectedBuffer = null;
            }
            base.Dispose(disposing);
        }
    }
}
