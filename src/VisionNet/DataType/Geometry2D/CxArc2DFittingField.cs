using System.Globalization;

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

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxArc2DFittingField(Axis={0}, Width={1:G9})", Axis, Width);
    }
}
