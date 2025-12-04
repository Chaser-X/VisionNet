using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using VisionNet.Compute;
using VisionNet.DataType;

namespace VisionNet
{
    public static partial class VisionOperator
    {
        public static bool InitialLib()
        {
            // Initialize the native library if needed
            // This can be used to load the library or perform any necessary setup
            var state = OpenCLEnvironment.Instance.Initialize();
            if (!state)
            {
                Console.WriteLine($"Failed to initialize OpenCL environment: {state}");
                return false;
            }
            return state;
        }

        public static void DestroyLib()
        {
            OpenCLEnvironment.Instance.Cleanup();
        }

        public static CxPoint3D GetPoint3DArrayCenter(List<CxPoint3D> point3Ds)
        {
            CxPoint3D center = new CxPoint3D();
            var ret = GetCenter(point3Ds.ToArray(), point3Ds.Count, ref center);
            if (ret == -1)
                center = new CxPoint3D(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            return center;
        }

        public static CxSurface UniformSuface(CxPoint3D[] points, byte[] intensity, int width, int height,
            float xScale, float yScale, float zScale, float xOffset, float yOffset, float zOffset)
        {
            CxSurface suface = new CxSurface();
            var sizeMap = width * height;
            float[] heightMap = new float[sizeMap];
            byte[] intensityMap = new byte[sizeMap];
            if (intensity == null)
                intensityMap = new byte[0];
            UniformGridSample(points, intensity, points.Length, xScale, yScale, xOffset, xOffset + width * xScale, yOffset,
                yOffset + height * yScale, heightMap, intensityMap, out int mapSize);

            short[] heighData = new short[mapSize];
            for (int i = 0; i < mapSize; i++)
            {
                if (float.IsInfinity(heightMap[i]) || float.IsNaN(heightMap[i]))
                    heighData[i] = -32768;
                else
                {
                    var data = (heightMap[i] - zOffset) / zScale;
                    heighData[i] = (short)(data);
                }
            }
            suface = new CxSurface(width, height, heighData, intensityMap, xOffset, yOffset, zOffset, xScale, yScale, zScale);
            return suface;
        }
        //3D transform point by matrix
        public static CxPoint3D TransformPoint3D(CxPoint3D point, CxMatrix4X4 matrix)
        {
            var m = matrix.Data;
            float x = point.X, y = point.Y, z = point.Z;

            // 按OpenGL列主序
            float tx = m[0] * x + m[1] * y + m[2] * z + m[3];
            float ty = m[4] * x + m[5] * y + m[6] * z + m[7];
            float tz = m[8] * x + m[9] * y + m[10] * z + m[11];
            return new CxPoint3D(tx, ty, tz);
        }
        /// <summary>
        /// 计算3D点云的边界框
        /// </summary>
        /// <param name="points"> 需要计算的3D点云数组</param>
        /// <returns> 返回计算得到的边界框，如果点云数组为空则返回null</returns>
        public static Box3D? CalculateBoundingBox(CxPoint3D[] points)
        {
            if (points.Length == 0)
                return null;

            var partitioner = Partitioner.Create(points, true);
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

            var lockObj = new object();

            Parallel.ForEach(partitioner, () => new
            {
                MinX = float.MaxValue,
                MaxX = float.MinValue,
                MinY = float.MaxValue,
                MaxY = float.MinValue,
                MinZ = float.MaxValue,
                MaxZ = float.MinValue
            },
            (point, loop, localState) =>
            {
                return new
                {
                    MinX = Math.Min(localState.MinX, point.X),
                    MaxX = Math.Max(localState.MaxX, point.X),
                    MinY = Math.Min(localState.MinY, point.Y),
                    MaxY = Math.Max(localState.MaxY, point.Y),
                    MinZ = Math.Min(localState.MinZ, point.Z),
                    MaxZ = Math.Max(localState.MaxZ, point.Z)
                };
            },
            localState =>
            {
                lock (lockObj)
                {
                    minX = Math.Min(minX, localState.MinX);
                    maxX = Math.Max(maxX, localState.MaxX);
                    minY = Math.Min(minY, localState.MinY);
                    maxY = Math.Max(maxY, localState.MaxY);
                    minZ = Math.Min(minZ, localState.MinZ);
                    maxZ = Math.Max(maxZ, localState.MaxZ);
                }
            });

            return new Box3D
            {
                Center = new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                Size = new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ)
            };

        }
        /// <summary>
        /// 使用SIMD计算3D点云的边界框
        /// </summary>
        /// <param name="points">需要计算的3D点云数组</param>
        /// <returns>返回计算得到的边界框，如果点云数组为空则返回null</returns>
        public static Box3D? CalculateBoundingBoxSIMD(CxPoint3D[] points)
        {
            if (points.Length == 0)
                return null;

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            for (int i = 0; i < points.Length; i++)
            {
                var point = new Vector3(points[i].X, points[i].Y, points[i].Z);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            return new Box3D(
                new CxPoint3D((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2),
                new CxSize3D(max.X - min.X, max.Y - min.Y, max.Z - min.Z));
        }

        /// <summary>
        /// 对CxSurface对象进行3D变换
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="matrix"></param>
        /// <returns></returns>
        public static CxSurface TransformSurface(CxSurface surface, CxMatrix4X4 matrix , SampleMode inMode = SampleMode.Average)
        {
            if (surface == null || matrix == null)
                return null;
            var transform = new CxTransformSurface(matrix);
            var points = transform.Transform(surface);

            if (points.TranformedPoints == null || points.TranformedPoints.Length == 0)
                return null;
            var box = CalculateBoundingBoxSIMD(points.TranformedPoints);
            if (box == null)
                return null;
            var xcale = 0.01f;//surface.XScale; 
            var yScale = 0.01f; //surface.YScale;
            var width = (int)Math.Ceiling((box.Value.Size.Width / xcale));
            var height = (int)Math.Ceiling((box.Value.Size.Height / yScale));
            var xOffset = box.Value.Center.X - (width * xcale) / 2;
            var yOffset = box.Value.Center.Y - (height * yScale) / 2;
            var zOffset = box.Value.Center.Z;
            var zScale = box.Value.Size.Depth / ushort.MaxValue;

            // Create a new surface with the transformed points
            var uniformSurface = new CxUniformSurface();
            var newSurface = uniformSurface.Sample(points.TranformedPoints, points.Intensitys, width, height,
                xcale, yScale, zScale, xOffset, yOffset, zOffset, inMode);
            //var newSurface = UniformSuface(points, null, width, height,
            //    xcale, yScale, zScale, xOffset, yOffset, zOffset);
            return newSurface;
        }

    }
}
