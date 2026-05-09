namespace VisionNet.DataType
{
    /// <summary>
    /// Represents an ordered sequence of 2D points forming either an open polyline
    /// or a closed polygon, depending on <see cref="IsClosed"/>.
    /// </summary>
    public struct Polygon2D
    {
        /// <summary>Ordered array of vertices.</summary>
        public CxPoint2D[] Points;

        /// <summary>
        /// <c>true</c> if the last vertex is implicitly connected back to the first vertex
        /// (closed polygon); <c>false</c> for an open polyline.
        /// </summary>
        public bool IsClosed;

        /// <summary>Initializes a polygon with the given vertices and closure flag.</summary>
        public Polygon2D(CxPoint2D[] points, bool isClosed = true)
        {
            Points   = points;
            IsClosed = isClosed;
        }
    }
}
