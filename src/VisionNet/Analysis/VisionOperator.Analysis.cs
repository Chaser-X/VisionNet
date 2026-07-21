using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using OpenCvSharp;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Measurement and statistical analysis operations.
    /// </summary>
    public static partial class VisionOperator
    {
        /// <summary>
        /// Computes the centroid of a list of 3D points using the native library.
        /// Returns <see cref="float.NegativeInfinity"/> in all axes if the list is empty.
        /// </summary>
        public static CxPoint3D GetPoint3DArrayCenter(List<CxPoint3D> point3Ds)
        {
            var center = new CxPoint3D();
            int ret = GetCenter(point3Ds.ToArray(), point3Ds.Count, ref center);
            if (ret == -1)
                center = new CxPoint3D(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            return center;
        }

        /// <summary>
        /// Computes the axis-aligned bounding box of an array of 3D points using
        /// <see cref="Parallel.ForEach"/> for large arrays.
        /// Returns <c>null</c> if <paramref name="points"/> is empty.
        /// </summary>
        public static CxBox3D? CalculateBoundingBox(CxPoint3D[] points)
        {
            if (points == null || points.Length == 0) return null;

            var partitioner = Partitioner.Create(points, true);
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            var lockObj = new object();

            Parallel.ForEach(partitioner,
                () => (MinX: float.MaxValue, MaxX: float.MinValue,
                       MinY: float.MaxValue, MaxY: float.MinValue,
                       MinZ: float.MaxValue, MaxZ: float.MinValue),
                (p, _, local) => (
                    Math.Min(local.MinX, p.X), Math.Max(local.MaxX, p.X),
                    Math.Min(local.MinY, p.Y), Math.Max(local.MaxY, p.Y),
                    Math.Min(local.MinZ, p.Z), Math.Max(local.MaxZ, p.Z)),
                local =>
                {
                    lock (lockObj)
                    {
                        if (local.MinX < minX) minX = local.MinX;
                        if (local.MaxX > maxX) maxX = local.MaxX;
                        if (local.MinY < minY) minY = local.MinY;
                        if (local.MaxY > maxY) maxY = local.MaxY;
                        if (local.MinZ < minZ) minZ = local.MinZ;
                        if (local.MaxZ > maxZ) maxZ = local.MaxZ;
                    }
                });

            return new CxBox3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        /// <summary>
        /// Computes the axis-aligned bounding box of an array of 3D points using
        /// <see cref="Vector3.Min"/> / <see cref="Vector3.Max"/> SIMD instructions.
        /// Returns <c>null</c> if <paramref name="points"/> is empty.
        /// </summary>
        public static CxBox3D? CalculateBoundingBoxSIMD(CxPoint3D[] points)
        {
            if (points == null || points.Length == 0) return null;

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            for (int i = 0; i < points.Length; i++)
            {
                var v = new Vector3(points[i].X, points[i].Y, points[i].Z);
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            return new CxBox3D(
                new CxPoint3D((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2),
                new CxSize3D(max.X - min.X, max.Y - min.Y, max.Z - min.Z));
        }

        // ── 2D Fitting ─────────────────────────────────────────────────────────

        /// <summary>Fits a circle to 2D points using algebraic least squares. Returns false if fewer than 3 points or degenerate.</summary>
        public static bool FitPointsToCircle2D(CxPoint2D[] points, out CxCircle2D circle)
        {
            circle = default;
            if (points == null || points.Length < 3) return false;

            int n = points.Length;

            double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
            double sz = 0, sxz = 0, syz = 0;

            for (int i = 0; i < n; i++)
            {
                double x = points[i].X, y = points[i].Y;
                double r2 = x * x + y * y;
                sx += x; sy += y;
                sxx += x * x; sxy += x * y; syy += y * y;
                sz += r2; sxz += x * r2; syz += y * r2;
            }

            var A = new Mat(3, 3, MatType.CV_32F);
            var B = new Mat(3, 1, MatType.CV_32F);
            A.Set(0, 0, (float)n);   A.Set(0, 1, (float)sx);   A.Set(0, 2, (float)sy);
            A.Set(1, 0, (float)sx);  A.Set(1, 1, (float)sxx);  A.Set(1, 2, (float)sxy);
            A.Set(2, 0, (float)sy);  A.Set(2, 1, (float)sxy);  A.Set(2, 2, (float)syy);
            B.Set(0, 0, (float)sz);  B.Set(1, 0, (float)sxz);  B.Set(2, 0, (float)syz);

            var X = new Mat();
            if (!Cv2.Solve(A, B, X, DecompTypes.LU))
            {
                circle = default;
                return false;
            }

            float a = X.At<float>(0, 0), bc = X.At<float>(1, 0), c = X.At<float>(2, 0);
            float cx = -a * 0.5f, cy = -bc * 0.5f;
            float radiusSq = cx * cx + cy * cy - c;

            if (radiusSq < 0)
            {
                circle = default;
                return false;
            }

            circle = new CxCircle2D(new CxPoint2D(cx, cy), (float)Math.Sqrt(radiusSq));
            return true;
        }

        /// <summary>Fits a line to 2D points using OpenCV fitLine. Returns false if fewer than 2 points.</summary>
        public static bool FitPointsToLine2D(CxPoint2D[] points, out CxLine2D line)
        {
            line = default;
            if (points == null || points.Length < 2) return false;

            var pts = new Mat(points.Length, 2, MatType.CV_32F);
            for (int i = 0; i < points.Length; i++)
            {
                pts.Set(i, 0, points[i].X);
                pts.Set(i, 1, points[i].Y);
            }

            var lineOut = new Mat(4, 1, MatType.CV_32F);
            Cv2.FitLine(pts, lineOut, DistanceTypes.L2, 0, 0.01, 0.01);

            line = new CxLine2D(
                new CxPoint2D(lineOut.At<float>(2), lineOut.At<float>(3)),
                new CxVector2D(lineOut.At<float>(0), lineOut.At<float>(1)));
            return true;
        }

        // ── 3D Fitting ─────────────────────────────────────────────────────────

        /// <summary>Fits a line to 3D points using OpenCV fitLine. Returns false if fewer than 2 points.</summary>
        public static bool FitPointsToLine3D(CxPoint3D[] points, out CxLine3D line)
        {
            line = default;
            if (points == null || points.Length < 2) return false;

            var pts = new Mat(points.Length, 3, MatType.CV_32F);
            for (int i = 0; i < points.Length; i++)
            {
                pts.Set(i, 0, points[i].X);
                pts.Set(i, 1, points[i].Y);
                pts.Set(i, 2, points[i].Z);
            }

            var lineOut = new Mat(6, 1, MatType.CV_32F);
            Cv2.FitLine(pts, lineOut, DistanceTypes.L2, 0, 0.01, 0.01);

            line = new CxLine3D(
                new CxPoint3D(lineOut.At<float>(3), lineOut.At<float>(4), lineOut.At<float>(5)),
                new CxVector3D(lineOut.At<float>(0), lineOut.At<float>(1), lineOut.At<float>(2)));
            return true;
        }

        /// <summary>Fits a plane to 3D points using OpenCV PCA. Returns false if fewer than 3 points or degenerate.</summary>
        public static bool FitPointsToPlane(CxPoint3D[] points, out CxPlane3D plane)
        {
            plane = default;
            if (points == null || points.Length < 3) return false;

            int n = points.Length;
            var data = new Mat(n, 3, MatType.CV_32F);
            for (int i = 0; i < n; i++)
            {
                data.Set(i, 0, points[i].X);
                data.Set(i, 1, points[i].Y);
                data.Set(i, 2, points[i].Z);
            }

            var mean = new Mat();
            var eigenvectors = new Mat();
            Cv2.PCACompute(data, mean, eigenvectors, 3);

            float nx = eigenvectors.At<float>(2, 0);
            float ny = eigenvectors.At<float>(2, 1);
            float nz = eigenvectors.At<float>(2, 2);
            float nLen = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (nLen < 1e-10f) return false;

            float inv = 1f / nLen;
            plane = new CxPlane3D(
                new CxPoint3D(mean.At<float>(0, 0), mean.At<float>(0, 1), mean.At<float>(0, 2)),
                new CxVector3D(nx * inv, ny * inv, nz * inv));
            return true;
        }

        /// <summary>Fits a sphere to 3D points using algebraic least squares. Returns false if fewer than 4 points or degenerate.</summary>
        public static bool FitSphere(CxPoint3D[] points, out CxSphere sphere)
        {
            sphere = default;
            if (points == null || points.Length < 4) return false;

            int n = points.Length;

            double sx = 0, sy = 0, sz = 0;
            double sxx = 0, sxy = 0, sxz = 0, syy = 0, syz = 0, szz = 0;
            double sr2 = 0, sr2x = 0, sr2y = 0, sr2z = 0;

            for (int i = 0; i < n; i++)
            {
                double x = points[i].X, y = points[i].Y, z = points[i].Z;
                double r2 = x * x + y * y + z * z;
                sx += x; sy += y; sz += z;
                sxx += x * x; sxy += x * y; sxz += x * z;
                syy += y * y; syz += y * z; szz += z * z;
                sr2 += r2; sr2x += x * r2; sr2y += y * r2; sr2z += z * r2;
            }

            var A = new Mat(4, 4, MatType.CV_32F);
            var B = new Mat(4, 1, MatType.CV_32F);

            A.Set(0, 0, (float)n);    A.Set(0, 1, (float)sx);   A.Set(0, 2, (float)sy);   A.Set(0, 3, (float)sz);
            A.Set(1, 0, (float)sx);   A.Set(1, 1, (float)sxx);  A.Set(1, 2, (float)sxy);  A.Set(1, 3, (float)sxz);
            A.Set(2, 0, (float)sy);   A.Set(2, 1, (float)sxy);  A.Set(2, 2, (float)syy);  A.Set(2, 3, (float)syz);
            A.Set(3, 0, (float)sz);   A.Set(3, 1, (float)sxz);  A.Set(3, 2, (float)syz);  A.Set(3, 3, (float)szz);
            B.Set(0, 0, (float)sr2);  B.Set(1, 0, (float)sr2x); B.Set(2, 0, (float)sr2y); B.Set(3, 0, (float)sr2z);

            var X = new Mat();
            if (!Cv2.Solve(A, B, X, DecompTypes.LU))
            {
                sphere = default;
                return false;
            }

            float a = X.At<float>(0, 0), bc = X.At<float>(1, 0), cc = X.At<float>(2, 0), d = X.At<float>(3, 0);
            float cx = -a * 0.5f, cy = -bc * 0.5f, cz = -cc * 0.5f;
            float radiusSq = cx * cx + cy * cy + cz * cz - d;

            if (radiusSq < 0)
            {
                sphere = default;
                return false;
            }

            sphere = new CxSphere(
                new CxPoint3D(cx, cy, cz),
                (float)Math.Sqrt(radiusSq));
            return true;
        }
    }
}
