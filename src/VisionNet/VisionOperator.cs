using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Provides static vision-processing operations such as point-cloud sampling,
    /// coordinate-frame transformations, and center-of-mass calculation.
    /// </summary>
    public static partial class VisionOperator
    {
        /// <summary>
        /// Computes the centroid of a list of 3D points using the native library.
        /// Returns <see cref="float.NegativeInfinity"/> in all axes if the list is empty.
        /// </summary>
        /// <param name="point3Ds">Input points.</param>
        /// <returns>Centroid point, or <c>(−∞, −∞, −∞)</c> on failure.</returns>
        public static CxPoint3D GetPoint3DArrayCenter(List<CxPoint3D> point3Ds)
        {
            CxPoint3D center = new CxPoint3D();
            int ret = GetCenter(point3Ds.ToArray(), point3Ds.Count, ref center);
            if (ret == -1)
                center = new CxPoint3D(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            return center;
        }

        /// <summary>
        /// Re-samples a point cloud onto a uniform grid and returns it as a <see cref="CxSurface"/>.
        /// Useful for down-sampling large unordered clouds to a structured representation.
        /// </summary>
        /// <param name="points">Input point positions.</param>
        /// <param name="intensity">Per-point intensity values, or <c>null</c>.</param>
        /// <param name="width">Output grid width (number of columns).</param>
        /// <param name="height">Output grid height (number of rows).</param>
        /// <param name="xScale">Grid spacing along X.</param>
        /// <param name="yScale">Grid spacing along Y.</param>
        /// <param name="zScale">Scale factor applied to Z values when encoding as shorts.</param>
        /// <param name="xOffset">World-space X origin of the output grid.</param>
        /// <param name="yOffset">World-space Y origin of the output grid.</param>
        /// <param name="zOffset">World-space Z origin of the output grid.</param>
        /// <returns>A structured <see cref="CxSurface"/> representing the resampled cloud.</returns>
        public static CxSurface UniformSurface(CxPoint3D[] points, byte[] intensity,
            int width, int height,
            float xScale, float yScale, float zScale,
            float xOffset, float yOffset, float zOffset)
        {
            int gridSize = width * height;
            float[] heightMap    = new float[gridSize];
            byte[]  intensityMap = intensity != null ? new byte[gridSize] : new byte[0];

            UniformGridSample(points, intensity, points.Length,
                xScale, yScale,
                xOffset, xOffset + width  * xScale,
                yOffset, yOffset + height * yScale,
                heightMap, intensityMap, out int mapSize);

            short[] heightData = new short[mapSize];
            for (int i = 0; i < mapSize; i++)
            {
                if (float.IsInfinity(heightMap[i]) || float.IsNaN(heightMap[i]))
                    heightData[i] = -32768;
                else
                    heightData[i] = (short)((heightMap[i] - zOffset) / zScale);
            }

            return new CxSurface(width, height, heightData, intensityMap,
                xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }

        /// <summary>
        /// Transforms a 3D point by a 4×4 column-major matrix (OpenGL convention).
        /// Performs perspective divide when the homogeneous <c>w</c> component is non-zero.
        /// </summary>
        /// <param name="point">Point to transform.</param>
        /// <param name="matrix">4×4 column-major transformation matrix.</param>
        /// <returns>Transformed point in world/clip space.</returns>
        public static CxPoint3D TransformPoint3D(CxPoint3D point, CxMatrix4X4 matrix)
        {
            float[] m = matrix.Data;
            float x = point.X, y = point.Y, z = point.Z;

            // Column-major order (OpenGL convention).
            float tx = m[0] * x + m[4] * y + m[8]  * z + m[12];
            float ty = m[1] * x + m[5] * y + m[9]  * z + m[13];
            float tz = m[2] * x + m[6] * y + m[10] * z + m[14];
            float tw = m[3] * x + m[7] * y + m[11] * z + m[15];

            if (Math.Abs(tw) > 1e-6f)
            {
                tx /= tw;
                ty /= tw;
                tz /= tw;
            }
            return new CxPoint3D(tx, ty, tz);
        }
    }
}
