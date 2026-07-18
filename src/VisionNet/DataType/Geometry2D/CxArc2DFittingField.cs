namespace VisionNet.DataType
{
    public struct CxArc2DFittingField
    {
        public CxArc2D Axis;
        public float Width;

        public CxArc2DFittingField(CxArc2D axis, float width)
        {
            Axis = axis;
            Width = width;
        }
    }
}
