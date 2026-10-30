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

        /// <summary>Colour is derived from the per-vertex difference value using the rainbow colour map.</summary>
        Diff,

        /// <summary>
        /// Flat colour mode: the item is drawn in a single caller-chosen colour
        /// (<see cref="AbstractCxObjRenderItem.SolidColor"/>) instead of a data-driven colour map.
        /// In <see cref="SurfaceMode.Mesh"/> the colour is modulated by per-fragment Blinn-Phong
        /// lighting (the normal is evaluated in GLSL via screen-space derivatives of the
        /// view-space position); in <see cref="SurfaceMode.PointCloud"/> it is drawn flat.
        /// </summary>
        Lit,
    }
}
