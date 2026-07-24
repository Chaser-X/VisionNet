namespace VisionNet.DataType
{
    public struct CxCircle2DFittingField
    {
        public CxCircle2D Axis;
        public float Width;

        public CxCircle2DFittingField(CxCircle2D axis, float width)
        {
            Axis = axis;
            Width = width;
        }
    }
}
