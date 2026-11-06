using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxPolygon3D"/> values as smooth polylines or closed loops,
    /// depending on <see cref="CxPolygon3D.IsClosed"/>.
    /// </summary>
    public class CxPolygon3DItem : AbstractRenderItem
    {
        /// <summary>Gets the polygons to be rendered.</summary>
        public CxPolygon3D[] Polygon3Ds { get; private set; }

        /// <summary>Initializes the item with the given polygons, colour, and line width.</summary>
        /// <param name="polygons">World-space polygons.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels.</param>
        public CxPolygon3DItem(CxPolygon3D[] polygons, Color color, float size) : base(color, size)
        {
            Polygon3Ds = polygons;
        }

        // ── Interaction state ────────────────────────────────────────────────────
        private int _hitPolyIndex   = -1;
        private int _hitVertexIndex = -1;  // -1 = edge/body, ≥0 = vertex index
        private float[][] _polygonBuffers = new float[0][];
        private float[] _markerBuffer = new float[0];

        /// <inheritdoc/>
        public override bool HitTest(CxPoint3D worldPos) => FindHit(worldPos).poly >= 0;

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint3D worldPos)
        {
            base.OnMouseDown(worldPos);
            (_hitPolyIndex, _hitVertexIndex) = FindHit(worldPos);
        }

        /// <inheritdoc/>
        public override void OnMouseMove(CxPoint3D worldPos, CxPoint3D prevWorldPos)
        {
            if (_hitPolyIndex < 0) return;
            double dx = worldPos.X - prevWorldPos.X;
            double dy = worldPos.Y - prevWorldPos.Y;
            double dz = worldPos.Z - prevWorldPos.Z;
            if (_hitVertexIndex < 0)
                TranslatePoly(_hitPolyIndex, dx, dy, dz);
            else
                MoveVertex(_hitPolyIndex, _hitVertexIndex, dx, dy, dz);
            RaiseOnChanged();
        }

        /// <inheritdoc/>
        public override void OnMouseUp()
        {
            _hitPolyIndex   = -1;
            _hitVertexIndex = -1;
        }

        /// <inheritdoc/>
        public override void Translate(double dx, double dy, double dz)
        {
            if (Polygon3Ds == null) return;
            for (int i = 0; i < Polygon3Ds.Length; i++)
                TranslatePoly(i, dx, dy, dz);
        }

        private void TranslatePoly(int idx, double dx, double dy, double dz)
        {
            var pts = Polygon3Ds[idx].Points;
            if (pts == null) return;
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new CxPoint3D((float)(pts[i].X + dx),
                                       (float)(pts[i].Y + dy),
                                       (float)(pts[i].Z + dz));
        }

        private void MoveVertex(int polyIdx, int vertexIdx, double dx, double dy, double dz)
        {
            var pts = Polygon3Ds[polyIdx].Points;
            pts[vertexIdx] = new CxPoint3D((float)(pts[vertexIdx].X + dx),
                                           (float)(pts[vertexIdx].Y + dy),
                                           (float)(pts[vertexIdx].Z + dz));
        }

        private (int poly, int vertex) FindHit(CxPoint3D worldPos)
        {
            if (Polygon3Ds == null) return (-1, -1);
            float t2 = HitThreshold * HitThreshold;
            for (int p = 0; p < Polygon3Ds.Length; p++)
            {
                var pts = Polygon3Ds[p].Points;
                if (pts == null || pts.Length == 0) continue;
                for (int v = 0; v < pts.Length; v++)
                {
                    float dx = pts[v].X - worldPos.X, dy = pts[v].Y - worldPos.Y, dz = pts[v].Z - worldPos.Z;
                    if (dx * dx + dy * dy + dz * dz <= t2) return (p, v);
                }
                int edgeCount = Polygon3Ds[p].IsClosed ? pts.Length : pts.Length - 1;
                for (int e = 0; e < edgeCount; e++)
                {
                    if (PointToSegDistSq(worldPos, pts[e], pts[(e + 1) % pts.Length]) <= t2)
                        return (p, -1);
                }
            }
            return (-1, -1);
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public override bool SupportsGroupTransform => true;

        /// <inheritdoc/>
        public override void Draw(CxGlContext gl)
        {
            if (Polygon3Ds == null || Polygon3Ds.Length == 0) return;

            PushModelMatrix(gl);
            var renderer = ModernGLRenderer.For(gl);
            if (_polygonBuffers.Length != Polygon3Ds.Length)
                _polygonBuffers = new float[Polygon3Ds.Length][];
            for (int polygonIndex = 0; polygonIndex < Polygon3Ds.Length; polygonIndex++)
            {
                var polygon = Polygon3Ds[polygonIndex];
                if (polygon.Points == null || polygon.Points.Length == 0) continue;
                EnsureExactCapacity(ref _polygonBuffers[polygonIndex], polygon.Points.Length * 3);
                var positions = _polygonBuffers[polygonIndex];
                for (int i = 0; i < polygon.Points.Length; i++)
                {
                    positions[i * 3] = polygon.Points[i].X;
                    positions[i * 3 + 1] = polygon.Points[i].Y;
                    positions[i * 3 + 2] = polygon.Points[i].Z;
                }
                renderer.Draw(polygon.IsClosed ? PrimitiveType.LineLoop : PrimitiveType.LineStrip,
                    positions, DrawColor, Size);
            }

            // Vertex markers on the hit polygon when active and selected.
            if (IsActiveObj && IsSelected && _hitPolyIndex >= 0 && _hitPolyIndex < Polygon3Ds.Length)
            {
                var pts = Polygon3Ds[_hitPolyIndex].Points;
                if (pts != null && pts.Length > 0)
                {
                    EnsureExactCapacity(ref _markerBuffer, pts.Length * 3);
                    var positions = _markerBuffer;
                    for (int i = 0; i < pts.Length; i++)
                    {
                        positions[i * 3] = pts[i].X;
                        positions[i * 3 + 1] = pts[i].Y;
                        positions[i * 3 + 2] = pts[i].Z;
                    }
                    renderer.Draw(PrimitiveType.Points, positions, DrawColor, 10f);
                }
            }
            PopModelMatrix(gl);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Polygon3Ds = null;
                _polygonBuffers = null;
                _markerBuffer = null;
            }
            base.Dispose(disposing);
        }
    }
}
