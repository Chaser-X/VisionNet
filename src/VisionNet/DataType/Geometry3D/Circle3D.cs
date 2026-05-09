namespace VisionNet.DataType
{
    public struct Circle3D
    {
        public CxPoint3D Center; public float Radius; public CxVector3D Normal;
        public Circle3D(CxPoint3D center, CxVector3D normal, float radius) { Center = center; Radius = radius; Normal = normal; }
    }
}
