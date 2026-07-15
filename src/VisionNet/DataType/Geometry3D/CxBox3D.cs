namespace VisionNet.DataType
{
    /// <summary>An axis-aligned bounding box defined by its centre point and half-extents.</summary>
    public struct CxBox3D
    {
        /// <summary>World-space centre of the box.</summary>
        public CxPoint3D Center;

        /// <summary>Full width, height, and depth of the box.</summary>
        public CxSize3D Size;

        /// <summary>Initializes a box with the given centre and size.</summary>
        public CxBox3D(CxPoint3D center, CxSize3D size) { Center = center; Size = size; }
    }
}
