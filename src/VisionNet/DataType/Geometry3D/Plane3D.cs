namespace VisionNet.DataType
{
    public struct Plane3D
    {
        public CxPoint3D Point; public CxVector3D Normal;
        public Plane3D(CxPoint3D point, CxVector3D normal) { Point = point; Normal = normal; }
    }
}
