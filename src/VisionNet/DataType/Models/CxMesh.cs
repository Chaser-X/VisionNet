namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 3D triangle mesh with vertex positions, indices, optional UV coordinates,
    /// and optional per-vertex intensity data.
    /// </summary>
    public class CxMesh
    {
        /// <summary>Initializes an empty mesh.</summary>
        public CxMesh() { }

        /// <summary>Gets or sets the vertex positions of the mesh.</summary>
        public CxPoint3D[] Vertices { get; set; }

        /// <summary>Gets or sets the triangle index buffer (three indices per triangle).</summary>
        public uint[] Indices { get; set; }

        /// <summary>Gets or sets per-vertex intensity values (0–255), or <c>null</c> if not available.</summary>
        public byte[] Intensity { get; set; }

        /// <summary>Gets or sets the width (columns) of the intensity texture.</summary>
        public int TextureWidth { get; set; } = 0;

        /// <summary>Gets or sets the height (rows) of the intensity texture.</summary>
        public int TextureHeight { get; set; } = 0;

        /// <summary>Gets or sets UV texture coordinates, one per vertex.</summary>
        public CxPoint2D[] UVs { get; set; }

        /// <summary>Releases all managed arrays held by this mesh.</summary>
        public void Dispose()
        {
            Vertices = null;
            Indices = null;
            Intensity = null;
            UVs = null;
        }
    }
}
