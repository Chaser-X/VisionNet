using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using VisionNet.DataType;

namespace VisionNet.Compute
{
    /// <summary>
    /// 基于OpenCL的高效点云采样（UniformGridSample功能）
    /// </summary>
    public class CxUniformSurface : OpenCLComputation
    {
        private const string KernelName = "UniformSurfaceSample";

        public CxUniformSurface() : base("CxUniformSurfaceProgram") { }

        //protected override string GetKernelSource()
        //{
        //    return @"__kernel void UniformSurfaceSample(
        //        __global const float3* points, // X,Y,Z
        //        __global const uchar* intensity,
        //        int pointCount,
        //        int width,
        //        int height,
        //        float xScale, float yScale, float zScale,
        //        float xOffset, float yOffset, float zOffset,
        //        __global int* heightMap,
        //        __global int* intensityMap,   
        //        __global int* pointCountMap)
        //    {
        //        int gid = get_global_id(0);
        //        float3 p = points[gid];
        //        //p.z为无效值时，跳过处理
        //        if (isnan(p.z) || isinf(p.z)) return;
        //        int xIdx = (int)((p.x - xOffset) / xScale);
        //        int yIdx = (int)((p.y - yOffset) / yScale);
        //        // 处理Z轴缩放
        //        p.z = (p.z - zOffset) / zScale;
        //        if (xIdx >= 0 && xIdx < width && yIdx >= 0 && yIdx < height)
        //        {
        //            int idx = yIdx * width + xIdx;
        //            int z = (int)p.z;
        //            int inten = intensity ? intensity[gid] : 0; // 用int类型
        //            atomic_add(&heightMap[idx], z);
        //            atomic_add(&intensityMap[idx], inten);      
        //            atomic_inc(&pointCountMap[idx]);
        //        }
        //    }";
        //}
        protected override string GetKernelSource()
        {
            return @"
            __kernel void UniformSurfaceSample(
                __global const float* points,
                __global const uchar* intensity,
                int pointCount,
                int width,
                int height,
                float xScale, 
                float yScale, 
                float zScale,
                float xOffset, 
                float yOffset, 
                float zOffset,
                __global int* heightMap,
                __global int* intensityMap,   
                __global int* pointCountMap,
                int inMode)
            {
                int gid = get_global_id(0);
                float px = points[gid * 3 + 0];
                float py = points[gid * 3 + 1];
                float pz = points[gid * 3 + 2];
                float3 p = (float3)(px,py,pz);
                if (isnan(p.z) || isinf(p.z)) return;
                int xIdx = (int)((p.x - xOffset) / xScale);
                int yIdx = (int)((p.y - yOffset) / yScale);
                if (xIdx < 0 || xIdx >= width || yIdx < 0 || yIdx >= height) return;
                int scaledZ = (int)((p.z - zOffset)/ zScale);
                int idx = yIdx * width + xIdx;
                int inten = intensity ? intensity[gid] : 0;
                switch (inMode) 
                {
                    case 0:
                        {
                            int oldZ = atomic_max(&heightMap[idx], scaledZ);
                            if (scaledZ >= oldZ) intensityMap[idx] = inten;
                        }
                        break;
                    case 1:
                        {
                            int oldZ = atomic_min(&heightMap[idx], scaledZ);
                            if (scaledZ < oldZ) intensityMap[idx] = inten;
                        }
                        break;
                    default:
                        atomic_add(&heightMap[idx], scaledZ);
                        atomic_add(&intensityMap[idx], inten);
                        break;
                }
                atomic_inc(&pointCountMap[idx]);
            }";
        }

        protected override string[] GetKernelNames()
        {
            return new[] { KernelName };
        }

