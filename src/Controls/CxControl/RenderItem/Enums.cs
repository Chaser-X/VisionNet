namespace VisionNet.Controls
{
    /// <summary>Specifies how surface geometry is rendered.</summary>
    public enum SurfaceMode
    {
        /// <summary>Render each vertex as an individual point.</summary>
        PointCloud = 1,

        /// <summary>Render the geometry as a triangle mesh.</summary>
        Mesh = 2,
    }

    /// <summary>Specifies how surface vertices are coloured.</summary>
    public enum SurfaceColorMode
    {
        /// <summary>Colour is derived from the vertex Z height using the rainbow colour map.</summary>
        Color,

        /// <summary>Colour is derived from the per-vertex intensity value (grayscale).</summary>
        Intensity,

        /// <summary>Colour blends height-based colour with per-vertex intensity.</summary>
        ColorWithIntensity,
    }
}
