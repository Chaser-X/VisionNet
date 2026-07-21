using System;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Pure geometric construction and computation operations (planes, intersections, distances).
    /// 数学精确构造，无数据拟合/误差。区别于 Analysis/（基于点云数据的统计/拟合）。
    /// </summary>
    public static partial class VisionOperator
    {
        private const float Eps = 1e-10f;

        // ── 2D ──────────────────────────────────────────────────────────────────

        /// <summary>Creates a line from two points.</summary>
        public static void CreateLine2D(CxPoint2D p1, CxPoint2D p2, out CxLine2D line)
        {
            line = CxLine2D.FromTwoPoints(p1, p2);
        }

        /// <summary>Creates a line from a point and a direction vector.</summary>
        public static void CreateLine2D(CxPoint2D point, CxVector2D direction, out CxLine2D line)
        {
            line = new CxLine2D(point, direction);
        }

        /// <summary>Returns the orientation angle of a line in degrees.</summary>
        public static void LineOrientation(CxLine2D line, AngleMode mode, out float angle)
        {
            float raw = (float)(Math.Atan2(line.Direction.Y, line.Direction.X) * 180.0 / Math.PI);
            angle = mode == AngleMode.Signed180 ? raw : (raw < 0f ? raw + 360f : raw);
        }

        /// <summary>Returns the orientation angle of a segment (from Start to End) in degrees.</summary>
        public static void SegmentOrientation(CxSegment2D segment, AngleMode mode, out float angle)
        {
            float dx = segment.End.X - segment.Start.X;
            float dy = segment.End.Y - segment.Start.Y;
            float raw = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            angle = mode == AngleMode.Signed180 ? raw : (raw < 0f ? raw + 360f : raw);
        }

        /// <summary>Returns the length of a segment.</summary>
        public static void SegmentLength(CxSegment2D segment, out float length)
        {
            float dx = segment.End.X - segment.Start.X;
            float dy = segment.End.Y - segment.Start.Y;
            length = (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Returns the midpoint of a segment.</summary>
        public static void SegmentMidpoint(CxSegment2D segment, out CxPoint2D midpoint)
        {
            midpoint = new CxPoint2D(
                (segment.Start.X + segment.End.X) * 0.5f,
                (segment.Start.Y + segment.End.Y) * 0.5f);
        }

        /// <summary>Computes the intersection point of two infinite lines. Returns false if parallel.</summary>
        public static bool IntersectLineLine(CxLine2D line1, CxLine2D line2, out CxPoint2D pt)
        {
            float d1x = line1.Direction.X, d1y = line1.Direction.Y;
            float d2x = line2.Direction.X, d2y = line2.Direction.Y;
            float denom = d1x * d2y - d1y * d2x;

            if (Math.Abs(denom) < Eps)
            {
                pt = default;
                return false;
            }

            float vx = line2.Point.X - line1.Point.X;
            float vy = line2.Point.Y - line1.Point.Y;
            float t = (vx * d2y - vy * d2x) / denom;

            pt = new CxPoint2D(
                line1.Point.X + t * d1x,
                line1.Point.Y + t * d1y);
            return true;
        }

        /// <summary>Computes the intersection of a line and a segment. Returns false if parallel or no intersection.</summary>
        public static bool IntersectLineSegment(CxLine2D line, CxSegment2D seg, out CxPoint2D pt)
        {
            float d1x = line.Direction.X, d1y = line.Direction.Y;
            float d2x = seg.End.X - seg.Start.X;
            float d2y = seg.End.Y - seg.Start.Y;
            float denom = d1x * d2y - d1y * d2x;

            if (Math.Abs(denom) < Eps)
            {
                pt = default;
                return false;
            }

            float vx = seg.Start.X - line.Point.X;
            float vy = seg.Start.Y - line.Point.Y;
            float t = (vx * d2y - vy * d2x) / denom;
            float s = (vx * d1y - vy * d1x) / denom;

            if (s < 0f || s > 1f)
            {
                pt = default;
                return false;
            }

            pt = new CxPoint2D(
                line.Point.X + t * d1x,
                line.Point.Y + t * d1y);
            return true;
        }

        /// <summary>Projects a point onto an infinite line.</summary>
        public static void ProjectPointToLine(CxPoint2D point, CxLine2D line, out CxPoint2D pt)
        {
            float dx = line.Direction.X, dy = line.Direction.Y;
            float vx = point.X - line.Point.X;
            float vy = point.Y - line.Point.Y;
            float t = (vx * dx + vy * dy) / (dx * dx + dy * dy);
            pt = new CxPoint2D(
                line.Point.X + t * dx,
                line.Point.Y + t * dy);
        }

        /// <summary>Returns the shortest distance from a point to an infinite line.</summary>
        public static void DistancePointToLine2D(CxPoint2D point, CxLine2D line, out float dist)
        {
            float dx = line.Direction.X, dy = line.Direction.Y;
            float vx = point.X - line.Point.X;
            float vy = point.Y - line.Point.Y;
            float cross = vx * dy - vy * dx;
            dist = (float)(Math.Abs(cross) / Math.Sqrt(dx * dx + dy * dy));
        }

        /// <summary>Returns the shortest distance from a point to a segment.</summary>
        public static void DistancePointToSegment2D(CxPoint2D point, CxSegment2D seg, out float dist)
        {
            float sx = seg.Start.X, sy = seg.Start.Y;
            float dx = seg.End.X - sx, dy = seg.End.Y - sy;
            float vx = point.X - sx, vy = point.Y - sy;
            float dot = vx * dx + vy * dy;
            float lenSq = dx * dx + dy * dy;

            float t;
            if (lenSq < Eps)
                t = 0f;
            else
                t = Math.Max(0f, Math.Min(1f, dot / lenSq));

            float cx = sx + t * dx - point.X;
            float cy = sy + t * dy - point.Y;
            dist = (float)Math.Sqrt(cx * cx + cy * cy);
        }

        

        // ── 3D ──────────────────────────────────────────────────────────────────

        /// <summary>Creates a plane from three points. Returns false if the points are collinear.</summary>
        public static bool CreatePlane(CxPoint3D p1, CxPoint3D p2, CxPoint3D p3, out CxPlane3D plane)
        {
            float e1x = p2.X - p1.X, e1y = p2.Y - p1.Y, e1z = p2.Z - p1.Z;
            float e2x = p3.X - p1.X, e2y = p3.Y - p1.Y, e2z = p3.Z - p1.Z;

            float nx = e1y * e2z - e1z * e2y;
            float ny = e1z * e2x - e1x * e2z;
            float nz = e1x * e2y - e1y * e2x;
            float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);

            if (len < Eps)
            {
                plane = default;
                return false;
            }

            float inv = 1f / len;
            plane = new CxPlane3D(p1, new CxVector3D(nx * inv, ny * inv, nz * inv));
            return true;
        }

        /// <summary>Creates a plane from a point and a segment (3 points). Returns false if collinear.</summary>
        public static bool CreatePlane(CxPoint3D point, CxSegment3D segment, out CxPlane3D plane)
        {
            return CreatePlane(point, segment.Start, segment.End, out plane);
        }

        /// <summary>Creates a plane from a point and a normal vector.</summary>
        public static void CreatePlane(CxPoint3D point, CxVector3D normal, out CxPlane3D plane)
        {
            float invLen = 1f / normal.Length;
            plane = new CxPlane3D(point, new CxVector3D(
                normal.X * invLen, normal.Y * invLen, normal.Z * invLen));
        }

        /// <summary>Intersects two planes. Returns false if they are parallel.</summary>
        public static bool IntersectPlanePlane(CxPlane3D p1, CxPlane3D p2, out CxLine3D line)
        {
            float n1x = p1.Normal.X, n1y = p1.Normal.Y, n1z = p1.Normal.Z;
            float n2x = p2.Normal.X, n2y = p2.Normal.Y, n2z = p2.Normal.Z;

            float dx = n1y * n2z - n1z * n2y;
            float dy = n1z * n2x - n1x * n2z;
            float dz = n1x * n2y - n1y * n2x;
            float dirLenSq = dx * dx + dy * dy + dz * dz;

            if (dirLenSq < Eps)
            {
                line = default;
                return false;
            }

            float d1 = n1x * p1.Point.X + n1y * p1.Point.Y + n1z * p1.Point.Z;
            float d2 = n2x * p2.Point.X + n2y * p2.Point.Y + n2z * p2.Point.Z;

            float a11 = n1x * n1x + n1y * n1y + n1z * n1z;
            float a12 = n1x * n2x + n1y * n2y + n1z * n2z;
            float a22 = n2x * n2x + n2y * n2y + n2z * n2z;
            float det = a11 * a22 - a12 * a12;

            float a = (d1 * a22 - d2 * a12) / det;
            float b = (d2 * a11 - d1 * a12) / det;

            line = new CxLine3D(
                new CxPoint3D(
                    a * n1x + b * n2x,
                    a * n1y + b * n2y,
                    a * n1z + b * n2z),
                new CxVector3D(dx, dy, dz));
            return true;
        }

        /// <summary>Intersects a plane with a segment. Returns false if parallel or no intersection.</summary>
        public static bool IntersectPlaneSegment(CxPlane3D plane, CxSegment3D seg, out CxPoint3D pt)
        {
            float nx = plane.Normal.X, ny = plane.Normal.Y, nz = plane.Normal.Z;
            float dx = seg.End.X - seg.Start.X;
            float dy = seg.End.Y - seg.Start.Y;
            float dz = seg.End.Z - seg.Start.Z;
            float denom = nx * dx + ny * dy + nz * dz;

            if (Math.Abs(denom) < Eps)
            {
                pt = default;
                return false;
            }

            float d = nx * plane.Point.X + ny * plane.Point.Y + nz * plane.Point.Z;
            float t = (d - (nx * seg.Start.X + ny * seg.Start.Y + nz * seg.Start.Z)) / denom;

            if (t < 0f || t > 1f)
            {
                pt = default;
                return false;
            }

            pt = new CxPoint3D(
                seg.Start.X + t * dx,
                seg.Start.Y + t * dy,
                seg.Start.Z + t * dz);
            return true;
        }

        /// <summary>Projects a point onto a plane along the plane normal.</summary>
        public static void ProjectPointToPlane(CxPoint3D point, CxPlane3D plane, out CxPoint3D pt)
        {
            float nx = plane.Normal.X, ny = plane.Normal.Y, nz = plane.Normal.Z;
            float vx = point.X - plane.Point.X;
            float vy = point.Y - plane.Point.Y;
            float vz = point.Z - plane.Point.Z;
            float t = vx * nx + vy * ny + vz * nz;
            pt = new CxPoint3D(
                point.X - t * nx,
                point.Y - t * ny,
                point.Z - t * nz);
        }

        /// <summary>Returns the shortest distance from a point to a plane.</summary>
        public static void DistancePointToPlane3D(CxPoint3D point, CxPlane3D plane, out float dist)
        {
            float vx = point.X - plane.Point.X;
            float vy = point.Y - plane.Point.Y;
            float vz = point.Z - plane.Point.Z;
            dist = Math.Abs(vx * plane.Normal.X + vy * plane.Normal.Y + vz * plane.Normal.Z);
        }

        /// <summary>Returns the shortest distance from a point to an infinite 3D line.</summary>
        public static void DistancePointToLine3D(CxPoint3D point, CxLine3D line, out float dist)
        {
            float vx = point.X - line.Point.X;
            float vy = point.Y - line.Point.Y;
            float vz = point.Z - line.Point.Z;
            float dx = line.Direction.X, dy = line.Direction.Y, dz = line.Direction.Z;

            float cx = vy * dz - vz * dy;
            float cy = vz * dx - vx * dz;
            float cz = vx * dy - vy * dx;

            float crossLenSq = cx * cx + cy * cy + cz * cz;
            float dirLenSq = dx * dx + dy * dy + dz * dz;

            dist = (float)(dirLenSq > Eps ? Math.Sqrt(crossLenSq / dirLenSq) : Math.Sqrt(vx * vx + vy * vy + vz * vz));
        }

        /// <summary>Finds the closest point on an infinite 3D line to a given point.</summary>
        public static void ClosestPointOnLine(CxPoint3D point, CxLine3D line, out CxPoint3D pt)
        {
            float dx = line.Direction.X, dy = line.Direction.Y, dz = line.Direction.Z;
            float vx = point.X - line.Point.X;
            float vy = point.Y - line.Point.Y;
            float vz = point.Z - line.Point.Z;
            float dirLenSq = dx * dx + dy * dy + dz * dz;

            float t;
            if (dirLenSq < Eps)
                t = 0f;
            else
                t = (vx * dx + vy * dy + vz * dz) / dirLenSq;

            pt = new CxPoint3D(
                line.Point.X + t * dx,
                line.Point.Y + t * dy,
                line.Point.Z + t * dz);
        }
    }
}
