using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet
{
    public static partial class VisionOperator
    {
        [DllImport(@"VisionLib", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetCenter(CxPoint3D[] Pts, int size, ref CxPoint3D outCenter);

        [DllImport(@"VisionLib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UniformGridSample(
            [In] CxPoint3D[] points, byte[] intensitys, int size, float xScale, float yScale, float xMin, float xMax, float yMin, float yMax,
            [Out] float[] heightMap, byte[] intensityMap,out int mapSize);
    }
}
