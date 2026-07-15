using SharpGL;
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
        public override void Draw(OpenGL gl)
        {
            if (Box3Ds == null || Box3Ds.Length == 0) return;

            foreach (var box in Box3Ds)
            {
                float hx = box.Size.Width  / 2;
                float hy = box.Size.Height / 2;
                float hz = box.Size.Depth  / 2;
                float cx = box.Center.X, cy = box.Center.Y, cz = box.Center.Z;

                // --- Semi-transparent filled faces ---
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0, Color.A / 255.0);
                gl.Begin(OpenGL.GL_QUADS);
                // Front (+Z)
                gl.Vertex(cx - hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                // Back (−Z)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.Vertex(cx + hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz - hz);
                // Left (−X)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                gl.Vertex(cx - hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy + hy, cz - hz);
                // Right (+X)
                gl.Vertex(cx + hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                // Top (+Y)
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                // Bottom (−Y)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.End();

                // --- Opaque wireframe edges ---
                gl.Color(DrawColor.R / 255.0, DrawColor.G / 255.0, DrawColor.B / 255.0, 1.0);
                gl.LineWidth(Size);
                gl.Begin(OpenGL.GL_LINES);
                // Front face edges
                gl.Vertex(cx - hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                gl.Vertex(cx - hx, cy + hy, cz + hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                // Back face edges
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.Vertex(cx + hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                gl.Vertex(cx + hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz - hz);
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz - hz);
                // Left pillar edges (−X)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx - hx, cy - hy, cz + hz);
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx - hx, cy + hy, cz + hz);
                // Right pillar edges (+X)
                gl.Vertex(cx + hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.Vertex(cx + hx, cy + hy, cz - hz); gl.Vertex(cx + hx, cy + hy, cz + hz);
                // Top horizontal edges (+Y)
                gl.Vertex(cx - hx, cy + hy, cz - hz); gl.Vertex(cx + hx, cy + hy, cz - hz);
                gl.Vertex(cx - hx, cy + hy, cz + hz); gl.Vertex(cx + hx, cy + hy, cz + hz);
                // Bottom horizontal edges (−Y)
                gl.Vertex(cx - hx, cy - hy, cz - hz); gl.Vertex(cx + hx, cy - hy, cz - hz);
                gl.Vertex(cx - hx, cy - hy, cz + hz); gl.Vertex(cx + hx, cy - hy, cz + hz);
                gl.End();
            }

            // Vertex markers: show the 8 corners of the hit box when active and selected.
            if (IsActiveObj && IsSelected && _hitBoxIndex >= 0 && _hitBoxIndex < Box3Ds.Length)
            {
                gl.PointSize(10f);
                gl.Begin(OpenGL.GL_POINTS);
                gl.Color(DrawColor.R / 255.0, DrawColor.G / 255.0, DrawColor.B / 255.0);
                foreach (var v in GetVertices(Box3Ds[_hitBoxIndex]))
                    gl.Vertex(v.X, v.Y, v.Z);
                gl.End();
                gl.PointSize(1f);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Box3Ds = null;
            base.Dispose(disposing);
        }
    }
}
