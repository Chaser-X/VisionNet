namespace VisionNet.DataType
{
    /// <summary>Represents a 2D line segment defined by a start point and an end point.</summary>
    public struct CxSegment2D
    {
        /// <summary>Start point of the segment.</summary>
        public CxPoint2D Start;

        /// <summary>End point of the segment.</summary>
        public CxPoint2D End;

        /// <summary>Initializes a segment with the given start and end points.</summary>
        public CxSegment2D(CxPoint2D start, CxPoint2D end) { Start = start; End = end; }
    }
}
