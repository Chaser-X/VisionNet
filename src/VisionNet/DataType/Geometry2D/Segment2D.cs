namespace VisionNet.DataType
{
    public struct Segment2D
    {
        public CxPoint2D Start; public CxPoint2D End;
        public Segment2D(CxPoint2D start, CxPoint2D end) { Start = start; End = end; }
    }
}
