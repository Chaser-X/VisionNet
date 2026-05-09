namespace VisionNet.DataType
{
    public struct CxCoordination3D
    {
        public CxPoint3D Origin; public CxVector3D XAxis; public CxVector3D YAxis; public CxVector3D ZAxis;
        public CxCoordination3D(CxPoint3D origin, CxVector3D xAxis, CxVector3D yAxis, CxVector3D zAxis) { Origin = origin; XAxis = xAxis; YAxis = yAxis; ZAxis = zAxis; }
    }
}
