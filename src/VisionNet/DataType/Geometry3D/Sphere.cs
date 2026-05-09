namespace VisionNet.DataType
{
    /// <summary>Represents a 3D sphere defined by its centre point and radius.</summary>
    public struct Sphere
    {
        /// <summary>World-space centre of the sphere.</summary>
        public CxPoint3D Center;

        /// <summary>Radius of the sphere.</summary>
        public float Radius;

        /// <summary>Initializes a sphere with the given centre and radius.</summary>
        public Sphere(CxPoint3D center, float radius) { Center = center; Radius = radius; }
    }
}
