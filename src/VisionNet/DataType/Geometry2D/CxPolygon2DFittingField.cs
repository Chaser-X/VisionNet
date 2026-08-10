using System.Globalization;

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

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxPolygon2DFittingField(Axis={0}, Width={1:G9})", Axis, Width);
    }
}
