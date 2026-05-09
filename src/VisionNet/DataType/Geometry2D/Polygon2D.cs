namespace VisionNet.DataType
{
    public struct Polygon2D
    {
        public CxPoint2D[] Points; public bool IsClosed;
        public Polygon2D(CxPoint2D[] points, bool isClosed = true) { Points = points; IsClosed = isClosed; }
    }
}
