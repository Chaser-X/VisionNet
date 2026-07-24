namespace VisionNet.Controls
{
    /// <summary>
    /// Stores all OpenGL resource IDs managed by <see cref="CxDisplay"/> on behalf of one render item.
    /// Items never hold or release these IDs directly; <c>CxDisplay</c> owns their lifecycle.
    /// </summary>
    public class GLResourceHandle
    {
        /// <summary>
        /// VBO IDs for vertex data. Index 0 = positions, 1 = colours or UVs, 2 = reserved.
        /// Allocated count is stored in <see cref="VboCount"/>.
        /// </summary>
        public uint[] VboIds { get; set; } = new uint[3];

        /// <summary>Vertex Array Object ID. Valid only when <see cref="HasVAO"/> is <c>true</c>.</summary>
        public uint VaoId { get; set; }

        /// <summary>Element Buffer Object ID. Valid only when <see cref="HasEBO"/> is <c>true</c>.</summary>
        public uint ElementBufferId { get; set; }

        /// <summary>Linked shader program ID. Valid only when <see cref="HasShader"/> is <c>true</c>.</summary>
        public uint ShaderProgram { get; set; }

        /// <summary>2D texture ID. Valid only when <see cref="HasTexture"/> is <c>true</c>.</summary>
        public uint TextureId { get; set; }

        /// <summary>
        /// <c>true</c> once all GL resources have been successfully created and are ready to draw.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Set to <c>true</c> when the item's CPU data changed and GPU buffers must be rebuilt.
        /// Cleared by <see cref="CxDisplay"/> after recreation.
        /// </summary>
        public bool NeedsUpdate { get; set; }

        /// <summary>Whether a VAO was created for this handle.</summary>
        public bool HasVAO { get; set; }

        /// <summary>Whether a shader program was compiled and linked for this handle.</summary>
        public bool HasShader { get; set; }

        /// <summary>Whether a 2D texture was uploaded for this handle.</summary>
        public bool HasTexture { get; set; }

        /// <summary>Whether an Element Buffer Object (index buffer) was created for this handle.</summary>
        public bool HasEBO { get; set; }

        /// <summary>Number of VBOs actually allocated in <see cref="VboIds"/>.</summary>
        public int VboCount { get; set; }

        /// <summary>
        /// <c>true</c> when the second VBO slot contains UV coordinates rather than RGB colours.
        /// </summary>
        public bool UseUVMode { get; set; }
    }
}
