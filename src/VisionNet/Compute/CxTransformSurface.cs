using OpenCL.Net;
using System;
using System.Runtime.InteropServices;
using VisionNet.DataType;

namespace VisionNet.Compute
{
    /// <summary>
    /// GPU-accelerated surface transformation: applies a 4×4 matrix to every valid point
    /// of a <see cref="CxSurface"/> height map on the OpenCL device and returns the
    /// resulting point array for further processing.
    /// </summary>
    public class CxTransformSurface : OpenCLComputation
    {
        private const string KernelName = "TransformSurface";

        private readonly CxMatrix4X4 _matrix;

        /// <summary>
        /// Initialises the computation with the transformation matrix to apply.
        /// </summary>
        /// <param name="matrix">4×4 column-major transformation matrix (OpenGL convention).</param>
        public CxTransformSurface(CxMatrix4X4 matrix) : base("CxTransformSurfaceProgram")
        {
            _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        }

        /// <inheritdoc/>
        protected override string[] GetKernelNames() => new[] { KernelName };

        /// <inheritdoc/>
        protected override string GetKernelSource() => @"
            __kernel void TransformSurface(
                __global const short* data,     // height map (Width X Length), -32768 = invalid
                __global float*       dstPoints,// output float4 per cell (x, y, z, w)
                int   width,
                int   length,
                float xOffset, float yOffset, float zOffset,
                float xScale,  float yScale,  float zScale,
                __global const float* matrix)   // 16-element column-major 4x4 matrix
            {
                int gid = get_global_id(0);
                int x   = gid % width;
                int y   = gid / width;
                if (x >= width || y >= length) return;
                int idx = y * width + x;
                float px = x * xScale + xOffset;
                float py = y * yScale + yOffset;
                float pz = zOffset + data[idx] * zScale;
                float4 p = (float4)(px, py, pz, 1.0f);
                float4 r;
                r.x = matrix[0]*p.x + matrix[1]*p.y + matrix[2]*p.z  + matrix[3]*p.w;
                r.y = matrix[4]*p.x + matrix[5]*p.y + matrix[6]*p.z  + matrix[7]*p.w;
                r.z = matrix[8]*p.x + matrix[9]*p.y + matrix[10]*p.z + matrix[11]*p.w;
                r.w = matrix[12]*p.x+ matrix[13]*p.y+ matrix[14]*p.z + matrix[15]*p.w;
                dstPoints[gid * 4]     = r.x;
                dstPoints[gid * 4 + 1] = r.y;
                dstPoints[gid * 4 + 2] = data[idx] == -32768 ? NAN : r.z;
                dstPoints[gid * 4 + 3] = r.w;
            }";

        /// <summary>
        /// Transforms all valid cells of <paramref name="surface"/> by the matrix supplied
        /// at construction time and returns the resulting point cloud.
        /// </summary>
        /// <param name="surface">Source height-map surface.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><c>TransformedPoints</c> — world-space positions of all valid cells after transformation.</item>
        ///   <item><c>Intensities</c> — corresponding intensity values, or <c>null</c> if the source had none.</item>
        /// </list>
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the OpenCL context could not be initialised.</exception>
        public (CxPoint3D[] TransformedPoints, byte[] Intensities) Transform(CxSurface surface)
        {
            int width    = surface.Width;
            int length   = surface.Length;
            int count    = width * length;
            var data      = surface.Data;
            var intensity = surface.Intensity;
            bool hasIntensity = intensity != null && intensity.Length == count;

            float[] dst = new float[count * 4];

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            var dataBuf   = CreateBuffer<short>(MemFlags.ReadOnly  | MemFlags.CopyHostPtr, data);
            var matrixBuf = CreateBuffer<float>(MemFlags.ReadOnly  | MemFlags.CopyHostPtr, _matrix.Data);
            var dstBuf    = CreateBufferWithSize<float>(MemFlags.WriteOnly, count * 4);

            bool ok = true;
            ok &= SetKernelArg(KernelName,  0, dataBuf);
            ok &= SetKernelArg(KernelName,  1, dstBuf);
            ok &= SetKernelArg(KernelName,  2, width);
            ok &= SetKernelArg(KernelName,  3, length);
            ok &= SetKernelArg(KernelName,  4, surface.XOffset);
            ok &= SetKernelArg(KernelName,  5, surface.YOffset);
            ok &= SetKernelArg(KernelName,  6, surface.ZOffset);
            ok &= SetKernelArg(KernelName,  7, surface.XScale);
            ok &= SetKernelArg(KernelName,  8, surface.YScale);
            ok &= SetKernelArg(KernelName,  9, surface.ZScale);
            ok &= SetKernelArg(KernelName, 10, matrixBuf);

            ok &= ExecuteKernel(KernelName, new[] { new IntPtr(count) });
            ok &= ReadBuffer(dstBuf, dst);

            Cleanup();

            if (!ok)
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            // Compact: filter out invalid (NaN/Inf z) cells.
            var tempPoints    = new CxPoint3D[count];
            var tempIntensity = new byte[count];
            int validCount    = 0;

            for (int i = 0; i < count; i++)
            {
                float z = dst[i * 4 + 2];
                if (float.IsNaN(z) || float.IsInfinity(z)) continue;

                float x = dst[i * 4 + 0];
                float y = dst[i * 4 + 1];
                float w = dst[i * 4 + 3];
                if (Math.Abs(w) > 1e-6f) { x /= w; y /= w; z /= w; }

                tempPoints[validCount] = new CxPoint3D(x, y, z);
                if (hasIntensity)
                    tempIntensity[validCount] = intensity[i];
                validCount++;
            }

            if (validCount == count)
                return (tempPoints, hasIntensity ? tempIntensity : null);

            var result    = new CxPoint3D[validCount];
            var resultI   = new byte[validCount];
            Array.Copy(tempPoints,    result,  validCount);
            if (hasIntensity) Array.Copy(tempIntensity, resultI, validCount);

            return (result, hasIntensity ? resultI : null);
        }
    }
}
