namespace VisionNet.DataType
{
    /// <summary>An axis-aligned bounding box in 2D space defined by its centre point and size.</summary>
    public struct CxBox2D
    {
        /// <summary>World-space centre of the box.</summary>
        public CxPoint2D Center;

        /// <summary>Full width and height of the box.</summary>
        public CxSize2D Size;

        /// <summary>Initializes a box with the given centre and size.</summary>
        public CxBox2D(CxPoint2D center, CxSize2D size) { Center = center; Size = size; }

        /// <summary>X coordinate of the left edge.</summary>
        public float Left   => Center.X - Size.Width  / 2f;

        /// <summary>X coordinate of the right edge.</summary>
        public float Right  => Center.X + Size.Width  / 2f;

        /// <summary>Y coordinate of the top edge (smaller Y in image convention).</summary>
        public float Top    => Center.Y - Size.Height / 2f;

        /// <summary>Y coordinate of the bottom edge (larger Y in image convention).</summary>
        public float Bottom => Center.Y + Size.Height / 2f;
    }
}
