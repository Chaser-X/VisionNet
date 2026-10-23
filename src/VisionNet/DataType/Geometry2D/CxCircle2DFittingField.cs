using System.Globalization;

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

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxCircle2DFittingField(Axis={0}, Width={1:G9})", Axis, Width);
    }
}
