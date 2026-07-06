using SharpGL;
using System;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="Segment3D"/> values as GL_LINES with a configurable line width.
    /// </summary>
    public class CxSegment3DItem : AbstractRenderItem
    {
        /// <summary>Gets the line segments to be rendered.</summary>
        public Segment3D[] Segment3Ds { get; private set; }

        /// <summary>Initializes the item with the given segments, colour, and line width.</summary>
        /// <param name="segments">World-space line segments.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels.</param>
        public CxSegment3DItem(Segment3D[] segments, Color color, float size = 1.0f) : base(color, size)
        {
            Segment3Ds = segments;
        }

        // ── Interaction state ────────────────────────────────────────────────────
        private int _hitSegIndex = -1;
        private int _hitEndpoint = -1;  // -1 = line body, 0 = Start, 1 = End

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
            Segment3Ds[idx] = new Segment3D(
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
                ? new Segment3D(new CxPoint3D((float)(seg.Start.X + dx),
                                              (float)(seg.Start.Y + dy),
                                              (float)(seg.Start.Z + dz)), seg.End)
                : new Segment3D(seg.Start,
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
        public override void Draw(OpenGL gl)
        {
            if (Segment3Ds == null || Segment3Ds.Length == 0) return;

            gl.Color(DrawColor.R / 255.0, DrawColor.G / 255.0, DrawColor.B / 255.0);
            gl.LineWidth(Size);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var seg in Segment3Ds)
            {
                gl.Vertex(seg.Start.X, seg.Start.Y, seg.Start.Z);
                gl.Vertex(seg.End.X,   seg.End.Y,   seg.End.Z);
            }
            gl.End();

            // Endpoint markers on the hit segment when active and selected.
            if (IsActiveObj && IsSelected && _hitSegIndex >= 0 && _hitSegIndex < Segment3Ds.Length)
            {
                gl.PointSize(10f);
                gl.Begin(OpenGL.GL_POINTS);
                gl.Color(DrawColor.R / 255.0, DrawColor.G / 255.0, DrawColor.B / 255.0);
                var s = Segment3Ds[_hitSegIndex];
                gl.Vertex(s.Start.X, s.Start.Y, s.Start.Z);
                gl.Vertex(s.End.X,   s.End.Y,   s.End.Z);
                gl.End();
                gl.PointSize(1f);
            }
        }
    }
}
