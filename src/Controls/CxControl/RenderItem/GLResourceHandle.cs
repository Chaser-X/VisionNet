namespace VisionNet.Controls
{
    public class GLResourceHandle
    {
        public uint[] VboIds { get; set; } = new uint[3];
        public uint VaoId { get; set; }
        public uint ElementBufferId { get; set; }
        public uint ShaderProgram { get; set; }
        public uint TextureId { get; set; }
        public bool IsValid { get; set; }
        public bool NeedsUpdate { get; set; }
        public bool HasVAO { get; set; }
        public bool HasShader { get; set; }
        public bool HasTexture { get; set; }
        public bool HasEBO { get; set; }
        public int VboCount { get; set; }
        public bool UseUVMode { get; set; }
    }
}
