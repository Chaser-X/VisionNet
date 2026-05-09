namespace VisionNet.DataType
{
    public struct Segment3D
    {
        public CxPoint3D Start; public CxPoint3D End;
        public Segment3D(CxPoint3D start, CxPoint3D end) { Start = start; End = end; }
    }
}
