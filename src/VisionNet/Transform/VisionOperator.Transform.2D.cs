using System;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// 2D coordinate alignment — transforms geometry elements between the world
    /// coordinate system and a <see cref="CxCoordination2D"/> local frame.
    /// </summary>
    /// <remarks>
    /// <para><b>Forward (World → Local, <c>forward=true</c>):</b>  applies
    /// <c>S⁻¹ · R⁻¹ · T⁻¹</c> so that geometry expressed in world space is
    /// shifted into the local frame.</para>
    /// <para><b>Reverse (Local → World, <c>forward=false</c>):</b>  applies
    /// <c>T · R · S</c> so that geometry expressed in the local frame is
    /// mapped back to world space.</para>
    /// <para>
    /// <c>M = T(Origin) · R(Angle) · S(Scale.X, Scale.Y)</c>
    /// </para>
    /// </remarks>
    public static partial class VisionOperator
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the forward or inverse 3×3 affine matrix from a coordinate frame.
        /// </summary>
        private static CxMatrix3X3 BuildMatrix(CxCoordination2D coord, bool forward)
        {
            var M = CxMatrix3X3.Translation(coord.Origin.X, coord.Origin.Y)
                  * CxMatrix3X3.Rotation(coord.Angle)
                  * CxMatrix3X3.Scale(coord.Scale.X, coord.Scale.Y);
            return forward ? M.Inverse() : M;
        }

        /// <summary>
        /// Throws when the frame has non-uniform scale (|Scale.X − Scale.Y| &gt; tolerance).
        /// </summary>
        private static void RequireUniformScale(CxCoordination2D coord, float tolerance = 1e-6f)
        {
            if (Math.Abs(coord.Scale.X - coord.Scale.Y) > tolerance)
                throw new ArgumentException(
                    "非均匀缩放不支持圆/弧/拟合场类型对齐。请保证 Scale.X == Scale.Y。");
        }

        // ── Points ──────────────────────────────────────────────────────────────

        /// <summary>Aligns a 2D point with the given coordinate frame.</summary>
        public static CxPoint2D AlignPoint2D(CxPoint2D point, CxCoordination2D coord, bool forward)
        {
            return BuildMatrix(coord, forward).TransformPoint2D(point);
        }

        // ── Vectors ─────────────────────────────────────────────────────────────

        /// <summary>Aligns a 2D vector (rotation + scale only, no translation).</summary>
        public static CxVector2D AlignVector2D(CxVector2D vector, CxCoordination2D coord, bool forward)
        {
            return BuildMatrix(coord, forward).TransformVector2D(vector);
        }

        // ── Segments ────────────────────────────────────────────────────────────

        /// <summary>Aligns a 2D line segment by transforming both endpoints.</summary>
        public static CxSegment2D AlignSegment2D(CxSegment2D segment, CxCoordination2D coord, bool forward)
        {
            var m = BuildMatrix(coord, forward);
            return new CxSegment2D(
                m.TransformPoint2D(segment.Start),
                m.TransformPoint2D(segment.End));
        }

        // ── Lines ───────────────────────────────────────────────────────────────

        /// <summary>Aligns an infinite 2D line (point + direction).</summary>
        public static CxLine2D AlignLine2D(CxLine2D line, CxCoordination2D coord, bool forward)
        {
            var m = BuildMatrix(coord, forward);
            return new CxLine2D(
                m.TransformPoint2D(line.Point),
                m.TransformVector2D(line.Direction));
        }

        // ── Circles ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 2D circle. Non-uniform scale throws <see cref="ArgumentException"/>.
        /// </summary>
        public static CxCircle2D AlignCircle2D(CxCircle2D circle, CxCoordination2D coord, bool forward)
        {
            RequireUniformScale(coord);
            float s = coord.Scale.X;
            var m = BuildMatrix(coord, forward);
            return new CxCircle2D(
                m.TransformPoint2D(circle.Center),
                forward ? circle.Radius / s : circle.Radius * s);
        }

        // ── Arcs ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 2D arc. Non-uniform scale throws <see cref="ArgumentException"/>.
        /// Angles are rotated by ±<c>coord.Angle</c> (forward: −, reverse: +).
        /// SweepAngle is preserved.
        /// </summary>
        public static CxArc2D AlignArc2D(CxArc2D arc, CxCoordination2D coord, bool forward)
        {
            RequireUniformScale(coord);
            float s = coord.Scale.X;
            var m = BuildMatrix(coord, forward);
            return new CxArc2D(
                m.TransformPoint2D(arc.Center),
                forward ? arc.Radius / s : arc.Radius * s,
                forward ? arc.StartAngle - coord.Angle : arc.StartAngle + coord.Angle,
                arc.SweepAngle);
        }

        // ── Polygons ────────────────────────────────────────────────────────────

        /// <summary>Aligns a 2D polygon / polyline by transforming every vertex.</summary>
        public static CxPolygon2D AlignPolygon2D(CxPolygon2D polygon, CxCoordination2D coord, bool forward)
        {
            var m = BuildMatrix(coord, forward);
            int n = polygon.Points.Length;
            var pts = new CxPoint2D[n];
            for (int i = 0; i < n; i++)
                pts[i] = m.TransformPoint2D(polygon.Points[i]);
            return new CxPolygon2D(pts, polygon.IsClosed);
        }

        // ── Rectangles ─────────────────────────────────────────────────────────

        /// <summary>
        /// Aligns a 2D rectangle. Non-uniform scaling is applied independently
        /// to Width (Scale.X) and Height (Scale.Y). Angle is rotated by ±Angle.
        /// </summary>
        public static CxRectangle2D AlignRectangle2D(CxRectangle2D rect, CxCoordination2D coord, bool forward)
        {
            var m = BuildMatrix(coord, forward);
            float sx = coord.Scale.X, sy = coord.Scale.Y;
            return new CxRectangle2D(
                m.TransformPoint2D(rect.Center),
                new CxSize2D(
                    forward ? rect.Size.Width  / sx : rect.Size.Width  * sx,
                    forward ? rect.Size.Height / sy : rect.Size.Height * sy),
                forward ? rect.Angle - coord.Angle : rect.Angle + coord.Angle);
        }

        // ── Fitting Fields ──────────────────────────────────────────────────────

        /// <summary>Aligns a segment fitting field (uniform scale required).</summary>
        public static CxSegment2DFittingField AlignSegment2DFittingField(
            CxSegment2DFittingField field, CxCoordination2D coord, bool forward)
        {
            RequireUniformScale(coord);
            float s = coord.Scale.X;
            return new CxSegment2DFittingField(
                AlignSegment2D(field.Axis, coord, forward),
                forward ? field.Width / s : field.Width * s);
        }

        /// <summary>Aligns an arc fitting field (uniform scale required).</summary>
        public static CxArc2DFittingField AlignArc2DFittingField(
            CxArc2DFittingField field, CxCoordination2D coord, bool forward)
        {
            RequireUniformScale(coord);
            float s = coord.Scale.X;
            return new CxArc2DFittingField(
                AlignArc2D(field.Axis, coord, forward),
                forward ? field.Width / s : field.Width * s);
        }

        /// <summary>Aligns a polygon fitting field (uniform scale required).</summary>
        public static CxPolygon2DFittingField AlignPolygon2DFittingField(
            CxPolygon2DFittingField field, CxCoordination2D coord, bool forward)
        {
            RequireUniformScale(coord);
            float s = coord.Scale.X;
            return new CxPolygon2DFittingField(
                AlignPolygon2D(field.Axis, coord, forward),
                forward ? field.Width / s : field.Width * s);
        }

        /// <summary>Aligns a circle fitting field (uniform scale required).</summary>
        public static CxCircle2DFittingField AlignCircle2DFittingField(
            CxCircle2DFittingField field, CxCoordination2D coord, bool forward)
        {
            RequireUniformScale(coord);
            float s = coord.Scale.X;
            return new CxCircle2DFittingField(
                AlignCircle2D(field.Axis, coord, forward),
                forward ? field.Width / s : field.Width * s);
        }

        // ── Resize（原点缩放） ──────────────────────────────────────────────

        /// <summary>Scales a polygon's vertices by <paramref name="scaleX"/> / <paramref name="scaleY"/> around the origin.</summary>
        public static void ResizePolygon2D(CxPolygon2D polygon, float scaleX, float scaleY, out CxPolygon2D result)
        {
            int n = polygon.Points.Length;
            var pts = new CxPoint2D[n];
            for (int i = 0; i < n; i++)
                pts[i] = new CxPoint2D(
                    polygon.Points[i].X * scaleX,
                    polygon.Points[i].Y * scaleY);
            result = new CxPolygon2D(pts, polygon.IsClosed);
        }

        /// <summary>Scales an axis-aligned box by <paramref name="scaleX"/> / <paramref name="scaleY"/> around the origin.</summary>
        public static void ResizeBox2D(CxBox2D box, float scaleX, float scaleY, out CxBox2D result)
        {
            result = new CxBox2D(
                new CxPoint2D(box.Center.X * scaleX, box.Center.Y * scaleY),
                new CxSize2D(box.Size.Width * scaleX, box.Size.Height * scaleY));
        }

        /// <summary>Scales a rotated rectangle by <paramref name="scaleX"/> / <paramref name="scaleY"/> around the origin.</summary>
        public static void ResizeRectangle2D(CxRectangle2D rect, float scaleX, float scaleY, out CxRectangle2D result)
        {
            result = new CxRectangle2D(
                new CxPoint2D(rect.Center.X * scaleX, rect.Center.Y * scaleY),
                new CxSize2D(rect.Size.Width * scaleX, rect.Size.Height * scaleY),
                rect.Angle);
        }
    }
}
