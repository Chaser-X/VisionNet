using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 2D point with double-precision floating-point X and Y coordinates.
    /// Double-precision counterpart of <see cref="CxPoint2D"/>.
    /// </summary>
    public struct CxDPoint2D
    {
        /// <summary>Initializes a point with the given coordinates.</summary>
        public CxDPoint2D(double x, double y) { X = x; Y = y; }

        /// <summary>X coordinate.</summary>
        public double X;

        /// <summary>Y coordinate.</summary>
        public double Y;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxDPoint2D(X={0:G17}, Y={1:G17})", X, Y);

        public static CxDPoint2D operator +(CxDPoint2D a, CxDVector2D b) => new CxDPoint2D(a.X + b.X, a.Y + b.Y);
        public static CxDPoint2D operator +(CxDPoint2D a, CxDPoint2D b)  => new CxDPoint2D(a.X + b.X, a.Y + b.Y);
        public static CxDPoint2D operator -(CxDPoint2D a, CxDVector2D b) => new CxDPoint2D(a.X - b.X, a.Y - b.Y);
        public static CxDVector2D operator -(CxDPoint2D a, CxDPoint2D b) => new CxDVector2D(a.X - b.X, a.Y - b.Y);
        public static CxDPoint2D operator *(CxDPoint2D p, double s)      => new CxDPoint2D(p.X * s, p.Y * s);
        public static CxDPoint2D operator *(double s, CxDPoint2D p)      => new CxDPoint2D(p.X * s, p.Y * s);
        public static CxDPoint2D operator /(CxDPoint2D p, double s)      => new CxDPoint2D(p.X / s, p.Y / s);

        /// <summary>Converts a single-precision point to double precision (lossless).</summary>
        public static implicit operator CxDPoint2D(CxPoint2D p) => new CxDPoint2D(p.X, p.Y);

        /// <summary>Converts this double-precision point to single precision (may lose precision).</summary>
        public static explicit operator CxPoint2D(CxDPoint2D p) => new CxPoint2D((float)p.X, (float)p.Y);
    }
}
