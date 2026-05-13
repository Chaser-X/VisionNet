using OpenCL.Net;
using System;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Compute
{
    /// <summary>
    /// GPU-accelerated uniform-grid re-sampling of an unordered point cloud.
    /// Replaces the CPU-bound native <c>UniformGridSample</c> P/Invoke for large inputs.
    /// <para>
    /// The OpenCL kernel processes each point in parallel and uses atomic operations
    /// to aggregate multiple points that fall into the same grid cell.
    /// The post-processing pass (average normalisation and short encoding) runs in
    /// parallel on the CPU via <see cref="Parallel.For"/>.
    /// </para>
    /// </summary>
    public class CxUniformSurface : OpenCLComputation
    {
        private const string KernelName = "UniformSurfaceSample";

        /// <summary>Initialises the computation with its fixed program identifier.</summary>
        public CxUniformSurface() : base("CxUniformSurfaceProgram") { }

        /// <inheritdoc/>
        protected override string[] GetKernelNames() => new[] { KernelName };

        /// <inheritdoc/>
        protected override string GetKernelSource() =>
            LoadEmbeddedResource("VisionNet.Compute.Kernels.UniformSurface.cl");

        /// <summary>
        /// Re-samples <paramref name="points"/> onto a <paramref name="width"/> x <paramref name="height"/>
        /// uniform grid and returns the result as a <see cref="CxSurface"/>.
        /// </summary>
        /// <param name="points">Unordered world-space point positions.</param>
        /// <param name="intensity">Per-point intensity values, or <c>null</c>.</param>
        /// <param name="width">Output grid column count.</param>
        /// <param name="height">Output grid row count.</param>
        /// <param name="xScale">Cell width along X.</param>
        /// <param name="yScale">Cell height along Y.</param>
        /// <param name="zScale">Z encoding scale factor.</param>
        /// <param name="xOffset">World-space X origin of the output grid.</param>
        /// <param name="yOffset">World-space Y origin of the output grid.</param>
        /// <param name="zOffset">World-space Z origin of the output grid.</param>
        /// <param name="sampleMode">Cell aggregation strategy (Max / Min / Average).</param>
        /// <exception cref="InvalidOperationException">Thrown if the OpenCL environment could not be initialised.</exception>
        public CxSurface Sample(CxPoint3D[] points, byte[] intensity,
            int width, int height,
            float xScale, float yScale, float zScale,
            float xOffset, float yOffset, float zOffset,
            SampleMode sampleMode = SampleMode.Average)
        {
            int count     = points.Length;
            int cellCount = width * height;

            // Initialise height-map sentinel values for max/min modes.
            int[] heightMap     = new int[cellCount];
            int[] intensityMap  = new int[cellCount];
            int[] pointCountMap = new int[cellCount];

            if (sampleMode == SampleMode.Max)
                Parallel.For(0, cellCount, i => heightMap[i] = int.MinValue);
            else if (sampleMode == SampleMode.Min)
                Parallel.For(0, cellCount, i => heightMap[i] = int.MaxValue);

            // Flatten CxPoint3D[] to float[] (X,Y,Z interleaved) in parallel.
            float[] pointData = new float[count * 3];
            Parallel.For(0, count, i =>
            {
                pointData[i * 3]     = points[i].X;
                pointData[i * 3 + 1] = points[i].Y;
                pointData[i * 3 + 2] = points[i].Z;
            });

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            // Allocate GPU buffers (all transient: released in ReleaseTransient below).
            // intensityMap and pointCountMap use ReadWrite because atomic operations
            // perform read-modify-write on these buffers.
            var pointsBuf    = AllocateTransient<float>(MemFlags.ReadOnly  | MemFlags.CopyHostPtr, pointData);
            var intensityBuf = AllocateTransient<byte> (MemFlags.ReadOnly  | MemFlags.CopyHostPtr,
                intensity ?? new byte[count]);
            var heightBuf    = AllocateTransient<int>  (MemFlags.ReadWrite | MemFlags.CopyHostPtr, heightMap);
            var intensMapBuf = AllocateTransient<int>  (MemFlags.ReadWrite | MemFlags.CopyHostPtr, new int[cellCount]);
            var countBuf     = AllocateTransient<int>  (MemFlags.ReadWrite | MemFlags.CopyHostPtr, new int[cellCount]);

            bool ok = SetKernelArgs(KernelName,
                pointsBuf, intensityBuf, count, width, height,
                xScale, yScale, zScale, xOffset, yOffset, zOffset,
                heightBuf, intensMapBuf, countBuf, (int)sampleMode, 3);

            ok &= ExecuteKernel(KernelName, new[] { new IntPtr(count) });
            ok &= ReadBuffer(heightBuf,    heightMap);
            ok &= ReadBuffer(intensMapBuf, intensityMap);
            ok &= ReadBuffer(countBuf,     pointCountMap);

            ReleaseTransient();

            if (!ok)
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            return BuildSurface(heightMap, intensityMap, pointCountMap,
                width, height, xOffset, yOffset, zOffset, xScale, yScale, zScale,
                intensity, sampleMode);
        }

        /// <summary>
        /// Re-samples a GPU-resident point buffer (produced by a preceding GPU computation,
        /// e.g., <see cref="CxTransformSurface.TransformToBuffer"/>) directly without CPU
        /// readback of the point data.
        /// </summary>
        /// <param name="pointsBuf">
        /// GPU buffer containing point data. Must contain <paramref name="stride"/> floats
        /// per point: 3 for XYZ (float3), or 4 for XYZW (float4 pipeline output).
        /// </param>
        /// <param name="stride">Floats per point in <paramref name="pointsBuf"/>: 3 or 4.</param>
        /// <param name="pointCount">Number of points in the buffer.</param>
        /// <param name="width">Output grid column count.</param>
        /// <param name="height">Output grid row count.</param>
        /// <param name="xScale">Cell width along X.</param>
        /// <param name="yScale">Cell height along Y.</param>
        /// <param name="zScale">Z encoding scale factor.</param>
        /// <param name="xOffset">World-space X origin of the output grid.</param>
        /// <param name="yOffset">World-space Y origin of the output grid.</param>
        /// <param name="zOffset">World-space Z origin of the output grid.</param>
        /// <param name="sampleMode">Cell aggregation strategy.</param>
        /// <exception cref="InvalidOperationException">Thrown if the OpenCL environment could not be initialised.</exception>
        internal CxSurface Sample(IMem pointsBuf, int stride, int pointCount,
            int width, int height,
            float xScale, float yScale, float zScale,
            float xOffset, float yOffset, float zOffset,
            SampleMode sampleMode = SampleMode.Average)
        {
            int cellCount = width * height;

            int[] heightMap     = new int[cellCount];
            int[] intensityMap  = new int[cellCount];
            int[] pointCountMap = new int[cellCount];

            if (sampleMode == SampleMode.Max)
                Parallel.For(0, cellCount, i => heightMap[i] = int.MinValue);
            else if (sampleMode == SampleMode.Min)
                Parallel.For(0, cellCount, i => heightMap[i] = int.MaxValue);

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            // pointsBuf is external (owned by the upstream CxTransformSurface) — not tracked here.
            // Intensity is not available in the GPU pipeline path.
            var intensityBuf = AllocateTransient<byte>(MemFlags.ReadOnly | MemFlags.CopyHostPtr, new byte[pointCount]);
            var heightBuf    = AllocateTransient<int>  (MemFlags.ReadWrite | MemFlags.CopyHostPtr, heightMap);
            var intensMapBuf = AllocateTransient<int>  (MemFlags.ReadWrite | MemFlags.CopyHostPtr, new int[cellCount]);
            var countBuf     = AllocateTransient<int>  (MemFlags.ReadWrite | MemFlags.CopyHostPtr, new int[cellCount]);

            bool ok = SetKernelArgs(KernelName,
                pointsBuf, intensityBuf, pointCount, width, height,
                xScale, yScale, zScale, xOffset, yOffset, zOffset,
                heightBuf, intensMapBuf, countBuf, (int)sampleMode, stride);

            ok &= ExecuteKernel(KernelName, new[] { new IntPtr(pointCount) });
            ok &= ReadBuffer(heightBuf,    heightMap);
            ok &= ReadBuffer(intensMapBuf, intensityMap);
            ok &= ReadBuffer(countBuf,     pointCountMap);

            ReleaseTransient();

            if (!ok)
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            // Intensity is not propagated through the GPU pipeline path.
            return BuildSurface(heightMap, intensityMap, pointCountMap,
                width, height, xOffset, yOffset, zOffset, xScale, yScale, zScale,
                intensity: null, sampleMode);
        }

        /// <summary>
        /// Builds a <see cref="CxSurface"/> from the aggregated GPU output maps.
        /// Runs in parallel across all grid cells.
        /// </summary>
        private static CxSurface BuildSurface(
            int[] heightMap, int[] intensityMap, int[] pointCountMap,
            int width, int height,
            float xOffset, float yOffset, float zOffset,
            float xScale, float yScale, float zScale,
            byte[] intensity, SampleMode sampleMode)
        {
            int cellCount = width * height;
            short[] data            = new short[cellCount];
            byte[]  intensityOutput = new byte[cellCount];

            Parallel.For(0, cellCount, i =>
            {
                if (pointCountMap[i] == 0)
                {
                    data[i] = short.MinValue;
                    return;
                }

                if (sampleMode == SampleMode.Average)
                {
                    float avgZ = heightMap[i] / (float)pointCountMap[i];
                    data[i] = avgZ < short.MinValue ? short.MinValue
                           : avgZ > short.MaxValue ? short.MaxValue
                           : (short)avgZ;
                    if (intensity != null)
                    {
                        float avgI = intensityMap[i] / (float)pointCountMap[i];
                        intensityOutput[i] = (byte)Math.Max(0, Math.Min(255, (int)avgI));
                    }
                }
                else
                {
                    data[i] = heightMap[i] < short.MinValue ? short.MinValue
                           : heightMap[i] > short.MaxValue ? short.MaxValue
                           : (short)heightMap[i];
                    if (intensity != null)
                        intensityOutput[i] = (byte)Math.Max(0, Math.Min(255, intensityMap[i]));
                }
            });

            return new CxSurface(width, height, data,
                intensity != null ? intensityOutput : new byte[0],
                xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }
    }
}
