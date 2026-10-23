using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>A 2D point with single-precision floating-point X and Y coordinates.</summary>
    public struct CxPoint2D
    {
        /// <summary>Initializes a point with the given coordinates.</summary>
        public CxPoint2D(float x, float y) { X = x; Y = y; }

        /// <summary>X coordinate.</summary>
        public float X;

        /// <summary>Y coordinate.</summary>
        public float Y;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxPoint2D(X={0:G9}, Y={1:G9})", X, Y);

        public static CxPoint2D operator +(CxPoint2D a, CxVector2D b) => new CxPoint2D(a.X + b.X, a.Y + b.Y);
        public static CxPoint2D operator +(CxPoint2D a, CxPoint2D b)  => new CxPoint2D(a.X + b.X, a.Y + b.Y);
        public static CxPoint2D operator -(CxPoint2D a, CxVector2D b) => new CxPoint2D(a.X - b.X, a.Y - b.Y);
        public static CxVector2D operator -(CxPoint2D a, CxPoint2D b) => new CxVector2D(a.X - b.X, a.Y - b.Y);
        public static CxPoint2D operator *(CxPoint2D p, float s)     => new CxPoint2D(p.X * s, p.Y * s);
        public static CxPoint2D operator *(float s, CxPoint2D p)     => new CxPoint2D(p.X * s, p.Y * s);
        public static CxPoint2D operator /(CxPoint2D p, float s)     => new CxPoint2D(p.X / s, p.Y / s);
    }
}
