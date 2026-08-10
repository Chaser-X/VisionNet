using System.Globalization;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>Represents the dimensions of a 3D region: width (X), height (Y), and depth (Z).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CxSize3D
    {
        /// <summary>Initializes a size with the given dimensions.</summary>
        public CxSize3D(float width, float height, float depth)
        {
            Width  = width;
            Height = height;
            Depth  = depth;
        }

        /// <summary>Extent along the X axis.</summary>
        public float Width;

        /// <summary>Extent along the Y axis.</summary>
        public float Height;

        /// <summary>Extent along the Z axis.</summary>
        public float Depth;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxSize3D(Width={0:G9}, Height={1:G9}, Depth={2:G9})", Width, Height, Depth);
    }
}
