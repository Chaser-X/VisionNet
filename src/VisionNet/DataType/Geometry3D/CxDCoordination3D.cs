using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 3D coordinate frame defined by an origin point and three orthogonal axis
    /// vectors, in double precision. Double-precision counterpart of <see cref="CxCoordination3D"/>.
    /// </summary>
    public struct CxDCoordination3D
    {
        /// <summary>Origin of the coordinate frame in world space.</summary>
        public CxDPoint3D Origin;
        /// <summary>Scale factor for the coordinate frame. This can be used to scale the coordinates in the frame.</summary>
        public CxDPoint3D Scale;
        /// <summary>Unit vector along the X axis of this frame.</summary>
        public CxDVector3D XAxis;

        /// <summary>Unit vector along the Y axis of this frame.</summary>
        public CxDVector3D YAxis;

        /// <summary>Unit vector along the Z axis of this frame.</summary>
        public CxDVector3D ZAxis;

        /// <summary>Initializes a coordinate frame with the given origin and axis vectors.</summary>
        public CxDCoordination3D(CxDPoint3D origin, CxDVector3D xAxis, CxDVector3D yAxis, CxDVector3D zAxis)
        {
            Origin = origin;
            Scale = new CxDPoint3D(1, 1, 1);
            XAxis = xAxis;
            YAxis = yAxis;
            ZAxis = zAxis;
        }
        /// <summary>Initializes a coordinate frame with the given origin, scale, and axis vectors.</summary>
        public CxDCoordination3D(CxDPoint3D origin, CxDPoint3D scale, CxDVector3D xAxis, CxDVector3D yAxis, CxDVector3D zAxis)
        {
            Origin = origin;
            Scale = scale;
            XAxis = xAxis;
            YAxis = yAxis;
            ZAxis = zAxis;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDCoordination3D(Origin={0}, Scale={1}, XAxis={2}, YAxis={3}, ZAxis={4})",
                Origin, Scale, XAxis, YAxis, ZAxis);

        /// <summary>Converts a single-precision coordinate frame to double precision (lossless).</summary>
        public static implicit operator CxDCoordination3D(CxCoordination3D c) =>
            new CxDCoordination3D(c.Origin, c.Scale, c.XAxis, c.YAxis, c.ZAxis);

        /// <summary>Converts this double-precision coordinate frame to single precision (may lose precision).</summary>
        public static explicit operator CxCoordination3D(CxDCoordination3D c) =>
            new CxCoordination3D((CxPoint3D)c.Origin, (CxPoint3D)c.Scale,
                (CxVector3D)c.XAxis, (CxVector3D)c.YAxis, (CxVector3D)c.ZAxis);
    }
}
