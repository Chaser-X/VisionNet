namespace VisionNet.DataType
{
    public struct CxSegment2DFittingField
    {
        public CxSegment2D Axis;
        public float Width;

        public CxSegment2DFittingField(CxSegment2D axis, float width)
        {
            Axis = axis;
            Width = width;
        }
    }
}
