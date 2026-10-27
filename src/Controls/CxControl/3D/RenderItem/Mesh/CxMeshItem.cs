using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Modern shader based mesh renderer.</summary>
    /// <remarks>Uses the shared VAO/VBO and GLSL renderer.</remarks>
    public class CxMeshItem : CxMeshAdvancedItem
    {
        public CxMeshItem(CxMesh mesh,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
            : base(mesh, surfaceMode, surfaceColorMode) { }
    }
}
