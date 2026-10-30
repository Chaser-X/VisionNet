using SharpGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxSegment3D"/> values as GL_LINES with a configurable line width.
    /// </summary>
    public class CxSegment3DItem : AbstractRenderItem
    {
        /// <summary>Gets the line segments to be rendered.</summary>
        public CxSegment3D[] Segment3Ds { get; private set; }

        /// <summary>Initializes the item with the given segments, colour, and line width.</summary>
        /// <param name="segments">World-space line segments.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels.</param>
        public CxSegment3DItem(CxSegment3D[] segments, Color color, float size = 1.0f) : base(color, size)
        {
            Segment3Ds = segments;
        }

        // ── Interaction state ────────────────────────────────────────────────────
        private int _hitSegIndex = -1;
        private int _hitEndpoint = -1;  // -1 = line body, 0 = Start, 1 = End
        private float[] _positionsBuffer = new float[0];
        private readonly float[] _endpointBuffer = new float[6];

        /// <inheritdoc/>
        public override bool HitTest(CxPoint3D worldPos) => FindHit(worldPos).seg >= 0;

        /// <inheritdoc/>
        public override void OnMouseDown(CxPoint3D worldPos)
        {
            base.OnMouseDown(worldPos);
            (_hitSegIndex, _hitEndpoint) = FindHit(worldPos);
        }

        /// <inheritdoc/>
        public override void OnMouseMove(CxPoint3D worldPos, CxPoint3D prevWorldPos)
        {
            if (_hitSegIndex < 0) return;
            double dx = worldPos.X - prevWorldPos.X;
            double dy = worldPos.Y - prevWorldPos.Y;
            double dz = worldPos.Z - prevWorldPos.Z;
            if (_hitEndpoint < 0)
                TranslateSeg(_hitSegIndex, dx, dy, dz);
            else
                MoveEndpoint(_hitSegIndex, _hitEndpoint, dx, dy, dz);
            RaiseOnChanged();
        }

        /// <inheritdoc/>
        public override void OnMouseUp()
        {
            _hitSegIndex = -1;
            _hitEndpoint = -1;
        }

        /// <inheritdoc/>
        public override void Translate(double dx, double dy, double dz)
        {
            if (Segment3Ds == null) return;
            for (int i = 0; i < Segment3Ds.Length; i++)
                TranslateSeg(i, dx, dy, dz);
        }

        private void TranslateSeg(int idx, double dx, double dy, double dz)
        {
            Segment3Ds[idx] = new CxSegment3D(
                new CxPoint3D((float)(Segment3Ds[idx].Start.X + dx),
                              (float)(Segment3Ds[idx].Start.Y + dy),
                              (float)(Segment3Ds[idx].Start.Z + dz)),
                new CxPoint3D((float)(Segment3Ds[idx].End.X + dx),
                              (float)(Segment3Ds[idx].End.Y + dy),
                              (float)(Segment3Ds[idx].End.Z + dz)));
        }

        private void MoveEndpoint(int idx, int endpoint, double dx, double dy, double dz)
        {
            var seg = Segment3Ds[idx];
            Segment3Ds[idx] = endpoint == 0
                ? new CxSegment3D(new CxPoint3D((float)(seg.Start.X + dx),
                                              (float)(seg.Start.Y + dy),
                                              (float)(seg.Start.Z + dz)), seg.End)
                : new CxSegment3D(seg.Start,
                                new CxPoint3D((float)(seg.End.X + dx),
                                              (float)(seg.End.Y + dy),
                                              (float)(seg.End.Z + dz)));
        }

        private (int seg, int endpoint) FindHit(CxPoint3D worldPos)
        {
            if (Segment3Ds == null) return (-1, -1);
            float t2 = HitThreshold * HitThreshold;
            for (int s = 0; s < Segment3Ds.Length; s++)
            {
                var seg = Segment3Ds[s];
                float dxS = seg.Start.X - worldPos.X, dyS = seg.Start.Y - worldPos.Y, dzS = seg.Start.Z - worldPos.Z;
                if (dxS * dxS + dyS * dyS + dzS * dzS <= t2) return (s, 0);
                float dxE = seg.End.X - worldPos.X, dyE = seg.End.Y - worldPos.Y, dzE = seg.End.Z - worldPos.Z;
                if (dxE * dxE + dyE * dyE + dzE * dzE <= t2) return (s, 1);
                if (PointToSegDistSq(worldPos, seg.Start, seg.End) <= t2) return (s, -1);
            }
            return (-1, -1);
        }

        private static float PointToSegDistSq(CxPoint3D p, CxPoint3D a, CxPoint3D b)
        {
            float abX = b.X - a.X, abY = b.Y - a.Y, abZ = b.Z - a.Z;
            float ab2 = abX * abX + abY * abY + abZ * abZ;
            if (ab2 < 1e-10f)
            {
                float dx = p.X - a.X, dy = p.Y - a.Y, dz = p.Z - a.Z;
                return dx * dx + dy * dy + dz * dz;
            }
            float t = Math.Max(0, Math.Min(1,
                ((p.X - a.X) * abX + (p.Y - a.Y) * abY + (p.Z - a.Z) * abZ) / ab2));
            float cx = a.X + t * abX, cy = a.Y + t * abY, cz = a.Z + t * abZ;
            float ex = p.X - cx, ey = p.Y - cy, ez = p.Z - cz;
            return ex * ex + ey * ey + ez * ez;
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public override bool SupportsGroupTransform => true;

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Segment3Ds == null || Segment3Ds.Length == 0) return;

            PushModelMatrix(gl);
            int required = Segment3Ds.Length * 6;
            if (_positionsBuffer.Length != required)
                _positionsBuffer = new float[required];
            var positions = _positionsBuffer;
            int offset = 0;
            foreach (var seg in Segment3Ds)
            {
                positions[offset++] = seg.Start.X; positions[offset++] = seg.Start.Y; positions[offset++] = seg.Start.Z;
                positions[offset++] = seg.End.X; positions[offset++] = seg.End.Y; positions[offset++] = seg.End.Z;
            }
            var renderer = ModernGLRenderer.For(gl);
            renderer.Draw(gl, OpenGL.GL_LINES, positions, DrawColor, Size);

            // Endpoint markers on the hit segment when active and selected.
            if (IsActiveObj && IsSelected && _hitSegIndex >= 0 && _hitSegIndex < Segment3Ds.Length)
            {
                var s = Segment3Ds[_hitSegIndex];
                _endpointBuffer[0] = s.Start.X; _endpointBuffer[1] = s.Start.Y; _endpointBuffer[2] = s.Start.Z;
                _endpointBuffer[3] = s.End.X; _endpointBuffer[4] = s.End.Y; _endpointBuffer[5] = s.End.Z;
                renderer.Draw(gl, OpenGL.GL_POINTS, _endpointBuffer, DrawColor, 10f);
            }
            PopModelMatrix(gl);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Segment3Ds = null;
                _positionsBuffer = null;
            }
            base.Dispose(disposing);
        }
    }
}
