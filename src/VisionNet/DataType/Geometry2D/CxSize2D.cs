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
    }
}
