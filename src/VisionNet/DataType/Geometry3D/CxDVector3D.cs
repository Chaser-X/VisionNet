using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 3D vector with double-precision floating-point X, Y, Z components, supporting
    /// arithmetic operators, dot product, cross product, and normalisation.
    /// Double-precision counterpart of <see cref="CxVector3D"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CxDVector3D
    {
        /// <summary>Initializes a vector with the given components.</summary>
        public CxDVector3D(double x, double y, double z) { X = x; Y = y; Z = z; }

        /// <summary>X component.</summary>
        public double X;

        /// <summary>Y component.</summary>
        public double Y;

        /// <summary>Z component.</summary>
        public double Z;

        /// <summary>Gets the Euclidean length of the vector.</summary>
        public double Length
        {
            get
            {
                // Scale before squaring to avoid overflow/underflow; keep zero and non-finite results.
                double scale = Math.Max(Math.Abs(X), Math.Max(Math.Abs(Y), Math.Abs(Z)));
                if (scale == 0.0 || double.IsNaN(scale) || double.IsInfinity(scale))
                    return Math.Sqrt(X * X + Y * Y + Z * Z);

                double sx = X / scale;
                double sy = Y / scale;
                double sz = Z / scale;
                return scale * Math.Sqrt(sx * sx + sy * sy + sz * sz);
            }
        }

        /// <summary>Returns the component-wise sum of two vectors.</summary>
        public static CxDVector3D operator +(CxDVector3D a, CxDVector3D b) => new CxDVector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>Returns the component-wise difference of two vectors.</summary>
        public static CxDVector3D operator -(CxDVector3D a, CxDVector3D b) => new CxDVector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>Returns the vector scaled by <paramref name="scale"/>.</summary>
        public static CxDVector3D operator *(CxDVector3D v, double scale) => new CxDVector3D(v.X * scale, v.Y * scale, v.Z * scale);

        /// <summary>Returns the vector divided by <paramref name="scale"/>.</summary>
        public static CxDVector3D operator /(CxDVector3D v, double scale) => new CxDVector3D(v.X / scale, v.Y / scale, v.Z / scale);

        /// <summary>Returns the dot (scalar) product of this vector and <paramref name="other"/>.</summary>
        public double Dot(CxDVector3D other) => X * other.X + Y * other.Y + Z * other.Z;

        /// <summary>Returns the cross (vector) product of this vector and <paramref name="other"/>.</summary>
        public CxDVector3D Cross(CxDVector3D other) =>
            new CxDVector3D(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

        /// <summary>Returns a unit vector in the same direction. Behaviour is undefined if length is zero.</summary>
        public CxDVector3D Normalize()
        {
            // Normalise the scaled components rather than dividing by Length: the raw length can
            // overflow or underflow for a direction that is itself perfectly representable.
            double scale = Math.Max(Math.Abs(X), Math.Max(Math.Abs(Y), Math.Abs(Z)));
            if (scale == 0.0 || double.IsNaN(scale) || double.IsInfinity(scale))
                return new CxDVector3D(X / Length, Y / Length, Z / Length);

            double sx = X / scale;
            double sy = Y / scale;
            double sz = Z / scale;
            double norm = Math.Sqrt(sx * sx + sy * sy + sz * sz);
            return new CxDVector3D(sx / norm, sy / norm, sz / norm);
        }

        /// <summary>
        /// Creates a unit direction vector from spherical coordinates (standard math convention).
        /// </summary>
        /// <param name="pitch">Polar angle from the +Z axis, in degrees. Range [0, 180].</param>
        /// <param name="roll">Azimuth angle from the +X axis in the XY plane, in degrees.</param>
        /// <returns>A unit direction vector.</returns>
        public static CxDVector3D FromSpherical(double pitch, double roll)
        {
            double pr = pitch * Math.PI / 180.0;
            double rr = roll  * Math.PI / 180.0;
            double sp = Math.Sin(pr);
            return new CxDVector3D(
                sp * Math.Cos(rr),
                sp * Math.Sin(rr),
                Math.Cos(pr));
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDVector3D(X={0:G17}, Y={1:G17}, Z={2:G17})", X, Y, Z);

        /// <summary>Converts a single-precision vector to double precision (lossless).</summary>
        public static implicit operator CxDVector3D(CxVector3D v) => new CxDVector3D(v.X, v.Y, v.Z);

        /// <summary>Converts this double-precision vector to single precision (may lose precision).</summary>
        public static explicit operator CxVector3D(CxDVector3D v) => new CxVector3D((float)v.X, (float)v.Y, (float)v.Z);
    }
}
