using System;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 2D vector with single-precision floating-point X and Y components,
    /// supporting arithmetic operators, dot product, and normalisation.
    /// </summary>
    public struct CxVector2D
    {
        /// <summary>Initializes a vector with the given components.</summary>
        public CxVector2D(float x, float y) { X = x; Y = y; }

        /// <summary>X component.</summary>
        public float X;

        /// <summary>Y component.</summary>
        public float Y;

        /// <summary>Gets the Euclidean length of the vector.</summary>
        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        /// <summary>Returns the component-wise sum of two vectors.</summary>
        public static CxVector2D operator +(CxVector2D a, CxVector2D b) => new CxVector2D(a.X + b.X, a.Y + b.Y);

        /// <summary>Returns the component-wise difference of two vectors.</summary>
        public static CxVector2D operator -(CxVector2D a, CxVector2D b) => new CxVector2D(a.X - b.X, a.Y - b.Y);

        /// <summary>Returns the vector scaled by <paramref name="scale"/>.</summary>
        public static CxVector2D operator *(CxVector2D v, float scale) => new CxVector2D(v.X * scale, v.Y * scale);

        /// <summary>Returns the vector divided by <paramref name="scale"/>.</summary>
        public static CxVector2D operator /(CxVector2D v, float scale) => new CxVector2D(v.X / scale, v.Y / scale);

        /// <summary>Returns the dot (scalar) product of this vector and <paramref name="other"/>.</summary>
        public float Dot(CxVector2D other) => X * other.X + Y * other.Y;

        /// <summary>Returns a unit vector in the same direction. Behaviour is undefined if length is zero.</summary>
        public CxVector2D Normalize() => new CxVector2D(X / Length, Y / Length);

        /// <summary>Creates a unit vector from an angle in radians (measured from +X axis).</summary>
        public static CxVector2D FromPolar(float angleRad) =>
            new CxVector2D((float)Math.Cos(angleRad), (float)Math.Sin(angleRad));
    }
}
