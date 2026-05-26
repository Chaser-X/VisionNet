using System;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Filtering and sampling operations (e.g. uniform grid resampling).
    /// </summary>
    public static partial class VisionOperator
    {
        /// <summary>
        /// Re-samples an unordered point cloud onto a uniform (X, Y) grid using the native
        /// library and returns a structured <see cref="CxSurface"/>.
        /// </summary>
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
                heightData[i] = (float.IsInfinity(heightMap[i]) || float.IsNaN(heightMap[i]))
                    ? (short)-32768
                    : (short)((heightMap[i] - zOffset) / zScale);
            }

            return new CxSurface(width, height, heightData, intensityMap,
                xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }

        // ── 预留扩展 ──────────────────────────────────────────────────────────
        // PassThrough 直通滤波
        // VoxelGrid 体素格下采样
        // StatisticalOutlierRemoval 统计离群点去除
        // RadiusOutlierRemoval 半径离群点去除
    }
}
