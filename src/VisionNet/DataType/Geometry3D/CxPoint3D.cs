using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 3D point with single-precision floating-point X, Y, Z coordinates.
    /// The struct uses an explicit memory layout for direct interop with native code.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct CxPoint3D
    {
        /// <summary>Initializes a point with the given coordinates.</summary>
        public CxPoint3D(float x, float y, float z) { X = x; Y = y; Z = z; }

        /// <summary>X coordinate (offset 0).</summary>
        [FieldOffset(0)] public float X;

        /// <summary>Y coordinate (offset 4).</summary>
        [FieldOffset(4)] public float Y;

        /// <summary>Z coordinate (offset 8).</summary>
        [FieldOffset(8)] public float Z;
    }

    /// <summary>A 3D point with an additional per-point intensity (reflectance) value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CxPoint3DI
    {
        /// <summary>Initializes the point and its intensity.</summary>
        public CxPoint3DI(float x, float y, float z, float intensity)
        {
            X = x; Y = y; Z = z; Intensity = intensity;
        }

        /// <summary>X coordinate.</summary>
        public float X;

        /// <summary>Y coordinate.</summary>
        public float Y;

        /// <summary>Z coordinate.</summary>
        public float Z;

        /// <summary>Per-point intensity / reflectance value.</summary>
        public float Intensity;
    }
}
