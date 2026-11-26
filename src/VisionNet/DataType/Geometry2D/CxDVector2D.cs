using System;
using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 2D vector with double-precision floating-point X and Y components,
    /// supporting arithmetic operators, dot product, and normalisation.
    /// Double-precision counterpart of <see cref="CxVector2D"/>.
    /// </summary>
    public struct CxDVector2D
    {
        /// <summary>Initializes a vector with the given components.</summary>
        public CxDVector2D(double x, double y) { X = x; Y = y; }

        /// <summary>X component.</summary>
        public double X;

        /// <summary>Y component.</summary>
        public double Y;

        /// <summary>Gets the Euclidean length of the vector.</summary>
        public double Length
        {
            get
            {
                // Scale before squaring to avoid overflow/underflow; keep zero and non-finite results.
                double scale = Math.Max(Math.Abs(X), Math.Abs(Y));
                if (scale == 0.0 || double.IsNaN(scale) || double.IsInfinity(scale))
                    return Math.Sqrt(X * X + Y * Y);

                double sx = X / scale;
                double sy = Y / scale;
                return scale * Math.Sqrt(sx * sx + sy * sy);
            }
        }

        /// <summary>Returns the component-wise sum of two vectors.</summary>
        public static CxDVector2D operator +(CxDVector2D a, CxDVector2D b) => new CxDVector2D(a.X + b.X, a.Y + b.Y);

        /// <summary>Returns the component-wise difference of two vectors.</summary>
        public static CxDVector2D operator -(CxDVector2D a, CxDVector2D b) => new CxDVector2D(a.X - b.X, a.Y - b.Y);

        /// <summary>Returns the vector scaled by <paramref name="scale"/>.</summary>
        public static CxDVector2D operator *(CxDVector2D v, double scale) => new CxDVector2D(v.X * scale, v.Y * scale);

        /// <summary>Returns the vector scaled by <paramref name="scale"/>.</summary>
        public static CxDVector2D operator *(double scale, CxDVector2D v) => new CxDVector2D(v.X * scale, v.Y * scale);

        /// <summary>Returns the vector divided by <paramref name="scale"/>.</summary>
        public static CxDVector2D operator /(CxDVector2D v, double scale) => new CxDVector2D(v.X / scale, v.Y / scale);

        /// <summary>Returns the dot (scalar) product of this vector and <paramref name="other"/>.</summary>
        public double Dot(CxDVector2D other) => X * other.X + Y * other.Y;

        /// <summary>Returns a unit vector in the same direction. Behaviour is undefined if length is zero.</summary>
        public CxDVector2D Normalize()
        {
            // Normalise the scaled components rather than dividing by Length: the raw length can
            // overflow or underflow for a direction that is itself perfectly representable.
            double scale = Math.Max(Math.Abs(X), Math.Abs(Y));
            if (scale == 0.0 || double.IsNaN(scale) || double.IsInfinity(scale))
                return new CxDVector2D(X / Length, Y / Length);

            double sx = X / scale;
            double sy = Y / scale;
            double norm = Math.Sqrt(sx * sx + sy * sy);
            return new CxDVector2D(sx / norm, sy / norm);
        }

        /// <summary>Creates a unit vector from an angle in degrees (measured from +X axis).</summary>
        public static CxDVector2D FromPolar(double angleDeg) =>
            new CxDVector2D(
                Math.Cos(angleDeg * Math.PI / 180.0),
                Math.Sin(angleDeg * Math.PI / 180.0));

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxDVector2D(X={0:G17}, Y={1:G17})", X, Y);

        /// <summary>Converts a single-precision vector to double precision (lossless).</summary>
        public static implicit operator CxDVector2D(CxVector2D v) => new CxDVector2D(v.X, v.Y);

        /// <summary>Converts this double-precision vector to single precision (may lose precision).</summary>
        public static explicit operator CxVector2D(CxDVector2D v) => new CxVector2D((float)v.X, (float)v.Y);
    }
}
