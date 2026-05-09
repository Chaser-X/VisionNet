namespace VisionNet.DataType
{
    public struct Polygon3D
    {
        public CxPoint3D[] Points; public bool IsClosed;
        public Polygon3D(CxPoint3D[] points, bool isClosed = true) { Points = points; IsClosed = isClosed; }
    }
}
