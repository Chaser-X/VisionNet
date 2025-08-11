using System;
using System.Collections.Generic;
using System.Linq;
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

        //3D transform CxSurface array by matrix
        public static CxPoint3D[] TransformSurface(CxSurface surface, CxMatrix4X4 matrix)
        {
            if (surface == null || matrix == null)
                return null;
            //int width = surface.Width;
            //int length = surface.Length;
            //int count = width * length;
            //var data = surface.Data;
            //CxPoint3D[] points = new CxPoint3D[count];
            //for (int i = 0; i < count; i++)
            //{
            //    float x = (i % width) * surface.XScale + surface.XOffset;
            //    float y = (i / width) * surface.YScale + surface.YOffset;
            //    float z = data[i] * surface.ZScale + surface.ZOffset;
            //    points[i] = TransformPoint3D(new CxPoint3D(x, y, z), matrix);
            //}
            //使用CxTransformSurface进行矩阵变换
            var transform = new CxTransformSurface(matrix);
            var points = transform.Transform(surface);

            return points;
        }
    }
}
