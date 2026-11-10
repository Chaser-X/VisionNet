using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// Represents the dimensions of a 2D region: width (X) and height (Y), in double precision.
    /// Double-precision counterpart of <see cref="CxSize2D"/>.
    /// </summary>
    public struct CxDSize2D
    {
        /// <summary>Initializes a size with the given dimensions.</summary>
        public CxDSize2D(double width, double height) { Width = width; Height = height; }

        /// <summary>Extent along the X axis.</summary>
        public double Width;

        /// <summary>Extent along the Y axis.</summary>
        public double Height;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxDSize2D(Width={0:G17}, Height={1:G17})", Width, Height);

        /// <summary>Converts a single-precision size to double precision (lossless).</summary>
        public static implicit operator CxDSize2D(CxSize2D s) => new CxDSize2D(s.Width, s.Height);

        /// <summary>Converts this double-precision size to single precision (may lose precision).</summary>
        public static explicit operator CxSize2D(CxDSize2D s) => new CxSize2D((float)s.Width, (float)s.Height);
    }
}
