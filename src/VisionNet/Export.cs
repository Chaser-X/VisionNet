using System.Runtime.InteropServices;
using VisionNet.DataType;

namespace VisionNet
{
    public static partial class VisionOperator
    {
        /// <summary>
        /// Computes the centroid of an array of 3D points using the native VisionLib library.
        /// </summary>
        /// <param name="pts">Input point array.</param>
        /// <param name="size">Number of points in <paramref name="pts"/>.</param>
        /// <param name="outCenter">Receives the computed centroid.</param>
        /// <returns>0 on success, −1 if the input is empty or invalid.</returns>
        [DllImport("VisionLib", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetCenter(CxPoint3D[] pts, int size, ref CxPoint3D outCenter);

        /// <summary>
        /// Re-samples an unordered point cloud onto a uniform (X, Y) grid using the native library.
        /// For each grid cell the highest Z value (height) is retained.
        /// </summary>
        /// <param name="points">Input world-space point positions.</param>
        /// <param name="intensities">Per-point intensity values, or <c>null</c>.</param>
        /// <param name="size">Number of points in <paramref name="points"/>.</param>
        /// <param name="xScale">Grid cell width along X.</param>
        /// <param name="yScale">Grid cell height along Y.</param>
        /// <param name="xMin">Left boundary of the output grid.</param>
        /// <param name="xMax">Right boundary of the output grid.</param>
        /// <param name="yMin">Bottom boundary of the output grid.</param>
        /// <param name="yMax">Top boundary of the output grid.</param>
        /// <param name="heightMap">Output height array, one element per grid cell.</param>
        /// <param name="intensityMap">Output intensity array, one element per grid cell.</param>
        /// <param name="mapSize">Receives the number of valid cells written to the output arrays.</param>
        [DllImport("VisionLib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UniformGridSample(
            [In]  CxPoint3D[] points,
                  byte[]      intensities,
                  int         size,
                  float       xScale,
                  float       yScale,
                  float       xMin,
                  float       xMax,
                  float       yMin,
                  float       yMax,
            [Out] float[]     heightMap,
                  byte[]      intensityMap,
            out   int         mapSize);
    }
}
