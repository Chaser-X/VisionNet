using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
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
        public static Box3D? CalculateBoundingBox(CxPoint3D[] points)
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

            return new Box3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        /// <summary>
        /// Computes the axis-aligned bounding box of an array of 3D points using
        /// <see cref="Vector3.Min"/> / <see cref="Vector3.Max"/> SIMD instructions.
        /// Returns <c>null</c> if <paramref name="points"/> is empty.
        /// </summary>
        public static Box3D? CalculateBoundingBoxSIMD(CxPoint3D[] points)
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

            return new Box3D(
                new CxPoint3D((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2),
                new CxSize3D(max.X - min.X, max.Y - min.Y, max.Z - min.Z));
        }

        // ── 预留扩展 ──────────────────────────────────────────────────────────
        // FindCircle   圆拟合（RANSAC）
        // FindEdges    边缘检测
        // FitPlane     平面拟合
        // FitSphere    球拟合
        // MeasureVolume 体积计算
        // MeasureArea   面积计算
    }
}
