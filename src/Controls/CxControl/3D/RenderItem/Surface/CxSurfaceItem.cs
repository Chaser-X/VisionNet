using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Modern shader based structured-surface renderer.</summary>
    /// <remarks>Uses the shared VAO/VBO and GLSL renderer.</remarks>
    public class CxSurfaceItem : CxSurfaceAdvancedItem
    {
        public CxSurfaceItem(CxSurface surface,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color,
            int maxPointCount = 2_000_000)
            : base(surface, surfaceMode, surfaceColorMode, maxPointCount) { }
    }
}
