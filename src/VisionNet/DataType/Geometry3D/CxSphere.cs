using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>Represents a 3D sphere defined by its centre point and radius.</summary>
    public struct CxSphere
    {
        /// <summary>World-space centre of the sphere.</summary>
        public CxPoint3D Center;

        /// <summary>Radius of the sphere.</summary>
        public float Radius;

        /// <summary>Initializes a sphere with the given centre and radius.</summary>
        public CxSphere(CxPoint3D center, float radius) { Center = center; Radius = radius; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "CxSphere(Center={0}, Radius={1:G9})", Center, Radius);
    }
}
