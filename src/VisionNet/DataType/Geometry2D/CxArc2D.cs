using System.Globalization;

namespace VisionNet.DataType
{
    public struct CxArc2D
    {
        public CxPoint2D Center;
        public float Radius;

        /// <summary>Start angle of the arc, in degrees.</summary>
        public float StartAngle;

        /// <summary>Sweep angle of the arc, in degrees (positive = counter-clockwise).</summary>
        public float SweepAngle;

        /// <summary>Initializes an arc.</summary>
        /// <param name="center">Centre of the arc.</param>
        /// <param name="radius">Radius of the arc.</param>
        /// <param name="startAngle">Start angle in degrees.</param>
        /// <param name="sweepAngle">Sweep angle in degrees (positive = counter-clockwise).</param>
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
