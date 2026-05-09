using System.Collections.Generic;

namespace VisionNet.Controls
{
    public class RenderData
    {
        public float[] Vertices { get; set; }
        public float[] Colors { get; set; }
        public float[] UVCoords { get; set; }
        public uint[] Indices { get; set; }
        public int VertexCount { get; set; }
        public int IndexCount { get; set; }
        public TextureData TextureData { get; set; }
        public ShaderSource ShaderSource { get; set; }
        public bool UseVAO { get; set; }
        public Dictionary<string, object> Uniforms { get; set; } = new Dictionary<string, object>();
    }

    public class TextureData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Data { get; set; }
    }

    public class ShaderSource
    {
        public string VertexSource { get; set; }
        public string FragmentSource { get; set; }
    }
}
