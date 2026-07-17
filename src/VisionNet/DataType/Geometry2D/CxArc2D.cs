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
    }
}
