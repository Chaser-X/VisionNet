using System.Globalization;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 3D point with double-precision floating-point X, Y, Z coordinates.
    /// The struct uses an explicit memory layout for direct interop with native code.
    /// Double-precision counterpart of <see cref="CxPoint3D"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct CxDPoint3D
    {
        /// <summary>Initializes a point with the given coordinates.</summary>
        public CxDPoint3D(double x, double y, double z) { X = x; Y = y; Z = z; }

        /// <summary>X coordinate (offset 0).</summary>
        [FieldOffset(0)] public double X;

        /// <summary>Y coordinate (offset 8).</summary>
        [FieldOffset(8)] public double Y;

        /// <summary>Z coordinate (offset 16).</summary>
        [FieldOffset(16)] public double Z;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDPoint3D(X={0:G17}, Y={1:G17}, Z={2:G17})", X, Y, Z);

        public static CxDPoint3D operator +(CxDPoint3D a, CxDVector3D b) => new CxDPoint3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static CxDPoint3D operator +(CxDPoint3D a, CxDPoint3D b)  => new CxDPoint3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static CxDPoint3D operator -(CxDPoint3D a, CxDVector3D b) => new CxDPoint3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static CxDVector3D operator -(CxDPoint3D a, CxDPoint3D b) => new CxDVector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static CxDPoint3D operator *(CxDPoint3D p, double s)      => new CxDPoint3D(p.X * s, p.Y * s, p.Z * s);
        public static CxDPoint3D operator *(double s, CxDPoint3D p)      => new CxDPoint3D(p.X * s, p.Y * s, p.Z * s);
        public static CxDPoint3D operator /(CxDPoint3D p, double s)      => new CxDPoint3D(p.X / s, p.Y / s, p.Z / s);

        /// <summary>Converts a single-precision point to double precision (lossless).</summary>
        public static implicit operator CxDPoint3D(CxPoint3D p) => new CxDPoint3D(p.X, p.Y, p.Z);

        /// <summary>Converts this double-precision point to single precision (may lose precision).</summary>
        public static explicit operator CxPoint3D(CxDPoint3D p) => new CxPoint3D((float)p.X, (float)p.Y, (float)p.Z);
    }
}
