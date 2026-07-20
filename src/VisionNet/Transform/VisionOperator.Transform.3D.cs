using System;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// 3D coordinate alignment — transforms geometry elements between the world
    /// coordinate system and a <see cref="CxCoordination3D"/> local frame.
    /// </summary>
    /// <remarks>
    /// <para><b>Forward (World → Local, <c>forward=true</c>):</b>  applies
    /// <c>M⁻¹</c> so that geometry expressed in world space is shifted into
    /// the local frame.</para>
    /// <para><b>Reverse (Local → World, <c>forward=false</c>):</b>  applies
    /// <c>M = T(Origin) · R(XAxis,YAxis,ZAxis) · S(Scale.X,Scale.Y,Scale.Z)</c>
    /// to map local geometry back to world space.</para>
    /// </remarks>
    public static partial class VisionOperator
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the forward or inverse 4×4 affine matrix from a 3D coordinate frame.
        /// </summary>
        private static CxMatrix4X4 BuildMatrix3D(CxCoordination3D coord, bool forward)
        {
            var X = coord.XAxis; var Y = coord.YAxis; var Z = coord.ZAxis;
            var s = coord.Scale; var o = coord.Origin;

            var rs = new CxMatrix4X4(new float[]
            {
                X.X * s.X, Y.X * s.Y, Z.X * s.Z, 0,
                X.Y * s.X, Y.Y * s.Y, Z.Y * s.Z, 0,
                X.Z * s.X, Y.Z * s.Y, Z.Z * s.Z, 0,
                0,         0,         0,         1
            });

            var M = CxMatrix4X4.Translation(o.X, o.Y, o.Z) * rs;
            return forward ? M.Inverse() : M;
        }

        /// <summary>
        /// Throws when the frame has non-uniform scale
        /// (|Scale.X−Scale.Y| > tol or |Scale.Y−Scale.Z| > tol).
        /// </summary>
        private static void RequireUniformScale3D(CxCoordination3D coord, float tolerance = 1e-6f)
        {
            if (Math.Abs(coord.Scale.X - coord.Scale.Y) > tolerance ||
                Math.Abs(coord.Scale.Y - coord.Scale.Z) > tolerance)
                throw new ArgumentException(
                    "非均匀缩放不支持该类型几何元素对齐。请保证 Scale.X == Scale.Y == Scale.Z。");
        }

        // ── Points ──────────────────────────────────────────────────────────────

        /// <summary>Aligns a 3D point with the given coordinate frame.</summary>
        public static CxPoint3D AlignPoint3D(CxPoint3D point, CxCoordination3D coord, bool forward)
        {
            return BuildMatrix3D(coord, forward).TransformPoint3D(point);
        }

        // ── Vectors ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D vector (rotation + scale only, no translation).
        /// </summary>
        public static CxVector3D AlignVector3D(CxVector3D vector, CxCoordination3D coord, bool forward)
        {
            return BuildMatrix3D(coord, forward).TransformVector3D(vector);
        }

        // ── Segments ────────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D line segment by transforming both endpoints.
        /// </summary>
        public static CxSegment3D AlignSegment3D(CxSegment3D segment, CxCoordination3D coord, bool forward)
        {
            var m = BuildMatrix3D(coord, forward);
            return new CxSegment3D(
                m.TransformPoint3D(segment.Start),
                m.TransformPoint3D(segment.End));
        }

        // ── Lines ───────────────────────────────────────────────────────────────

        /// <summary>Aligns an infinite 3D line (point + direction).</summary>
        public static CxLine3D AlignLine3D(CxLine3D line, CxCoordination3D coord, bool forward)
        {
            var m = BuildMatrix3D(coord, forward);
            return new CxLine3D(
                m.TransformPoint3D(line.Point),
                m.TransformVector3D(line.Direction));
        }

        // ── Planes ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D plane. Non-uniform scale throws <see cref="ArgumentException"/>.
        /// The normal is re-normalized after transformation.
        /// </summary>
        public static CxPlane3D AlignPlane3D(CxPlane3D plane, CxCoordination3D coord, bool forward)
        {
            RequireUniformScale3D(coord);
            var m = BuildMatrix3D(coord, forward);
            return new CxPlane3D(
                m.TransformPoint3D(plane.Point),
                m.TransformVector3D(plane.Normal).Normalize());
        }

        // ── Spheres ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D sphere. Non-uniform scale throws <see cref="ArgumentException"/>.
        /// </summary>
        public static CxSphere AlignSphere(CxSphere sphere, CxCoordination3D coord, bool forward)
        {
            RequireUniformScale3D(coord);
            float s = coord.Scale.X;
            var m = BuildMatrix3D(coord, forward);
            return new CxSphere(
                m.TransformPoint3D(sphere.Center),
                forward ? sphere.Radius / s : sphere.Radius * s);
        }

        // ── 3D Circles ──────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D circle. Non-uniform scale throws <see cref="ArgumentException"/>.
        /// The normal is re-normalized after transformation.
        /// </summary>
        public static CxCircle3D AlignCircle3D(CxCircle3D circle, CxCoordination3D coord, bool forward)
        {
            RequireUniformScale3D(coord);
            float s = coord.Scale.X;
            var m = BuildMatrix3D(coord, forward);
            return new CxCircle3D(
                m.TransformPoint3D(circle.Center),
                m.TransformVector3D(circle.Normal).Normalize(),
                forward ? circle.Radius / s : circle.Radius * s);
        }

        // ── Polygons ────────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D polygon / polyline by transforming every vertex.
        /// </summary>
        public static CxPolygon3D AlignPolygon3D(CxPolygon3D polygon, CxCoordination3D coord, bool forward)
        {
            var m = BuildMatrix3D(coord, forward);
            int n = polygon.Points.Length;
            var pts = new CxPoint3D[n];
            for (int i = 0; i < n; i++)
                pts[i] = m.TransformPoint3D(polygon.Points[i]);
            return new CxPolygon3D(pts, polygon.IsClosed);
        }

        // ── Axis-Aligned Boxes ──────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D axis-aligned bounding box. The center receives a full
        /// affine transform, while the size is scaled per-axis (rotation is
        /// <b>not</b> applied to preserve the AABB invariant).
        /// </summary>
        public static CxBox3D AlignBox3D(CxBox3D box, CxCoordination3D coord, bool forward)
        {
            var m = BuildMatrix3D(coord, forward);
            float sx = coord.Scale.X, sy = coord.Scale.Y, sz = coord.Scale.Z;
            return new CxBox3D(
                m.TransformPoint3D(box.Center),
                new CxSize3D(
                    forward ? box.Size.Width  / sx : box.Size.Width  * sx,
                    forward ? box.Size.Height / sy : box.Size.Height * sy,
                    forward ? box.Size.Depth  / sz : box.Size.Depth  * sz));
        }

        // ── Text Labels ─────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 3D text label. Location is transformed as a point and
        /// font size is scaled uniformly by Scale.X.
        /// </summary>
        public static CxTextInfo AlignTextInfo(CxTextInfo text, CxCoordination3D coord, bool forward)
        {
            var m = BuildMatrix3D(coord, forward);
            float s = coord.Scale.X;
            return new CxTextInfo(
                m.TransformPoint3D(text.Location),
                text.Text,
                forward ? text.Size / s : text.Size * s);
        }
    }
}
