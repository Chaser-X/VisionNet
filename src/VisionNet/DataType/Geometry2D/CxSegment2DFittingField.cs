using System.Globalization;

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

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxSegment2DFittingField(Axis={0}, Width={1:G9})", Axis, Width);
    }
}
