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

        public static CxSuface UniformSuface(CxPoint3D[] points, byte[] intensity, int width, int height,
            float xScale, float yScale, float zScale, float xOffset, float yOffset, float zOffset)
        {
            CxSuface suface = new CxSuface();
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
            suface = new CxSuface(width, height, heighData, intensityMap, xOffset, yOffset, zOffset, xScale, yScale, zScale);
            return suface;
        }
    }
}
