using System;
using System.Runtime.InteropServices;
using VisionNet.DataType;

namespace VisionNet.Compute
{
    /// <summary>
    /// 使用OpenCL并行计算对CxSurface对象进行基于CxMatrix4X4的变换
    /// </summary>
    public class CxTransformSurface : OpenCLComputation
    {
        private const string KernelName = "TransformSurface";
        private CxMatrix4X4 _matrix;

        public CxTransformSurface(CxMatrix4X4 matrix)
            : base("CxTransformSurfaceProgram")
        {
            _matrix = matrix;
        }

        protected override string GetKernelSource()
        {
            // OpenCL kernel: 4x4矩阵变换3D点
            return @"__kernel void TransformSurface(
                __global const short* data,
                __global float* dstPoints,
                int width,
                int length,
                float xOffset, float yOffset, float zOffset,
                float xScale, float yScale, float zScale,
                __global const float* matrix)
            {
                int gid = get_global_id(0);
                int x = gid % width;
                int y = gid / width;
                if (x >= width || y >= length) return;
                int idx = y * width + x;
                float px = x * xScale + xOffset;
                float py = y * yScale + yOffset;
                float pz = zOffset + data[idx] * zScale;
                float4 p = (float4)(px, py, pz, 1.0f);

                float4 r;
                r.x = matrix[0]*p.x + matrix[1]*p.y + matrix[2]*p.z + matrix[3]*p.w;
                r.y = matrix[4]*p.x + matrix[5]*p.y + matrix[6]*p.z + matrix[7]*p.w;
                r.z = matrix[8]*p.x + matrix[9]*p.y + matrix[10]*p.z + matrix[11]*p.w;
                r.w = matrix[12]*p.x + matrix[13]*p.y + matrix[14]*p.z + matrix[15]*p.w;
                dstPoints[gid * 4] = r.x;
                dstPoints[gid * 4 + 1] = r.y;
                dstPoints[gid * 4 + 2] = data[idx] == -32768 ? NAN : r.z;
                dstPoints[gid * 4 + 3] = r.w;
            }";//= data[idx] == -32768 ? (float4)(NAN, NAN, NAN, 1.0f): r;
        }

        protected override string[] GetKernelNames()
        {
            return new[] { KernelName };
        }

        /// <summary>
        /// 对CxSurface进行矩阵变换，返回变换后的点云
        /// </summary>
        public (CxPoint3D[] TranformedPoints, byte[] Intensitys) Transform(CxSurface surface)
        {
            int width = surface.Width;
            int length = surface.Length;
            int count = width * length;
            var data = surface.Data;
            var intensity = surface.Intensity;
            float[] dstFloat4 = new float[count * 4];
            float[] matrix = _matrix.Data;
            if (!EnsureInitialized())
                throw new Exception("OpenCL环境初始化失败");

            var dataBuffer = CreateBuffer<short>(OpenCL.Net.MemFlags.ReadOnly | OpenCL.Net.MemFlags.CopyHostPtr, data);
            var matrixBuffer = CreateBuffer<float>(OpenCL.Net.MemFlags.ReadOnly | OpenCL.Net.MemFlags.CopyHostPtr, matrix);
            var dstBuffer = CreateBufferWithSize<float>(OpenCL.Net.MemFlags.WriteOnly, count * 4);
            var state = true;
            state &= SetKernelArg(KernelName, 0, dataBuffer);
            state &= SetKernelArg(KernelName, 1, dstBuffer);
            state &= SetKernelArg(KernelName, 2, width);
            state &= SetKernelArg(KernelName, 3, length);
            state &= SetKernelArg(KernelName, 4, surface.XOffset);
            state &= SetKernelArg(KernelName, 5, surface.YOffset);
            state &= SetKernelArg(KernelName, 6, surface.ZOffset);
            state &= SetKernelArg(KernelName, 7, surface.XScale);
            state &= SetKernelArg(KernelName, 8, surface.YScale);
            state &= SetKernelArg(KernelName, 9, surface.ZScale);
            state &= SetKernelArg(KernelName, 10, matrixBuffer);

            state &= ExecuteKernel(KernelName, new IntPtr[] { new IntPtr(count) });

            state &= ReadBuffer(dstBuffer, dstFloat4);

            if (!state)
            {
                Cleanup();
                throw new Exception("OpenCL计算失败");
            }

            int validCount = 0;
            CxPoint3D[] temp = new CxPoint3D[count];
            byte[] tempIntensity = new byte[count];
            var hasIntensity = intensity != null && intensity.Length == count;
            for (int i = 0; i < count; i++)
            {
                float z = dstFloat4[i * 4 + 2];
                if (float.IsInfinity(z) || float.IsNaN(z))
                    continue;
                float x = dstFloat4[i * 4 + 0];
                float y = dstFloat4[i * 4 + 1];
                float w = dstFloat4[i * 4 + 3];
                if (Math.Abs(w) > 1e-6f)
                {
                    x /= w;
                    y /= w;
                    z /= w;
                }
                temp[validCount] = new CxPoint3D(x, y, z);
                if (hasIntensity)
                    tempIntensity[validCount] = intensity[i];
                validCount++;
            }
            if (validCount == temp.Length)
                return (temp, hasIntensity ? tempIntensity : null);
            CxPoint3D[] result = new CxPoint3D[validCount];
            Array.Copy(temp, result, validCount);
            byte[] resultIntensity = new byte[validCount];
            if (hasIntensity)
                Array.Copy(tempIntensity, resultIntensity, validCount);
            // 释放资源
            Cleanup();
            return (result, hasIntensity ? resultIntensity : null);
        }
    }
}
