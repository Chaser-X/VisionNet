namespace VisionNet.DataType
{
    /// <summary>A 2D point with single-precision floating-point X and Y coordinates.</summary>
    public struct CxPoint2D
    {
        /// <summary>Initializes a point with the given coordinates.</summary>
        public CxPoint2D(float x, float y) { X = x; Y = y; }

        /// <summary>X coordinate.</summary>
        public float X;

        /// <summary>Y coordinate.</summary>
        public float Y;
    }

    /// <summary>A 2D vector with single-precision floating-point X and Y components.</summary>
    public struct CxVector2D
    {
        /// <summary>Initializes a vector with the given components.</summary>
        public CxVector2D(float x, float y) { X = x; Y = y; }

        /// <summary>X component.</summary>
        public float X;

        /// <summary>Y component.</summary>
        public float Y;
    }
}
