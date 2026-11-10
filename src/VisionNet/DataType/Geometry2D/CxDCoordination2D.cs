using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 2D coordinate frame defined by an origin point and a rotation angle,
    /// in double precision. Double-precision counterpart of <see cref="CxCoordination2D"/>.
    /// </summary>
    public struct CxDCoordination2D
    {
        /// <summary>Origin of the coordinate frame in 2D space.</summary>
        public CxDPoint2D Origin;
        /// <summary>
        /// Scale factor for the coordinate frame. This can be used to scale the coordinates in the frame.
        /// </summary>
        public CxDPoint2D Scale;
        /// <summary>Rotation angle in degrees.</summary>
        public double Angle;

        /// <summary>Initializes a coordinate frame with the given origin and rotation angle (in degrees).</summary>
        public CxDCoordination2D(CxDPoint2D origin, double angleDeg = 0)
        {
            Origin = origin;
            Scale = new CxDPoint2D(1, 1);
            Angle = angleDeg;
        }
        /// <summary>Initializes a coordinate frame with the given origin, scale, and rotation angle (in degrees).</summary>
        public CxDCoordination2D(CxDPoint2D origin, CxDPoint2D scale, double angleDeg = 0)
        {
            Origin = origin;
            Scale = scale;
            Angle = angleDeg;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDCoordination2D(Origin={0}, Scale={1}, Angle={2:G17})", Origin, Scale, Angle);

        /// <summary>Converts a single-precision coordinate frame to double precision (lossless).</summary>
        public static implicit operator CxDCoordination2D(CxCoordination2D c) =>
            new CxDCoordination2D(c.Origin, c.Scale, c.Angle);

        /// <summary>Converts this double-precision coordinate frame to single precision (may lose precision).</summary>
        public static explicit operator CxCoordination2D(CxDCoordination2D c) =>
            new CxCoordination2D((CxPoint2D)c.Origin, (CxPoint2D)c.Scale, (float)c.Angle);
    }
}
