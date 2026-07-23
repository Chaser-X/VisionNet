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

        // ── 2D: Construction ─────────────────────────────────────────────────────

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

        /// <summary>Converts an arc fitting field (annular sector) to a closed polygon.</summary>
        /// <param name="segmentCount">Number of segments for each arc (min 3). Total vertices = 2 × (segmentCount + 1).</param>
        public static void Arc2DFittingFieldToPolygon2D(CxArc2DFittingField field, int segmentCount, out CxPolygon2D polygon)
        {
            var arc = field.Axis;
            int n = Math.Max(3, segmentCount);
            float radPerStep = arc.SweepAngle * (float)Math.PI / (180f * n);
            float ri = Math.Max(1f, arc.Radius - field.Width / 2f);
            float ro = arc.Radius + field.Width / 2f;
            float cx = arc.Center.X, cy = arc.Center.Y;
            int total = 2 * (n + 1);
            var pts = new CxPoint2D[total];

            float a0 = arc.StartAngle * (float)Math.PI / 180f;
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + radPerStep * i;
                pts[i] = new CxPoint2D(cx + ro * (float)Math.Cos(a), cy + ro * (float)Math.Sin(a));
            }
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + radPerStep * (n - i);
                pts[n + 1 + i] = new CxPoint2D(cx + ri * (float)Math.Cos(a), cy + ri * (float)Math.Sin(a));
            }

            polygon = new CxPolygon2D(pts, true);
        }

        /// <summary>Converts an arc to an open polyline by sampling along the curve.</summary>
        /// <param name="segmentCount">Number of segments (min 2). Total vertices = segmentCount + 1.</param>
        public static void Arc2DToPolygon2D(CxArc2D arc, int segmentCount, out CxPolygon2D polygon)
        {
            int n = Math.Max(2, segmentCount);
            float radStep = arc.SweepAngle * (float)Math.PI / (180f * n);
            float a0 = arc.StartAngle * (float)Math.PI / 180f;
            float cx = arc.Center.X, cy = arc.Center.Y, R = arc.Radius;
            var pts = new CxPoint2D[n + 1];

            for (int i = 0; i <= n; i++)
            {
                float a = a0 + radStep * i;
                pts[i] = new CxPoint2D(cx + R * (float)Math.Cos(a), cy + R * (float)Math.Sin(a));
            }

            polygon = new CxPolygon2D(pts, false);
        }

        /// <summary>Converts a circle to a closed polygon by sampling the circumference.</summary>
        /// <param name="segmentCount">Number of vertices (min 3).</param>
        public static void Circle2DToPolygon2D(CxCircle2D circle, int segmentCount, out CxPolygon2D polygon)
        {
            int n = Math.Max(3, segmentCount);
            float radStep = 2f * (float)Math.PI / n;
            float cx = circle.Center.X, cy = circle.Center.Y, R = circle.Radius;
            var pts = new CxPoint2D[n];

            for (int i = 0; i < n; i++)
            {
                float a = radStep * i;
                pts[i] = new CxPoint2D(cx + R * (float)Math.Cos(a), cy + R * (float)Math.Sin(a));
            }

            polygon = new CxPolygon2D(pts, true);
        }

        /// <summary>Converts a circle fitting field (annular ring) to a closed polygon.</summary>
        /// <param name="segmentCount">Number of vertices per ring (min 3). Total vertices = 2 × segmentCount.</param>
        public static void Circle2DFittingFieldToPolygon2D(CxCircle2DFittingField field, int segmentCount, out CxPolygon2D polygon)
        {
            int n = Math.Max(3, segmentCount);
            float radStep = 2f * (float)Math.PI / n;
            float cx = field.Axis.Center.X, cy = field.Axis.Center.Y;
            float R = field.Axis.Radius;
            float ri = Math.Max(1f, R - field.Width / 2f);
            float ro = R + field.Width / 2f;
            int total = 2 * n;
            var pts = new CxPoint2D[total];

            for (int i = 0; i < n; i++)
            {
                float a = radStep * i;
                pts[i] = new CxPoint2D(cx + ro * (float)Math.Cos(a), cy + ro * (float)Math.Sin(a));
            }
            for (int i = 0; i < n; i++)
            {
                float a = radStep * i;
                pts[n + i] = new CxPoint2D(cx + ri * (float)Math.Cos(-a), cy + ri * (float)Math.Sin(-a));
            }

            polygon = new CxPolygon2D(pts, true);
        }

        /// <summary>Converts a polygon fitting field (mitre band) to a closed polygon.</summary>
        public static void Polygon2DFittingFieldToPolygon2D(CxPolygon2DFittingField field, out CxPolygon2D polygon)
        {
            var pts = field.Axis.Points;
            float halfW = field.Width / 2f;
            bool closed = field.Axis.IsClosed;

            if (pts == null || pts.Length < 2)
            { polygon = new CxPolygon2D(null, true); return; }

            int n = pts.Length;
            var outer = new CxPoint2D[n];
            var inner = new CxPoint2D[n];
            const float mitreLimit = 4f;

            for (int i = 0; i < n; i++)
            {
                CxPoint2D tIn, tOut;
                if (closed)
                {
                    tIn  = new CxPoint2D(pts[i].X - pts[(i - 1 + n) % n].X, pts[i].Y - pts[(i - 1 + n) % n].Y);
                    tOut = new CxPoint2D(pts[(i + 1) % n].X - pts[i].X, pts[(i + 1) % n].Y - pts[i].Y);
                }
                else
                {
                    tIn  = i > 0
                        ? new CxPoint2D(pts[i].X - pts[i - 1].X, pts[i].Y - pts[i - 1].Y)
                        : new CxPoint2D(pts[1].X - pts[0].X, pts[1].Y - pts[0].Y);
                    tOut = i < n - 1
                        ? new CxPoint2D(pts[i + 1].X - pts[i].X, pts[i + 1].Y - pts[i].Y)
                        : new CxPoint2D(pts[n - 1].X - pts[n - 2].X, pts[n - 1].Y - pts[n - 2].Y);
                }

                float inLen  = (float)Math.Sqrt(tIn.X  * tIn.X  + tIn.Y  * tIn.Y);
                float outLen = (float)Math.Sqrt(tOut.X * tOut.X + tOut.Y * tOut.Y);
                if (inLen  > 0) { tIn  = new CxPoint2D(tIn.X  / inLen,  tIn.Y  / inLen); }
                else { tIn  = new CxPoint2D(1, 0); }
                if (outLen > 0) { tOut = new CxPoint2D(tOut.X / outLen, tOut.Y / outLen); }
                else { tOut = new CxPoint2D(1, 0); }

                float dot = tIn.X * tOut.X + tIn.Y * tOut.Y;
                if (dot > 1f) dot = 1f; else if (dot < -1f) dot = -1f;
                float cosHalf = (float)Math.Sqrt((1f + dot) / 2f);
                if (cosHalf < 1e-4f) cosHalf = 1e-4f;
                float mitreLen = halfW / cosHalf;
                if (mitreLen > mitreLimit * halfW) mitreLen = mitreLimit * halfW;

                float perpInX  = -tIn.Y,  perpInY  = tIn.X;
                float perpOutX = -tOut.Y, perpOutY = tOut.X;
                float bx = perpInX + perpOutX;
                float by = perpInY + perpOutY;
                float bLen = (float)Math.Sqrt(bx * bx + by * by);
                if (bLen > 0) { bx /= bLen; by /= bLen; }
                else { bx = perpInX; by = perpInY; }

                outer[i] = new CxPoint2D(pts[i].X + bx * mitreLen, pts[i].Y + by * mitreLen);
                inner[i] = new CxPoint2D(pts[i].X - bx * mitreLen, pts[i].Y - by * mitreLen);
            }

            var band = new CxPoint2D[2 * n];
            for (int i = 0; i < n; i++) band[i] = outer[i];
            for (int i = 0; i < n; i++) band[n + i] = inner[n - 1 - i];
            polygon = new CxPolygon2D(band, true);
        }

        /// <summary>Extends the first and last segments of an open polyline by given lengths.</summary>
        public static void ExtendPolygon2D(CxPolygon2D polygon, float startLength, float endLength, out CxPolygon2D result)
        {
            var pts = polygon.Points;
            if (pts == null || pts.Length < 2 || polygon.IsClosed || (startLength <= 0 && endLength <= 0))
            { result = polygon; return; }

            int n = pts.Length;
            float sx = pts[1].X - pts[0].X, sy = pts[1].Y - pts[0].Y;
            float sLen = (float)Math.Sqrt(sx * sx + sy * sy);
            float ex = pts[n - 1].X - pts[n - 2].X, ey = pts[n - 1].Y - pts[n - 2].Y;
            float eLen = (float)Math.Sqrt(ex * ex + ey * ey);

            var outPts = new CxPoint2D[n];
            if (sLen > 0 && startLength > 0)
            {
                float d = startLength / sLen;
                outPts[0] = new CxPoint2D(pts[0].X - sx * d, pts[0].Y - sy * d);
            }
            else
                outPts[0] = pts[0];

            for (int i = 1; i < n - 1; i++)
                outPts[i] = pts[i];

            if (eLen > 0 && endLength > 0)
            {
                float d = endLength / eLen;
                outPts[n - 1] = new CxPoint2D(pts[n - 1].X + ex * d, pts[n - 1].Y + ey * d);
            }
            else
                outPts[n - 1] = pts[n - 1];

            result = new CxPolygon2D(outPts, false);
        }

        // ── 2D: Properties (Angles & Lengths) ────────────────────────────────────

        /// <summary>Returns the orientation angle of a line in degrees.</summary>
        public static void Line2DOrientation(CxLine2D line, AngleMode mode, out float angle)
        {
            float raw = (float)(Math.Atan2(line.Direction.Y, line.Direction.X) * 180.0 / Math.PI);
            angle = mode == AngleMode.Signed180 ? raw : (raw < 0f ? raw + 360f : raw);
        }

        /// <summary>Returns the orientation angle of a segment (from Start to End) in degrees.</summary>
        public static void Segment2DOrientation(CxSegment2D segment, AngleMode mode, out float angle)
        {
            float dx = segment.End.X - segment.Start.X;
            float dy = segment.End.Y - segment.Start.Y;
            float raw = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            angle = mode == AngleMode.Signed180 ? raw : (raw < 0f ? raw + 360f : raw);
        }

        /// <summary>Returns the length of a segment.</summary>
        public static void Segment2DLength(CxSegment2D segment, out float length)
        {
            float dx = segment.End.X - segment.Start.X;
            float dy = segment.End.Y - segment.Start.Y;
            length = (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Returns the midpoint of a segment.</summary>
        public static void Segment2DMidpoint(CxSegment2D segment, out CxPoint2D midpoint)
        {
            midpoint = new CxPoint2D(
                (segment.Start.X + segment.End.X) * 0.5f,
                (segment.Start.Y + segment.End.Y) * 0.5f);
        }

        /// <summary>Returns the total perimeter length of a polygon.</summary>
        public static void Polygon2DLength(CxPolygon2D polygon, out float length)
        {
            var pts = polygon.Points;
            if (pts == null || pts.Length < 2) { length = 0; return; }
            float sum = 0;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float dx = pts[i + 1].X - pts[i].X;
                float dy = pts[i + 1].Y - pts[i].Y;
                sum += (float)Math.Sqrt(dx * dx + dy * dy);
            }
            if (polygon.IsClosed && pts.Length >= 2)
            {
                float dx = pts[0].X - pts[pts.Length - 1].X;
                float dy = pts[0].Y - pts[pts.Length - 1].Y;
                sum += (float)Math.Sqrt(dx * dx + dy * dy);
            }
            length = sum;
        }

        // ── 2D: Intersection ─────────────────────────────────────────────────────

        /// <summary>Computes the intersection point of two infinite lines. Returns false if parallel.</summary>
        public static bool IntersectLineLine2D(CxLine2D line1, CxLine2D line2, out CxPoint2D pt)
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
        public static bool IntersectLineSegment2D(CxLine2D line, CxSegment2D seg, out CxPoint2D pt)
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

        /// <summary>Clips an infinite line to the region inside a rotated rectangle. Returns false if no intersection.</summary>
        public static bool CropLineToRectangle2D(CxLine2D line, CxRectangle2D rect, out CxSegment2D segment)
        {
            // Build a local frame that un-rotates the rectangle to axis-aligned
            var localFrame = new CxCoordination2D(rect.Center, new CxPoint2D(1f, 1f), rect.Angle);

            // Transform line to local frame (World → Local)
            var localLine = AlignLine2D(line, localFrame, forward: true);
            float px = localLine.Point.X, py = localLine.Point.Y;
            float dx = localLine.Direction.X, dy = localLine.Direction.Y;

            float hw = rect.Size.Width / 2f, hh = rect.Size.Height / 2f;
            float tNear = float.NegativeInfinity, tFar = float.PositiveInfinity;

            // X boundaries
            if (Math.Abs(dx) < Eps)
            {
                if (px < -hw || px > hw) { segment = default; return false; }
            }
            else
            {
                float t1 = (-hw - px) / dx, t2 = (hw - px) / dx;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tNear = Math.Max(tNear, t1);
                tFar  = Math.Min(tFar,  t2);
                if (tNear > tFar) { segment = default; return false; }
            }

            // Y boundaries
            if (Math.Abs(dy) < Eps)
            {
                if (py < -hh || py > hh) { segment = default; return false; }
            }
            else
            {
                float t1 = (-hh - py) / dy, t2 = (hh - py) / dy;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tNear = Math.Max(tNear, t1);
                tFar  = Math.Min(tFar,  t2);
                if (tNear > tFar) { segment = default; return false; }
            }

            // Transform endpoints back to world (Local → World)
            var localStart = new CxPoint2D(px + dx * tNear, py + dy * tNear);
            var localEnd   = new CxPoint2D(px + dx * tFar,  py + dy * tFar);
            var worldStart = AlignPoint2D(localStart, localFrame, forward: false);
            var worldEnd   = AlignPoint2D(localEnd,   localFrame, forward: false);

            segment = new CxSegment2D(worldStart, worldEnd);
            return true;
        }

        /// <summary>Clips a circle against a polygon, returning the arc portion inside the polygon.</summary>
        public static bool CropCircleToPolygon2D(CxCircle2D circle, CxPolygon2D polygon, out CxArc2D arc)
        {
            var pts = polygon.Points;
            if (pts == null || pts.Length < 2) { arc = default; return false; }

            int n = pts.Length;
            float cx = circle.Center.X, cy = circle.Center.Y, R = circle.Radius;

            // Collect intersection angles
            var angles = new System.Collections.Generic.List<float>();
            for (int i = 0; i < n; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];

                float dx = b.X - a.X, dy = b.Y - a.Y;
                float fx = a.X - cx, fy = a.Y - cy;
                float a2 = dx * dx + dy * dy;
                float b2 = 2f * (fx * dx + fy * dy);
                float c2 = fx * fx + fy * fy - R * R;

                if (Math.Abs(a2) < Eps) continue; // degenerate edge

                float disc = b2 * b2 - 4f * a2 * c2;
                if (disc < 0) continue;

                float sqrtDisc = (float)Math.Sqrt(disc);
                float t1 = (-b2 - sqrtDisc) / (2f * a2);
                float t2 = (-b2 + sqrtDisc) / (2f * a2);

                // for each valid t in [0,1], compute point and angle
                if (t1 >= -Eps && t1 <= 1f + Eps)
                {
                    float px = a.X + Math.Max(0f, Math.Min(1f, t1)) * dx;
                    float py = a.Y + Math.Max(0f, Math.Min(1f, t1)) * dy;
                    angles.Add((float)(Math.Atan2(py - cy, px - cx) * 180.0 / Math.PI));
                }
                if (t2 >= -Eps && t2 <= 1f + Eps)
                {
                    float px = a.X + Math.Max(0f, Math.Min(1f, t2)) * dx;
                    float py = a.Y + Math.Max(0f, Math.Min(1f, t2)) * dy;
                    angles.Add((float)(Math.Atan2(py - cy, px - cx) * 180.0 / Math.PI));
                }
            }

            if (angles.Count == 0)
            {
                // No intersection: test if center is inside polygon
                if (IsPointInPolygon2D(circle.Center, polygon))
                {
                    arc = new CxArc2D(circle.Center, R, 0f, 360f);
                    return true;
                }
                arc = default;
                return false;
            }

            // Sort angles
            angles.Sort();

            // Remove near-duplicates
            var filtered = new System.Collections.Generic.List<float>();
            filtered.Add(angles[0]);
            for (int i = 1; i < angles.Count; i++)
                if (angles[i] - filtered[filtered.Count - 1] > 1e-4f)
                    filtered.Add(angles[i]);
            angles = filtered;

            if (angles.Count < 2) { arc = default; return false; }

            // Find the longest interior arc segment
            float bestStart = 0, bestSweep = 0;
            float testRadius = Math.Max(1f, R - 0.5f);

            for (int i = 0; i < angles.Count; i++)
            {
                float aStart = angles[i];
                float aEnd = angles[(i + 1) % angles.Count];
                float sweep;

                if (i < angles.Count - 1)
                {
                    sweep = aEnd - aStart;
                }
                else
                {
                    // Last segment: wrap around 360
                    sweep = (aEnd + 360f) - aStart;
                }

                if (sweep < 1e-3f) continue;

                // Test midpoint of this arc segment
                float midAngle = aStart + sweep * 0.5f;
                float midRad = midAngle * (float)Math.PI / 180f;
                var testPt = new CxPoint2D(
                    cx + testRadius * (float)Math.Cos(midRad),
                    cy + testRadius * (float)Math.Sin(midRad));

                if (IsPointInPolygon2D(testPt, polygon))
                {
                    if (sweep > bestSweep)
                    {
                        bestStart = aStart;
                        bestSweep = sweep;
                    }
                }
            }

            if (bestSweep < 1e-3f) { arc = default; return false; }

            arc = new CxArc2D(circle.Center, R, bestStart, bestSweep);
            return true;
        }

        // ── 2D: Projection & Distance ────────────────────────────────────────────

        /// <summary>Projects a point onto an infinite line.</summary>
        public static void ProjectPointToLine2D(CxPoint2D point, CxLine2D line, out CxPoint2D pt)
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

        /// <summary>Returns the Euclidean distance between two points.</summary>
        public static void DistancePointToPoint2D(CxPoint2D a, CxPoint2D b, out float dist)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            dist = (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Returns the shortest distance from a point to a rotated rectangle (0 if inside).</summary>
        public static void DistancePointToRectangle2D(CxPoint2D p, CxRectangle2D rect, out float dist)
        {
            float rad = -rect.Angle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            float dx = p.X - rect.Center.X, dy = p.Y - rect.Center.Y;
            float lx = dx * cos - dy * sin;
            float ly = dx * sin + dy * cos;
            float hw = rect.Size.Width / 2f, hh = rect.Size.Height / 2f;
            float ddx = Math.Max(0f, Math.Abs(lx) - hw);
            float ddy = Math.Max(0f, Math.Abs(ly) - hh);
            dist = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
        }

        /// <summary>Returns the shortest distance from a point to an axis-aligned box (0 if inside).</summary>
        public static void DistancePointToBox2D(CxPoint2D p, CxBox2D box, out float dist)
        {
            float hw = box.Size.Width / 2f, hh = box.Size.Height / 2f;
            float dx = p.X - box.Center.X, dy = p.Y - box.Center.Y;
            float ddx = Math.Max(0f, Math.Abs(dx) - hw);
            float ddy = Math.Max(0f, Math.Abs(dy) - hh);
            dist = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
        }

        /// <summary>Returns the shortest distance from a point to the edges of a polygon (open or closed).</summary>
        public static void DistancePointToPolygon2D(CxPoint2D p, CxPolygon2D polygon, out float dist)
        {
            var pts = polygon.Points;
            if (pts == null || pts.Length < 2) { dist = float.MaxValue; return; }
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                DistancePointToSegment2D(p, new CxSegment2D(pts[i], pts[i + 1]), out float d);
                if (d < best) best = d;
            }
            if (polygon.IsClosed)
            {
                int last = pts.Length - 1;
                DistancePointToSegment2D(p, new CxSegment2D(pts[last], pts[0]), out float d);
                if (d < best) best = d;
            }
            dist = best;
        }

        /// <summary>Returns the shortest distance from a point to an arc (32-sample approximation).</summary>
        public static void DistancePointToArc2D(CxPoint2D p, CxArc2D arc, out float dist)
        {
            float rad = arc.StartAngle * (float)Math.PI / 180f;
            float best = float.MaxValue;
            int numSamples = 32;
            for (int i = 0; i <= numSamples; i++)
            {
                float t = (float)i / numSamples;
                float a = rad + arc.SweepAngle * t * (float)Math.PI / 180f;
                float px = arc.Center.X + arc.Radius * (float)Math.Cos(a);
                float py = arc.Center.Y + arc.Radius * (float)Math.Sin(a);
                float dx = p.X - px, dy = p.Y - py;
                float d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            dist = (float)Math.Sqrt(best);
        }

        // ── 2D: Translate & Rotate ────────────────────────────────────────────────

        /// <summary>Translates a point by an offset vector.</summary>
        public static void TranslatePoint2D(CxPoint2D point, CxVector2D offset, out CxPoint2D result)
        {
            result = new CxPoint2D(point.X + offset.X, point.Y + offset.Y);
        }

        /// <summary>Translates a segment by an offset vector.</summary>
        public static void TranslateSegment2D(CxSegment2D seg, CxVector2D offset, out CxSegment2D result)
        {
            TranslatePoint2D(seg.Start, offset, out var s);
            TranslatePoint2D(seg.End,   offset, out var e);
            result = new CxSegment2D(s, e);
        }

        /// <summary>Rotates a 2D vector by the given angle (degrees) around the origin.</summary>
        public static void RotateVector2D(CxVector2D vec, float angleDeg, out CxVector2D result)
        {
            float rad = angleDeg * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            result = new CxVector2D(
                vec.X * cos - vec.Y * sin,
                vec.X * sin + vec.Y * cos);
        }

        /// <summary>Rotates a segment around the origin by the given angle (degrees).</summary>
        public static void RotateSegment2D(CxSegment2D seg, float angleDeg, out CxSegment2D result)
        {
            float rad = angleDeg * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            var s = new CxPoint2D(
                seg.Start.X * cos - seg.Start.Y * sin,
                seg.Start.X * sin + seg.Start.Y * cos);
            var e = new CxPoint2D(
                seg.End.X * cos - seg.End.Y * sin,
                seg.End.X * sin + seg.End.Y * cos);
            result = new CxSegment2D(s, e);
        }

        /// <summary>Returns the point at a given distance along the polygon perimeter from the first vertex.</summary>
        public static void GetPointOnPolygon2D(CxPolygon2D polygon, float distance, out CxPoint2D point)
        {
            var pts = polygon.Points;
            if (pts == null || pts.Length < 2) { point = default; return; }

            Polygon2DLength(polygon, out float totalLen);
            if (totalLen < 1e-6f) { point = pts[0]; return; }

            float d = distance;
            // Handle negative/wrap-around for closed polygons
            if (polygon.IsClosed)
            {
                d = d % totalLen;
                if (d < 0) d += totalLen;
            }

            if (d <= 0) { point = pts[0]; return; }
            if (d >= totalLen) { point = pts[pts.Length - 1]; return; }

            float accum = 0;
            int n = pts.Length;
            for (int i = 0; i < n - 1; i++)
            {
                float dx = pts[i + 1].X - pts[i].X;
                float dy = pts[i + 1].Y - pts[i].Y;
                float segLen = (float)Math.Sqrt(dx * dx + dy * dy);
                if (segLen < 1e-10f) continue;

                if (accum + segLen >= d)
                {
                    float t = (d - accum) / segLen;
                    point = new CxPoint2D(pts[i].X + dx * t, pts[i].Y + dy * t);
                    return;
                }
                accum += segLen;
            }

            // For closed polygons, check the closing edge
            if (polygon.IsClosed)
            {
                float dx = pts[0].X - pts[n - 1].X;
                float dy = pts[0].Y - pts[n - 1].Y;
                float segLen = (float)Math.Sqrt(dx * dx + dy * dy);
                if (segLen >= 1e-10f && accum + segLen >= d)
                {
                    float t = (d - accum) / segLen;
                    point = new CxPoint2D(pts[n - 1].X + dx * t, pts[n - 1].Y + dy * t);
                    return;
                }
            }

            point = pts[pts.Length - 1];
        }

        /// <summary>
        /// Simplifies polygon vertices using the Ramer-Douglas-Peucker algorithm.
        /// Fixes applied vs. previous version:
        /// 1. Closed polygons: the wrap-around segment (pts[n-1]→pts[0]) is now also simplified.
        /// 2. Hard cap: after RDP, output is clamped to <paramref name="maxPoints"/> via uniform sub-sampling.
        /// </summary>
        /// <param name="epsilon">Maximum allowed perpendicular deviation in the same units as the polygon coordinates.</param>
        /// <param name="maxPoints">Hard cap on output vertex count after RDP. Default 512.</param>
        public static void SimplifyPolygon2D(CxPolygon2D polygon, float epsilon, out CxPolygon2D result, int maxPoints = 512)
        {
            var src = polygon.Points;
            if (src == null || src.Length <= 3)
            { result = polygon; return; }

            // For closed polygons, append pts[0] so RDP also processes the wrap-around segment.
            bool closed  = polygon.IsClosed;
            bool needsClose = closed
                && (src[0].X != src[src.Length - 1].X || src[0].Y != src[src.Length - 1].Y);

            CxPoint2D[] pts;
            if (needsClose)
            {
                pts = new CxPoint2D[src.Length + 1];
                Array.Copy(src, pts, src.Length);
                pts[src.Length] = src[0];
            }
            else
            {
                pts = src;
            }

            int n = pts.Length;
            bool[] keep = new bool[n];
            keep[0] = keep[n - 1] = true;
            var stack = new System.Collections.Generic.Stack<(int, int)>();
            stack.Push((0, n - 1));

            while (stack.Count > 0)
            {
                var (a, b) = stack.Pop();
                float maxDist = 0;
                int maxIdx   = 0;
                float ax     = pts[a].X, ay = pts[a].Y;
                float bx     = pts[b].X, by = pts[b].Y;
                float ddx    = bx - ax,  ddy = by - ay;
                float lenSq  = ddx * ddx + ddy * ddy;

                for (int i = a + 1; i < b; i++)
                {
                    float dist;
                    float vx = pts[i].X - ax, vy = pts[i].Y - ay;
                    if (lenSq < 1e-10f)
                    {
                        dist = vx * vx + vy * vy;
                    }
                    else
                    {
                        float t  = Math.Max(0f, Math.Min(1f, (vx * ddx + vy * ddy) / lenSq));
                        float cx = ax + t * ddx - pts[i].X;
                        float cy = ay + t * ddy - pts[i].Y;
                        dist = cx * cx + cy * cy;
                    }
                    if (dist > maxDist) { maxDist = dist; maxIdx = i; }
                }

                if (Math.Sqrt(maxDist) > epsilon)
                {
                    keep[maxIdx] = true;
                    stack.Push((a, maxIdx));
                    stack.Push((maxIdx, b));
                }
            }

            int count = 0;
            for (int i = 0; i < n; i++) if (keep[i]) count++;

            // If closed polygon was extended, skip the duplicated last point.
            int outLen = (needsClose && keep[n - 1]) ? count - 1 : count;
            if (outLen < 3)
            {
                // RDP simplified away too many points (polygon fits entirely within epsilon).
                // Fall back to uniform sub-sampling of the source, capped at maxPoints.
                int s = Math.Min(maxPoints, src.Length);
                if (s < 3) { result = polygon; return; }   // truly degenerate
                var sample = new CxPoint2D[s];
                float sStep = (float)(src.Length - 1) / (s - 1);
                for (int i = 0; i < s; i++)
                    sample[i] = src[(int)Math.Round(i * sStep)];
                result = new CxPolygon2D(sample, polygon.IsClosed);
                return;
            }

            var outPts = new CxPoint2D[outLen];
            int idx = 0;
            for (int i = 0; i < n && idx < outLen; i++)
                if (keep[i]) outPts[idx++] = pts[i];

            // Hard cap: uniform sub-sampling when RDP still leaves too many vertices.
            if (outLen > maxPoints)
            {
                var sub  = new CxPoint2D[maxPoints];
                float step = (float)(outLen - 1) / (maxPoints - 1);
                for (int i = 0; i < maxPoints; i++)
                    sub[i] = outPts[(int)Math.Round(i * step)];
                outPts = sub;
            }

            result = new CxPolygon2D(outPts, closed);
        }

        // ── 2D: Containment Tests ────────────────────────────────────────────────

        /// <summary>Tests whether a point lies inside a rotated rectangle.</summary>
        public static bool IsPointInRectangle2D(CxPoint2D p, CxRectangle2D rect)
        {
            float rad = -rect.Angle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            float dx = p.X - rect.Center.X, dy = p.Y - rect.Center.Y;
            float lx = dx * cos - dy * sin;
            float ly = dx * sin + dy * cos;
            return Math.Abs(lx) <= rect.Size.Width / 2f && Math.Abs(ly) <= rect.Size.Height / 2f;
        }

        /// <summary>Ray-casting point-in-polygon test for arbitrary (possibly concave) polygons.</summary>
        public static bool IsPointInPolygon2D(CxPoint2D p, CxPolygon2D polygon)
        {
            var pts = polygon.Points;
            if (pts == null || pts.Length < 3) return false;
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].Y > p.Y) != (pts[j].Y > p.Y) &&
                    p.X < (pts[j].X - pts[i].X) * (p.Y - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Tests whether a point lies inside an annular sector (radial band + angular sweep).</summary>
        public static bool IsPointInAnnularSector2D(CxPoint2D p, CxArc2D arc, float width)
        {
            float dx = p.X - arc.Center.X;
            float dy = p.Y - arc.Center.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            float outerR = arc.Radius + width / 2f;
            float innerR = Math.Max(1f, arc.Radius - width / 2f);
            if (dist < innerR || dist > outerR) return false;

            float angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
            float delta = angle - arc.StartAngle;
            while (delta < 0) delta += 360f;
            while (delta >= 360f) delta -= 360f;

            return arc.SweepAngle > 0
                ? delta <= arc.SweepAngle
                : delta >= 360f + arc.SweepAngle;
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
        public static bool IntersectPlaneSegment3D(CxPlane3D plane, CxSegment3D seg, out CxPoint3D pt)
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
        public static void ProjectPoint3DToPlane(CxPoint3D point, CxPlane3D plane, out CxPoint3D pt)
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
        public static void DistancePoint3DToPlane(CxPoint3D point, CxPlane3D plane, out float dist)
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
        public static void ClosestPointOnLine3D(CxPoint3D point, CxLine3D line, out CxPoint3D pt)
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
