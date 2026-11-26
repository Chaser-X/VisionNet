using System;
using System.Globalization;

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
        public float Length
        {
            get
            {
                // Scale before squaring to avoid overflow/underflow; keep zero and non-finite results.
                float scale = Math.Max(Math.Abs(X), Math.Abs(Y));
                if (scale == 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                    return (float)Math.Sqrt(X * X + Y * Y);

                float sx = X / scale;
                float sy = Y / scale;
                return scale * (float)Math.Sqrt(sx * sx + sy * sy);
            }
        }

        /// <summary>Returns the component-wise sum of two vectors.</summary>
        public static CxVector2D operator +(CxVector2D a, CxVector2D b) => new CxVector2D(a.X + b.X, a.Y + b.Y);

        /// <summary>Returns the component-wise difference of two vectors.</summary>
        public static CxVector2D operator -(CxVector2D a, CxVector2D b) => new CxVector2D(a.X - b.X, a.Y - b.Y);

        /// <summary>Returns the vector scaled by <paramref name="scale"/>.</summary>
        public static CxVector2D operator *(CxVector2D v, float scale) => new CxVector2D(v.X * scale, v.Y * scale);

        /// <summary>Returns the vector scaled by <paramref name="scale"/>.</summary>
        public static CxVector2D operator *(float scale, CxVector2D v) => new CxVector2D(v.X * scale, v.Y * scale);

        /// <summary>Returns the vector divided by <paramref name="scale"/>.</summary>
        public static CxVector2D operator /(CxVector2D v, float scale) => new CxVector2D(v.X / scale, v.Y / scale);

        /// <summary>Returns the dot (scalar) product of this vector and <paramref name="other"/>.</summary>
        public float Dot(CxVector2D other) => X * other.X + Y * other.Y;

        /// <summary>Returns a unit vector in the same direction. Behaviour is undefined if length is zero.</summary>
        public CxVector2D Normalize()
        {
            // Normalise the scaled components rather than dividing by Length: the raw length can
            // overflow or underflow for a direction that is itself perfectly representable.
            float scale = Math.Max(Math.Abs(X), Math.Abs(Y));
            if (scale == 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                return new CxVector2D(X / Length, Y / Length);

            float sx = X / scale;
            float sy = Y / scale;
            float norm = (float)Math.Sqrt(sx * sx + sy * sy);
            return new CxVector2D(sx / norm, sy / norm);
        }

        /// <summary>Creates a unit vector from an angle in degrees (measured from +X axis).</summary>
        public static CxVector2D FromPolar(float angleDeg) =>
            new CxVector2D(
                (float)Math.Cos(angleDeg * Math.PI / 180f),
                (float)Math.Sin(angleDeg * Math.PI / 180f));

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxVector2D(X={0:G9}, Y={1:G9})", X, Y);
    }
}
