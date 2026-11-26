using System.Globalization;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// Represents the dimensions of a 3D region: width (X), height (Y), and depth (Z),
    /// in double precision. Double-precision counterpart of <see cref="CxSize3D"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CxDSize3D
    {
        /// <summary>Initializes a size with the given dimensions.</summary>
        public CxDSize3D(double width, double height, double depth)
        {
            Width  = width;
            Height = height;
            Depth  = depth;
        }

        /// <summary>Extent along the X axis.</summary>
        public double Width;

        /// <summary>Extent along the Y axis.</summary>
        public double Height;

        /// <summary>Extent along the Z axis.</summary>
        public double Depth;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDSize3D(Width={0:G17}, Height={1:G17}, Depth={2:G17})", Width, Height, Depth);

        /// <summary>Converts a single-precision size to double precision (lossless).</summary>
        public static implicit operator CxDSize3D(CxSize3D s) => new CxDSize3D(s.Width, s.Height, s.Depth);

        /// <summary>Converts this double-precision size to single precision (may lose precision).</summary>
        public static explicit operator CxSize3D(CxDSize3D s) => new CxSize3D((float)s.Width, (float)s.Height, (float)s.Depth);
    }
}
