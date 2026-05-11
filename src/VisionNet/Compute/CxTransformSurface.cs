using OpenCL.Net;
using System;
using System.Runtime.InteropServices;
using VisionNet.DataType;

namespace VisionNet.Compute
{
    /// <summary>
    /// GPU-accelerated surface transformation: applies a 4×4 matrix to every valid point
    /// of a <see cref="CxSurface"/> height map on the OpenCL device.
    /// <para>
    /// The transformation matrix is uploaded to GPU memory once on the first call and
    /// retained as a persistent buffer for the lifetime of this instance, avoiding
    /// redundant host-to-device transfers across repeated calls with the same matrix.
    /// </para>
    /// </summary>
    public class CxTransformSurface : OpenCLComputation
    {
        private const string KernelName = "TransformSurface";

        private readonly CxMatrix4X4 _matrix;
        private readonly object _matrixLock = new object();
        private IMem _matrixBuf;    // persistent: uploaded once, reused across Transform() calls

        /// <summary>
        /// Initialises the computation with the transformation matrix to apply.
        /// </summary>
        /// <param name="matrix">4×4 row-major transformation matrix.</param>
        public CxTransformSurface(CxMatrix4X4 matrix) : base("CxTransformSurfaceProgram")
        {
            _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        }

        /// <inheritdoc/>
        protected override string[] GetKernelNames() => new[] { KernelName };

        /// <inheritdoc/>
        protected override string GetKernelSource() =>
            LoadEmbeddedResource("VisionNet.Compute.Kernels.TransformSurface.cl");

        /// <summary>
        /// Ensures the matrix buffer has been uploaded to GPU memory.
        /// Must be called after <see cref="OpenCLComputation.EnsureInitialized"/>.
        /// </summary>
        private void EnsureMatrixBuffer()
        {
            if (_matrixBuf != null) return;
            lock (_matrixLock)
            {
                if (_matrixBuf != null) return;
                _matrixBuf = AllocatePersistent<float>(
                    MemFlags.ReadOnly | MemFlags.CopyHostPtr, _matrix.Data);
                if (_matrixBuf == null)
                    throw new InvalidOperationException(
                        "Failed to allocate persistent matrix buffer on GPU.");
            }
        }

        /// <summary>
        /// Transforms all valid cells of <paramref name="surface"/> by the matrix supplied
        /// at construction time and returns the resulting point cloud on the CPU.
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

            EnsureMatrixBuffer();

            var dataBuf = AllocateTransient<short>(MemFlags.ReadOnly | MemFlags.CopyHostPtr, data);
            var dstBuf  = AllocateTransientWithSize<float>(MemFlags.WriteOnly, count * 4);

            bool ok = SetKernelArgs(KernelName,
                dataBuf, dstBuf, width, length,
                surface.XOffset, surface.YOffset, surface.ZOffset,
                surface.XScale, surface.YScale, surface.ZScale,
                _matrixBuf);

            ok &= ExecuteKernel(KernelName, new[] { new IntPtr(count) });
            ok &= ReadBuffer(dstBuf, dst);

            ReleaseTransient();

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

            var result  = new CxPoint3D[validCount];
            var resultI = new byte[validCount];
            Array.Copy(tempPoints,    result,  validCount);
            if (hasIntensity) Array.Copy(tempIntensity, resultI, validCount);

            return (result, hasIntensity ? resultI : null);
        }

        /// <summary>
        /// Transforms all cells of <paramref name="surface"/> on the GPU and returns the
        /// output buffer directly, without reading data back to the CPU.
        /// The buffer holds one <c>float4</c> (XYZW) per input cell; cells that were invalid
        /// in the source (Z == <c>-32768</c>) carry <c>NaN</c> as their Z component.
        /// </summary>
        /// <remarks>
        /// The returned buffer is tracked as a transient resource on this instance.
        /// After the downstream computation (e.g., <see cref="CxUniformSurface"/>) has
        /// finished consuming the buffer, the caller must invoke
        /// <see cref="OpenCLComputation.ReleaseTransient"/> on <b>this</b> instance to
        /// free the GPU memory.
        /// </remarks>
        /// <param name="surface">Source height-map surface.</param>
        /// <returns>
        /// GPU buffer (float4 per cell, stride = 4) and the total cell count.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if initialisation or kernel execution fails.</exception>
        internal (IMem DstBuf, int CellCount) TransformToBuffer(CxSurface surface)
        {
            int count = surface.Width * surface.Length;

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            EnsureMatrixBuffer();

            var dataBuf = AllocateTransient<short>(MemFlags.ReadOnly | MemFlags.CopyHostPtr, surface.Data);
            var dstBuf  = AllocateTransientWithSize<float>(MemFlags.WriteOnly, count * 4);

            bool ok = SetKernelArgs(KernelName,
                dataBuf, dstBuf, surface.Width, surface.Length,
                surface.XOffset, surface.YOffset, surface.ZOffset,
                surface.XScale, surface.YScale, surface.ZScale,
                _matrixBuf);

            if (!ok || !ExecuteKernel(KernelName, new[] { new IntPtr(count) }))
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            return (dstBuf, count);
        }
    }
}
