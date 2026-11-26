using System;
using VisionNet.Compute;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Coordinate transformation and registration operations.
    /// </summary>
    public static partial class VisionOperator
    {
        /// <summary>
        /// Transforms a 3D point by a 4×4 row-major transformation matrix.
        /// Performs perspective divide when the homogeneous <c>w</c> component is non-zero.
        /// Semantics match <see cref="CxMatrix4X4.TransformPoint3D"/> and the OpenCL
        /// transform kernels (<c>M · v</c>, translation in elements 3/7/11).
        /// </summary>
        public static CxPoint3D TransformPoint3D(CxPoint3D point, CxMatrix4X4 matrix)
        {
            // Keep single-point transforms consistent with the matrix implementation.
            return matrix.TransformPoint3D(point);
        }

        /// <summary>
        /// Applies a 4×4 transformation matrix to a <see cref="CxSurface"/> on the GPU
        /// (OpenCL) and re-grids the result back into a new <see cref="CxSurface"/>.
        /// </summary>
        public static CxSurface TransformSurface(CxSurface surface, CxMatrix4X4 matrix,
            float xScale = 0.01f, float yScale = 0.01f,
            SampleMode sampleMode = SampleMode.Average)
        {
            if (surface == null || matrix == null) return null;
            if (xScale <= 0 || yScale <= 0) return null;

            CxPoint3D[] transformedPoints;
            byte[]      intensities;
            using (var transformer = new CxTransformSurface(matrix))
            {
                (transformedPoints, intensities) = transformer.Transform(surface);
            }

            if (transformedPoints == null || transformedPoints.Length == 0) return null;

            var box = CalculateBoundingBoxSIMD(transformedPoints);
            if (box == null) return null;

            int   width   = (int)Math.Ceiling(box.Value.Size.Width  / xScale);
            int   height  = (int)Math.Ceiling(box.Value.Size.Height / yScale);
            float xOffset = box.Value.Center.X - width  * xScale / 2f;
            float yOffset = box.Value.Center.Y - height * yScale / 2f;
            float zOffset = box.Value.Center.Z;
            float zScale  = Math.Max(box.Value.Size.Depth / ushort.MaxValue, 1e-6f);

            using (var sampler = new CxUniformSurface())
            {
                return sampler.Sample(
                    transformedPoints, intensities,
                    width, height,
                    xScale, yScale, zScale,
                    xOffset, yOffset, zOffset,
                    sampleMode);
            }
        }

        /// <summary>
        /// Applies a 4×4 transformation matrix to an ordered point cloud on the GPU.
        /// </summary>
        public static (CxPoint3D[] Points, byte[] Intensities) TransformPointCloud(
            CxPointCloud cloud, CxMatrix4X4 matrix)
        {
            if (cloud == null || matrix == null) return (null, null);

            using (var transformer = new CxTransformPointCloud(matrix))
                return transformer.Transform(cloud);
        }

        // ── 预留扩展 ──────────────────────────────────────────────────────────
        // ICP 精配准
        // NDT 配准
        // 手眼标定
    }
}
