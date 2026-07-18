namespace VisionNet.DataType
{
    public struct CxPolygon2DFittingField
    {
        public CxPolygon2D Axis;
        public float Width;

        public CxPolygon2DFittingField(CxPolygon2D axis, float width)
        {
            Axis = axis;
            Width = width;
        }
    }
}
