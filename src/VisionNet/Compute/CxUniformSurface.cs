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
        protected override string GetKernelSource() => @"
            __kernel void UniformSurfaceSample(
                __global const float* points,       // interleaved X,Y,Z (3 floats per point)
                __global const uchar* intensity,
                int   pointCount,
                int   width,
                int   height,
                float xScale,
                float yScale,
                float zScale,
                float xOffset,
                float yOffset,
                float zOffset,
                __global int* heightMap,            // aggregated Z (int, atomic)
                __global int* intensityMap,         // aggregated intensity (int, atomic)
                __global int* pointCountMap,        // number of points per cell
                int   inMode)                       // 0=Max, 1=Min, 2=Average
            {
                int gid = get_global_id(0);
                float px = points[gid * 3 + 0];
                float py = points[gid * 3 + 1];
                float pz = points[gid * 3 + 2];
                if (isnan(pz) || isinf(pz)) return;
                int xIdx = (int)((px - xOffset) / xScale);
                int yIdx = (int)((py - yOffset) / yScale);
                if (xIdx < 0 || xIdx >= width || yIdx < 0 || yIdx >= height) return;
                int scaledZ = (int)((pz - zOffset) / zScale);
                int idx     = yIdx * width + xIdx;
                int inten   = intensity ? (int)intensity[gid] : 0;
                switch (inMode)
                {
                    case 0:  // Max
                    {
                        int old = atomic_max(&heightMap[idx], scaledZ);
                        if (scaledZ >= old) intensityMap[idx] = inten;
                        break;
                    }
                    case 1:  // Min
                    {
                        int old = atomic_min(&heightMap[idx], scaledZ);
                        if (scaledZ < old) intensityMap[idx] = inten;
                        break;
                    }
                    default: // Average
                        atomic_add(&heightMap[idx], scaledZ);
                        atomic_add(&intensityMap[idx], inten);
                        break;
                }
                atomic_inc(&pointCountMap[idx]);
            }";

        /// <summary>
        /// Re-samples <paramref name="points"/> onto a <paramref name="width"/> × <paramref name="height"/>
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
        /// <exception cref="Exception">Thrown if the OpenCL environment could not be initialised.</exception>
        public CxSurface Sample(CxPoint3D[] points, byte[] intensity,
            int width, int height,
            float xScale, float yScale, float zScale,
            float xOffset, float yOffset, float zOffset,
            SampleMode sampleMode = SampleMode.Average)
        {
            int count      = points.Length;
            int cellCount  = width * height;

            // Initialise height-map sentinel values for max/min modes.
            int[] heightMap      = new int[cellCount];
            int[] intensityMap   = new int[cellCount];
            int[] pointCountMap  = new int[cellCount];

            if (sampleMode == SampleMode.Max)
                Parallel.For(0, cellCount, i => heightMap[i] = int.MinValue);
            else if (sampleMode == SampleMode.Min)
                Parallel.For(0, cellCount, i => heightMap[i] = int.MaxValue);

            // Flatten CxPoint3D[] → float[] (X,Y,Z interleaved) in parallel.
            float[] pointData = new float[count * 3];
            Parallel.For(0, count, i =>
            {
                pointData[i * 3]     = points[i].X;
                pointData[i * 3 + 1] = points[i].Y;
                pointData[i * 3 + 2] = points[i].Z;
            });

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            // Allocate GPU buffers.
            var pointsBuf    = CreateBuffer<float>(MemFlags.ReadOnly  | MemFlags.CopyHostPtr, pointData);
            var intensityBuf = CreateBuffer<byte> (MemFlags.ReadOnly  | MemFlags.CopyHostPtr,
                intensity ?? new byte[count]);
            var heightBuf    = CreateBuffer<int>  (MemFlags.ReadWrite | MemFlags.CopyHostPtr, heightMap);
            var intensMapBuf = CreateBufferWithSize<int>(MemFlags.WriteOnly, cellCount);
            var countBuf     = CreateBufferWithSize<int>(MemFlags.WriteOnly, cellCount);

            // Bind kernel arguments.
            bool ok = true;
            ok &= SetKernelArg(KernelName,  0, pointsBuf);
            ok &= SetKernelArg(KernelName,  1, intensityBuf);
            ok &= SetKernelArg(KernelName,  2, count);
            ok &= SetKernelArg(KernelName,  3, width);
            ok &= SetKernelArg(KernelName,  4, height);
            ok &= SetKernelArg(KernelName,  5, xScale);
            ok &= SetKernelArg(KernelName,  6, yScale);
            ok &= SetKernelArg(KernelName,  7, zScale);
            ok &= SetKernelArg(KernelName,  8, xOffset);
            ok &= SetKernelArg(KernelName,  9, yOffset);
            ok &= SetKernelArg(KernelName, 10, zOffset);
            ok &= SetKernelArg(KernelName, 11, heightBuf);
            ok &= SetKernelArg(KernelName, 12, intensMapBuf);
            ok &= SetKernelArg(KernelName, 13, countBuf);
            ok &= SetKernelArg(KernelName, 14, (int)sampleMode);

            ok &= ExecuteKernel(KernelName, new[] { new IntPtr(count) });
            ok &= ReadBuffer(heightBuf,    heightMap);
            ok &= ReadBuffer(intensMapBuf, intensityMap);
            ok &= ReadBuffer(countBuf,     pointCountMap);

            Cleanup();

            if (!ok)
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            // Build the output CxSurface from the aggregated maps.
            short[] data            = new short[cellCount];
            byte[]  intensityOutput = new byte[cellCount];

            for (int i = 0; i < cellCount; i++)
            {
                if (pointCountMap[i] == 0)
                {
                    data[i] = short.MinValue;
                    continue;
                }

                if (sampleMode == SampleMode.Average)
                {
                    float avgZ = heightMap[i] / (float)pointCountMap[i];
                    data[i] = avgZ < short.MinValue ? short.MinValue : (short)avgZ;
                    if (intensity != null)
                    {
                        float avgI = intensityMap[i] / (float)pointCountMap[i];
                        intensityOutput[i] = (byte)Math.Max(0, Math.Min(255, (int)avgI));
                    }
                }
                else
                {
                    data[i] = heightMap[i] < short.MinValue ? short.MinValue : (short)heightMap[i];
                    if (intensity != null)
                        intensityOutput[i] = (byte)Math.Max(0, Math.Min(255, intensityMap[i]));
                }
            }

            return new CxSurface(width, height, data,
                intensity != null ? intensityOutput : new byte[0],
                xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }
    }
}
