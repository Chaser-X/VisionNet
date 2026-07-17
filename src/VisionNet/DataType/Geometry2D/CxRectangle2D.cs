namespace VisionNet.DataType
{
    public struct CxRectangle2D
    {
        public CxPoint2D Center;
        public CxSize2D Size;
        public float Angle;

        public CxRectangle2D(CxPoint2D center, CxSize2D size, float angle = 0f)
        {
            Center = center;
            Size = size;
            Angle = angle;
        }
    }
}
