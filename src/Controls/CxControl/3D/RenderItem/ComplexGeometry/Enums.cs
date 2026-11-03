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
        /// lighting. The normal is the interpolated per-vertex normal when the item built one
        /// (indexed mesh, or a structured surface / ordered point cloud in Mesh mode), and falls
        /// back to the GLSL screen-space derivative of the view-space position otherwise.
        /// In <see cref="SurfaceMode.PointCloud"/> the colour is drawn flat.
        /// </summary>
        Lit,
    }

    /// <summary>
    /// Specifies how a Mesh-mode surface item is lit. Applies only when the item is drawn as a
    /// mesh (<see cref="SurfaceMode.Mesh"/>); point clouds are drawn unlit.
    /// </summary>
    public enum SurfaceLightingMode
    {
        /// <summary>
        /// Per-fragment Blinn-Phong with a fixed view-space key light (the historical look).
        /// </summary>
        Legacy = 0,

        /// <summary>
        /// Physically-based shading with image-based lighting (split-sum: irradiance + prefiltered
        /// specular + BRDF LUT). The environment is generated procedurally; see
        /// <see cref="SurfaceEnvironmentPreset"/>.
        /// </summary>
        Pbr = 1,
    }
}
