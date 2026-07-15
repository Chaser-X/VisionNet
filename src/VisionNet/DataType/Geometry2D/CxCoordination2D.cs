namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 2D coordinate frame defined by an origin point and a rotation angle.
    /// </summary>
    public struct CxCoordination2D
    {
        /// <summary>Origin of the coordinate frame in 2D space.</summary>
        public CxPoint2D Origin;

        /// <summary>Rotation angle in degrees.</summary>
        public float Angle;

        /// <summary>Initializes a coordinate frame with the given origin and angle.</summary>
        public CxCoordination2D(CxPoint2D origin, float angleDeg)
        {
            Origin = origin;
            Angle  = angleDeg;
        }
    }
}
