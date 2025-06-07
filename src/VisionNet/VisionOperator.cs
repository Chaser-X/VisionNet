using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet
{
    public static partial class VisionOperator
    {
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
            float tx = m[0] * x + m[4] * y + m[8] * z + m[12];
            float ty = m[1] * x + m[5] * y + m[9] * z + m[13];
            float tz = m[2] * x + m[6] * y + m[10] * z + m[14];
            float tw = m[3] * x + m[7] * y + m[11] * z + m[15];

            if (Math.Abs(tw) > 1e-6f)
            {
                tx /= tw;
                ty /= tw;
                tz /= tw;
            }
            return new CxPoint3D(tx, ty, tz);
        }
    }
}
