namespace VisionNet.DataType
{
    /// <summary>Represents an infinite 3D line defined by a point and a direction vector.</summary>
    public struct CxLine3D
    {
        public CxPoint3D  Point;
        public CxVector3D Direction;

        public CxLine3D(CxPoint3D point, CxVector3D direction)
        {
            Point = point;
            Direction = direction;
        }

        /// <summary>Evaluates the line at parameter t: P + t * D.</summary>
        public CxPoint3D GetPoint(float t) => new CxPoint3D(
            Point.X + Direction.X * t,
            Point.Y + Direction.Y * t,
            Point.Z + Direction.Z * t);

        /// <summary>Creates a line passing through two points.</summary>
        public static CxLine3D FromTwoPoints(CxPoint3D p1, CxPoint3D p2) =>
            new CxLine3D(p1, new CxVector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z));

        /// <summary>Creates a line from axis intercepts: x/a + y/b + z/c = 1.</summary>
        public static CxLine3D FromIntercept(float xIntercept, float yIntercept, float zIntercept) =>
            new CxLine3D(
                new CxPoint3D(xIntercept, 0, 0),
                new CxVector3D(-xIntercept, yIntercept, zIntercept));
    }
}
