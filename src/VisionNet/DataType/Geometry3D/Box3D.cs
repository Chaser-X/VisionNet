namespace VisionNet.DataType
{
    public struct Box3D
    {
        public CxPoint3D Center; public CxSize3D Size;
        public Box3D(CxPoint3D center, CxSize3D size) { Center = center; Size = size; }
    }
}
