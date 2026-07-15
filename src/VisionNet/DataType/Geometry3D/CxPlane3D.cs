namespace VisionNet.DataType
{
    /// <summary>
    /// Represents an infinite 3D plane defined by a point on the plane and its unit normal vector.
    /// </summary>
    public struct CxPlane3D
    {
        /// <summary>A point that lies on the plane.</summary>
        public CxPoint3D Point;

        /// <summary>The outward-facing unit normal of the plane.</summary>
        public CxVector3D Normal;

        /// <summary>Initializes a plane with the given point and normal.</summary>
        public CxPlane3D(CxPoint3D point, CxVector3D normal) { Point = point; Normal = normal; }
    }
}
