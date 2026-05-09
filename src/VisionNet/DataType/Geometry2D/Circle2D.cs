namespace VisionNet.DataType
{
    public struct Circle2D
    {
        public CxPoint2D Center; public float Radius;
        public Circle2D(CxPoint2D center, float radius) { Center = center; Radius = radius; }
    }
}
