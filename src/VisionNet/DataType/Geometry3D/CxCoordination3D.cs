namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 3D coordinate frame defined by an origin point and three orthogonal axis vectors.
    /// Used to express sensor poses, tool frames, and custom reference frames.
    /// </summary>
    public struct CxCoordination3D
    {
        /// <summary>Origin of the coordinate frame in world space.</summary>
        public CxPoint3D Origin;

        /// <summary>Unit vector along the X axis of this frame.</summary>
        public CxVector3D XAxis;

        /// <summary>Unit vector along the Y axis of this frame.</summary>
        public CxVector3D YAxis;

        /// <summary>Unit vector along the Z axis of this frame.</summary>
        public CxVector3D ZAxis;

        /// <summary>Initializes a coordinate frame with the given origin and axis vectors.</summary>
        public CxCoordination3D(CxPoint3D origin, CxVector3D xAxis, CxVector3D yAxis, CxVector3D zAxis)
        {
            Origin = origin;
            XAxis  = xAxis;
            YAxis  = yAxis;
            ZAxis  = zAxis;
        }
    }
}
