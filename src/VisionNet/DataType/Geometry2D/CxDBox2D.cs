using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// An axis-aligned bounding box in 2D space defined by its centre point and size,
    /// in double precision. Double-precision counterpart of <see cref="CxBox2D"/>.
    /// </summary>
    public struct CxDBox2D
    {
        /// <summary>World-space centre of the box.</summary>
        public CxDPoint2D Center;

        /// <summary>Full width and height of the box.</summary>
        public CxDSize2D Size;

        /// <summary>Initializes a box with the given centre and size.</summary>
        public CxDBox2D(CxDPoint2D center, CxDSize2D size) { Center = center; Size = size; }

        /// <summary>X coordinate of the left edge.</summary>
        public double Left   => Center.X - Size.Width  / 2.0;

        /// <summary>X coordinate of the right edge.</summary>
        public double Right  => Center.X + Size.Width  / 2.0;

        /// <summary>Y coordinate of the top edge (smaller Y in image convention).</summary>
        public double Top    => Center.Y - Size.Height / 2.0;

        /// <summary>Y coordinate of the bottom edge (larger Y in image convention).</summary>
        public double Bottom => Center.Y + Size.Height / 2.0;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxDBox2D(Center={0}, Size={1})", Center, Size);

        /// <summary>Converts a single-precision box to double precision (lossless).</summary>
        public static implicit operator CxDBox2D(CxBox2D b) => new CxDBox2D(b.Center, b.Size);

        /// <summary>Converts this double-precision box to single precision (may lose precision).</summary>
        public static explicit operator CxBox2D(CxDBox2D b) =>
            new CxBox2D((CxPoint2D)b.Center, (CxSize2D)b.Size);
    }
}
