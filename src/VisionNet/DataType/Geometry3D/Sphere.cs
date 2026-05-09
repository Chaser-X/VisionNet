namespace VisionNet.DataType
{
    public struct Sphere
    {
        public CxPoint3D Center; public float Radius;
        public Sphere(CxPoint3D center, float radius) { Center = center; Radius = radius; }
    }
}
