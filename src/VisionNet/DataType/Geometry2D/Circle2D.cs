namespace VisionNet.DataType
{
    /// <summary>Represents a 2D circle defined by its centre point and radius.</summary>
    public struct Circle2D
    {
        /// <summary>Centre of the circle in 2D space.</summary>
        public CxPoint2D Center;

        /// <summary>Radius of the circle.</summary>
        public float Radius;

        /// <summary>Initializes a circle with the given centre and radius.</summary>
        public Circle2D(CxPoint2D center, float radius) { Center = center; Radius = radius; }
    }
}
