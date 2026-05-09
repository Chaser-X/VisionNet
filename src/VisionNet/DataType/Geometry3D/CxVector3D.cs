using System;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// An immutable 3D vector supporting arithmetic operators, dot product, cross product,
    /// and normalisation. The struct uses a sequential memory layout for native interop.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CxVector3D
    {
        /// <summary>Initializes a vector with the given components.</summary>
        public CxVector3D(float x, float y, float z) { X = x; Y = y; Z = z; }

        /// <summary>X component.</summary>
        public float X;

        /// <summary>Y component.</summary>
        public float Y;

        /// <summary>Z component.</summary>
        public float Z;

        /// <summary>Gets the Euclidean length of the vector.</summary>
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Returns the component-wise sum of two vectors.</summary>
        public static CxVector3D operator +(CxVector3D a, CxVector3D b) => new CxVector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>Returns the component-wise difference of two vectors.</summary>
        public static CxVector3D operator -(CxVector3D a, CxVector3D b) => new CxVector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>Returns the vector scaled by <paramref name="scale"/>.</summary>
        public static CxVector3D operator *(CxVector3D v, float scale) => new CxVector3D(v.X * scale, v.Y * scale, v.Z * scale);

        /// <summary>Returns the vector divided by <paramref name="scale"/>.</summary>
        public static CxVector3D operator /(CxVector3D v, float scale) => new CxVector3D(v.X / scale, v.Y / scale, v.Z / scale);

        /// <summary>Returns the dot (scalar) product of this vector and <paramref name="other"/>.</summary>
        public float Dot(CxVector3D other) => X * other.X + Y * other.Y + Z * other.Z;

        /// <summary>Returns the cross (vector) product of this vector and <paramref name="other"/>.</summary>
        public CxVector3D Cross(CxVector3D other) =>
            new CxVector3D(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

        /// <summary>Returns a unit vector in the same direction. Behaviour is undefined if length is zero.</summary>
        public CxVector3D Normalize() => new CxVector3D(X / Length, Y / Length, Z / Length);
    }
}
