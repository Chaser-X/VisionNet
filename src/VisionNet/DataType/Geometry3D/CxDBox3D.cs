using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// An axis-aligned bounding box defined by its centre point and half-extents,
    /// in double precision. Double-precision counterpart of <see cref="CxBox3D"/>.
    /// </summary>
    public struct CxDBox3D
    {
        /// <summary>World-space centre of the box.</summary>
        public CxDPoint3D Center;

        /// <summary>Full width, height, and depth of the box.</summary>
        public CxDSize3D Size;

        /// <summary>Initializes a box with the given centre and size.</summary>
        public CxDBox3D(CxDPoint3D center, CxDSize3D size) { Center = center; Size = size; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxDBox3D(Center={0}, Size={1})", Center, Size);

        /// <summary>Converts a single-precision box to double precision (lossless).</summary>
        public static implicit operator CxDBox3D(CxBox3D b) => new CxDBox3D(b.Center, b.Size);

        /// <summary>Converts this double-precision box to single precision (may lose precision).</summary>
        public static explicit operator CxBox3D(CxDBox3D b) =>
            new CxBox3D((CxPoint3D)b.Center, (CxSize3D)b.Size);
    }
}