        /// <summary>
        /// 点云采样为高度图和亮度图
        /// </summary>
        public CxSurface Sample(CxPoint3D[] points, byte[] intensity, int width, int height,
    float xScale, float yScale, float zScale, float xOffset, float yOffset, float zOffset, SampleMode inMode = SampleMode.Average)
        {
            int count = points.Length;
            int[] heightMap = new int[width * height];
            switch (inMode)
            {
                case SampleMode.Max:
                    Parallel.For(0, heightMap.Length, i => heightMap[i] = int.MinValue);
                    break;
                case SampleMode.Min:
                    Parallel.For(0, heightMap.Length, i => heightMap[i] = int.MaxValue);
                    break;
                    // 平均模式无需处理
            }
            int[] intensityMap = new int[width * height];
            int[] pointCountMap = new int[width * height];
            // 点云数据转换为float[]，每点3分量
            float[] pointData = new float[count * 3];
            Parallel.For(0, count, i =>
            {
                pointData[i * 3] = points[i].X;
                pointData[i * 3 + 1] = points[i].Y;
                pointData[i * 3 + 2] = points[i].Z;
            });

            if (!EnsureInitialized())
                throw new Exception("OpenCL环境初始化失败");

            // 创建OpenCL缓冲区
            var pointsBuffer = CreateBuffer<float>(OpenCL.Net.MemFlags.ReadOnly | OpenCL.Net.MemFlags.CopyHostPtr, pointData);
            var intensityBuffer = CreateBuffer<byte>(OpenCL.Net.MemFlags.ReadOnly | OpenCL.Net.MemFlags.CopyHostPtr, intensity ?? new byte[count]);
            //var heightMapBuffer = CreateBufferWithSize<int>(OpenCL.Net.MemFlags.WriteOnly, width * height);
            var heightMapBuffer = CreateBuffer<int>(OpenCL.Net.MemFlags.ReadWrite | OpenCL.Net.MemFlags.CopyHostPtr, heightMap);
            var intensityMapBuffer = CreateBufferWithSize<int>(OpenCL.Net.MemFlags.WriteOnly, width * height);
            var pointCountMapBuffer = CreateBufferWithSize<int>(OpenCL.Net.MemFlags.WriteOnly, width * height);

            bool state = true;
            state &= SetKernelArg(KernelName, 0, pointsBuffer);
            state &= SetKernelArg(KernelName, 1, intensityBuffer);
            state &= SetKernelArg(KernelName, 2, count);
            state &= SetKernelArg(KernelName, 3, width);
            state &= SetKernelArg(KernelName, 4, height);
            state &= SetKernelArg(KernelName, 5, xScale);
            state &= SetKernelArg(KernelName, 6, yScale);
            state &= SetKernelArg(KernelName, 7, zScale);
            state &= SetKernelArg(KernelName, 8, xOffset);
            state &= SetKernelArg(KernelName, 9, yOffset);
            state &= SetKernelArg(KernelName, 10, zOffset);
            state &= SetKernelArg(KernelName, 11, heightMapBuffer);
            state &= SetKernelArg(KernelName, 12, intensityMapBuffer);
            state &= SetKernelArg(KernelName, 13, pointCountMapBuffer);
            state &= SetKernelArg(KernelName, 14, (int)inMode);

            state &= ExecuteKernel(KernelName, new IntPtr[] { new IntPtr(count) });

            // 读取结果
            state &= ReadBuffer(heightMapBuffer, heightMap);
            state &= ReadBuffer(intensityMapBuffer, intensityMap);
            state &= ReadBuffer(pointCountMapBuffer, pointCountMap);

            if (!state)
            {
                Cleanup();
                throw new Exception("OpenCL计算失败");
            }
            // 归一化
            short[] data = new short[width * height];
            byte[] intensitydata = new byte[width * height];
            for (int i = 0; i < width * height; i++)
            {
                if (pointCountMap[i] > 0)
                {
                    if (inMode == SampleMode.Average)
                    {
                        float avgZ = heightMap[i] / (float)pointCountMap[i];
                        data[i] = avgZ < -32768 ? short.MinValue : (short)avgZ;
                        if (intensity != null)
                        {
                            float avgInten = intensityMap[i] / (float)pointCountMap[i];
                            intensitydata[i] = (byte)(avgInten < 0 ? 0 : (avgInten > 255 ? 255 : (byte)avgInten));
                        }
                    }
                    else
                    {
                        data[i] = heightMap[i] < -32768 ? short.MinValue : (short)heightMap[i];
                        if (intensity != null)
                            intensitydata[i] = (byte)(intensityMap[i] < 0 ? 0 : (intensityMap[i] > 255 ? 255 : intensityMap[i]));
                    }
                }
                else
                {
                    data[i] = short.MinValue;
                    if (intensity != null)
                        intensitydata[i] = 0;
                }
            }
            Cleanup();
            return new CxSurface(width, height, data, intensity == null ? new byte[0] : intensitydata, xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }
    }
}
