using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VisionNet.DataType
{
    [StructLayout(LayoutKind.Explicit)]
    public struct Point3D
    {
        public Point3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        [FieldOffset(0)]
        public float X;
        [FieldOffset(4)]
        public float Y;
        [FieldOffset(8)]
        public float Z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point3DI
    {
        public Point3DI(float x, float y, float z, float i)
        {
            X = x;
            Y = y;
            Z = z;
            Intensity = i;
        }
        public float X;
        public float Y;
        public float Z;
        public float Intensity;
    }
}
