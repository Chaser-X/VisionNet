using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>Represents the dimensions of a 2D region: width (X) and height (Y).</summary>
    public struct CxSize2D
    {
        /// <summary>Initializes a size with the given dimensions.</summary>
        public CxSize2D(float width, float height) { Width = width; Height = height; }

        /// <summary>Extent along the X axis.</summary>
        public float Width;

        /// <summary>Extent along the Y axis.</summary>
        public float Height;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxSize2D(Width={0:G9}, Height={1:G9})", Width, Height);
    }
}
