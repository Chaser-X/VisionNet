namespace VisionNet.DataType
{
    /// <summary>Represents an infinite 2D line defined by a point and a direction vector.</summary>
    public struct CxLine2D
    {
        public CxPoint2D  Point;
        public CxVector2D Direction;

        public CxLine2D(CxPoint2D point, CxVector2D direction)
        {
            Point = point;
            Direction = direction;
        }

        /// <summary>Evaluates the line at parameter t: P + t * D.</summary>
        public CxPoint2D GetPoint(float t) => new CxPoint2D(
            Point.X + Direction.X * t,
            Point.Y + Direction.Y * t);

        /// <summary>Creates a line passing through two points.</summary>
        public static CxLine2D FromTwoPoints(CxPoint2D p1, CxPoint2D p2) =>
            new CxLine2D(p1, new CxVector2D(p2.X - p1.X, p2.Y - p1.Y));

        /// <summary>Creates a line from X and Y axis intercepts: x/a + y/b = 1.</summary>
        public static CxLine2D FromIntercept(float xIntercept, float yIntercept) =>
            new CxLine2D(new CxPoint2D(xIntercept, 0), new CxVector2D(-xIntercept, yIntercept));

        /// <summary>Creates a line from a point and a slope: y - y₀ = slope * (x - x₀).</summary>
        public static CxLine2D FromPointSlope(CxPoint2D point, float slope) =>
            new CxLine2D(point, new CxVector2D(1f, slope));
    }
}
