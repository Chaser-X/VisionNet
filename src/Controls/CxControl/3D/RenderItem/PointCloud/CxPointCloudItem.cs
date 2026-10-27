using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Modern shader based point-cloud renderer.</summary>
    /// <remarks>Uses the shared VAO/VBO and GLSL renderer.</remarks>
    public class CxPointCloudItem : CxPointCloudAdvancedItem
    {
        public CxPointCloudItem(CxPointCloud pointCloud,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color,
            int maxPointCount = 2_000_000)
            : base(pointCloud, surfaceMode, surfaceColorMode, maxPointCount) { }
    }
}
