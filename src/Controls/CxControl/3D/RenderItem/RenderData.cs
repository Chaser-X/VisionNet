using System.Collections.Generic;

namespace VisionNet.Controls
{
    /// <summary>
    /// CPU-side render data produced by <see cref="ICxObjRenderItem.PrepareRenderData"/>.
    /// <see cref="CxDisplay"/> reads these arrays to create or update GPU buffers.
    /// No OpenGL calls are made when building or reading this object.
    /// </summary>
    public class RenderData
    {
        /// <summary>
        /// Interleaved vertex positions as <c>float[n × 3]</c> (X, Y, Z per vertex),
        /// or <c>null</c> if positions are handled differently.
        /// </summary>
        public float[] Vertices { get; set; }

        /// <summary>
        /// Per-vertex RGB colours as <c>float[n × 3]</c>, or <c>null</c> when UV/texture is used instead.
        /// </summary>
        public float[] Colors { get; set; }

        /// <summary>
        /// Per-vertex texture UV coordinates as <c>float[n × 2]</c>,
        /// or <c>null</c> when vertex colours are used.
        /// </summary>
        public float[] UVCoords { get; set; }

        /// <summary>Triangle index buffer, or <c>null</c> for non-indexed (point-cloud) data.</summary>
        public uint[] Indices { get; set; }

        /// <summary>Number of vertices in <see cref="Vertices"/>.</summary>
        public int VertexCount { get; set; }

        /// <summary>Number of indices in <see cref="Indices"/>.</summary>
        public int IndexCount { get; set; }

        /// <summary>Intensity texture for the shader path, or <c>null</c> for the fixed-function path.</summary>
        public TextureData TextureData { get; set; }

        /// <summary>GLSL shader sources, or <c>null</c> to use the fixed-function pipeline.</summary>
        public ShaderSource ShaderSource { get; set; }

        /// <summary>
        /// When <c>true</c>, <see cref="CxDisplay"/> creates a VAO and sets up vertex attribute pointers.
        /// When <c>false</c>, the legacy <c>glVertexPointer</c> / <c>glColorPointer</c> path is used.
        /// </summary>
        public bool UseVAO { get; set; }

        /// <summary>
        /// Named uniform values passed to the shader each frame.
        /// Supported value types: <see cref="float"/>, <see cref="int"/>.
        /// </summary>
        public Dictionary<string, object> Uniforms { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>Raw RGBA pixel data for an OpenGL 2D texture.</summary>
    public class TextureData
    {
        /// <summary>Texture width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Texture height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>RGBA pixel bytes, row-major, 4 bytes per pixel.</summary>
        public byte[] Data { get; set; }
    }

    /// <summary>GLSL vertex and fragment shader source code pair.</summary>
    public class ShaderSource
    {
        /// <summary>GLSL source for the vertex shader.</summary>
        public string VertexSource { get; set; }

        /// <summary>GLSL source for the fragment shader.</summary>
        public string FragmentSource { get; set; }
    }
}
