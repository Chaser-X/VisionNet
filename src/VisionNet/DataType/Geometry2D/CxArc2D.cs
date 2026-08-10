using System.Globalization;

namespace VisionNet.DataType
{
    public struct CxArc2D
    {
        public CxPoint2D Center;
        public float Radius;
        public float StartAngle;
        public float SweepAngle;

        public CxArc2D(CxPoint2D center, float radius, float startAngle, float sweepAngle)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            SweepAngle = sweepAngle;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxArc2D(Center={0}, Radius={1:G9}, StartAngle={2:G9}, SweepAngle={3:G9})",
                Center, Radius, StartAngle, SweepAngle);
    }
}
