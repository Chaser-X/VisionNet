using System;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CxVector3D
    {
        public CxVector3D(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float X; public float Y; public float Z;
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public static CxVector3D operator +(CxVector3D v1, CxVector3D v2) => new CxVector3D(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        public static CxVector3D operator -(CxVector3D v1, CxVector3D v2) => new CxVector3D(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        public static CxVector3D operator *(CxVector3D v, float scale) => new CxVector3D(v.X * scale, v.Y * scale, v.Z * scale);
        public static CxVector3D operator /(CxVector3D v, float scale) => new CxVector3D(v.X / scale, v.Y / scale, v.Z / scale);
        public float Dot(CxVector3D v2) => X * v2.X + Y * v2.Y + Z * v2.Z;
        public CxVector3D Cross(CxVector3D v2) => new CxVector3D(Y * v2.Z - Z * v2.Y, Z * v2.X - X * v2.Z, X * v2.Y - Y * v2.X);
        public CxVector3D Normalize() => new CxVector3D(X / Length, Y / Length, Z / Length);
    }
}
