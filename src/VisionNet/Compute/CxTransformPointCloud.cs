using OpenCL.Net;
using System;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Compute
{
    /// <summary>
    /// GPU-accelerated point cloud transformation: applies a 4×4 matrix to every point
    /// in a <see cref="CxPointCloud"/> on the OpenCL device.
    /// </summary>
    public class CxTransformPointCloud : OpenCLComputation
    {
        private const string KernelName = "TransformVertices";

        private readonly CxMatrix4X4 _matrix;
        private readonly object _matrixLock = new object();
        private IMem _matrixBuf;

        /// <param name="matrix">4×4 row-major transformation matrix.</param>
        public CxTransformPointCloud(CxMatrix4X4 matrix) : base("CxTransformPointCloudProgram")
        {
            _matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        }

        protected override string[] GetKernelNames() => new[] { KernelName };

        protected override string GetKernelSource() =>
            LoadEmbeddedResource("VisionNet.Compute.Kernels.TransformPointCloud.cl");

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
        /// Transforms all points of <paramref name="cloud"/> by the matrix supplied
        /// at construction time and returns the resulting point array on the CPU.
        /// </summary>
        public (CxPoint3D[] TransformedPoints, byte[] Intensities) Transform(CxPointCloud cloud)
        {
            int count = cloud.Width * cloud.Length;
            var pts = cloud.ToPoints();
            bool hasIntensity = cloud.Intensity != null && cloud.Intensity.Length >= count;

            float[] pointData = new float[count * 3];
            Parallel.For(0, count, i =>
            {
                pointData[i * 3]     = pts[i].X;
                pointData[i * 3 + 1] = pts[i].Y;
                pointData[i * 3 + 2] = pts[i].Z;
            });

            float[] dst = new float[count * 3];

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            EnsureMatrixBuffer();

            var pointBuf = AllocateTransient<float>(
                MemFlags.ReadOnly | MemFlags.CopyHostPtr, pointData);
            var dstBuf = AllocateTransientWithSize<float>(
                MemFlags.WriteOnly, count * 3);

            bool ok = SetKernelArgs(KernelName,
                pointBuf, _matrixBuf, count, dstBuf);
            ok &= ExecuteKernel(KernelName, new[] { new IntPtr(count) });
            ok &= ReadBuffer(dstBuf, dst);

            ReleaseTransient();

            if (!ok)
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            var result = new CxPoint3D[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new CxPoint3D(dst[i * 3], dst[i * 3 + 1], dst[i * 3 + 2]);
            }

            return (result, hasIntensity ? cloud.Intensity : null);
        }

        /// <summary>
        /// Transforms all points on the GPU and returns the output buffer directly,
        /// without reading data back to the CPU (for GPU pipeline usage).
        /// </summary>
        internal (IMem DstBuf, int PointCount) TransformToBuffer(CxPointCloud cloud)
        {
            int count = cloud.Width * cloud.Length;
            var pts = cloud.ToPoints();

            float[] pointData = new float[count * 3];
            for (int i = 0; i < count; i++)
            {
                pointData[i * 3]     = pts[i].X;
                pointData[i * 3 + 1] = pts[i].Y;
                pointData[i * 3 + 2] = pts[i].Z;
            }

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            EnsureMatrixBuffer();

            var pointBuf = AllocateTransient<float>(
                MemFlags.ReadOnly | MemFlags.CopyHostPtr, pointData);
            var dstBuf = AllocateTransientWithSize<float>(
                MemFlags.WriteOnly, count * 3);

            bool ok = SetKernelArgs(KernelName,
                pointBuf, _matrixBuf, count, dstBuf);

            if (!ok || !ExecuteKernel(KernelName, new[] { new IntPtr(count) }))
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            return (dstBuf, count);
        }
    }
}
