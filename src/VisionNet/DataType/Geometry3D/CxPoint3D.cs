using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    [StructLayout(LayoutKind.Explicit)]
    public struct CxPoint3D
    {
        public CxPoint3D(float x, float y, float z) { X = x; Y = y; Z = z; }
        [FieldOffset(0)] public float X;
        [FieldOffset(4)] public float Y;
        [FieldOffset(8)] public float Z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CxPoint3DI
    {
        public CxPoint3DI(float x, float y, float z, float i) { X = x; Y = y; Z = z; Intensity = i; }
        public float X; public float Y; public float Z; public float Intensity;
    }
}
