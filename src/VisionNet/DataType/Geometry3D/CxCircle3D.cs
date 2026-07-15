namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 3D circle defined by its centre, the unit normal of its plane, and its radius.
    /// </summary>
    public struct CxCircle3D
    {
        /// <summary>World-space centre of the circle.</summary>
        public CxPoint3D Center;

        /// <summary>Radius of the circle.</summary>
        public float Radius;

        /// <summary>Unit normal of the plane in which the circle lies.</summary>
        public CxVector3D Normal;

        /// <summary>Initializes a circle with the given centre, normal, and radius.</summary>
        public CxCircle3D(CxPoint3D center, CxVector3D normal, float radius)
        {
            Center = center;
            Normal = normal;
            Radius = radius;
        }
    }
}
