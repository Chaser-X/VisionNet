namespace VisionNet.DataType
{
    /// <summary>Represents a 3D line segment defined by a start point and an end point.</summary>
    public struct CxSegment3D
    {
        /// <summary>Start point of the segment.</summary>
        public CxPoint3D Start;

        /// <summary>End point of the segment.</summary>
        public CxPoint3D End;

        /// <summary>Initializes a segment with the given start and end points.</summary>
        public CxSegment3D(CxPoint3D start, CxPoint3D end) { Start = start; End = end; }
    }
}
