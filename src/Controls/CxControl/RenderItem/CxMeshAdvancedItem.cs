using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxMeshAdvancedItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxMesh Mesh { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }

        private SurfaceMode _surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set => _surfaceMode = value;
        }

        private SurfaceColorMode _surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get => _surfaceColorMode;
            set
            {
                if (_surfaceColorMode != value)
                {
                    _surfaceColorMode = value;
                    if (_cachedRenderData?.Uniforms != null)
                        _cachedRenderData.Uniforms["colorMode"] = (int)value;
                }
            }
        }

        private RenderData _cachedRenderData;

        #region Shader 源码（与 CxSurfaceAdvancedItem 内容一致，为解耦独立维护）
        internal static readonly string VertexShaderSource =
            @"#version 330 core
            layout (location = 0) in vec3 aPos;
            layout (location = 1) in vec2 aTexCoord;

            uniform mat4 view;
            uniform mat4 projection;

            out float height;
            out vec2 TexCoord;

            void main()
            {
                gl_Position = projection * view * vec4(aPos, 1.0);
                height = aPos.z;
                TexCoord = aTexCoord;
            }";

        internal static readonly string FragmentShaderSource =
            @"#version 330 core
            in float height;
            in vec2 TexCoord;
            out vec4 FragColor;

            uniform float zMin;
            uniform float zMax;
            uniform int colorMode;
            uniform sampler2D intensityTexture;

            vec3 getColorByHeight(float h)
            {
                float n = clamp((h - zMin) / (zMax - zMin), 0.0, 1.0);
                if (n < 0.2) return mix(vec3(0,0,1), vec3(0,1,1), n * 5.0);
                if (n < 0.4) return mix(vec3(0,1,1), vec3(0,1,0), (n-0.2)*5.0);
                if (n < 0.6) return mix(vec3(0,1,0), vec3(1,1,0), (n-0.4)*5.0);
                if (n < 0.8) return mix(vec3(1,1,0), vec3(1,0,0), (n-0.6)*5.0);
                return mix(vec3(1,0,0), vec3(1,0,1), (n-0.8)*5.0);
            }

            void main()
            {
                if (isinf(height)) discard;
                float intensity = texture(intensityTexture, TexCoord).r;
                if (colorMode == 0) {
                    FragColor = vec4(getColorByHeight(height), 1.0);
                } else if (colorMode == 1) {
                    FragColor = vec4(vec3(intensity), 1.0);
                } else {
                    FragColor = vec4(mix(vec3(intensity), getColorByHeight(height), 0.5), 1.0);
                }
            }";
        #endregion

        public CxMeshAdvancedItem(CxMesh mesh,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            Mesh = mesh;
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;

            BoundingBox = CxExtension.CalculateBoundingBox(mesh?.Vertexs);
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
        }

        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || Mesh == null || Mesh.Vertexs == null || Mesh.Vertexs.Length == 0)
                return null;

            var vertices = new float[Mesh.Vertexs.Length * 3];
            for (int i = 0; i < Mesh.Vertexs.Length; i++)
            {
                vertices[i * 3]     = Mesh.Vertexs[i].X;
                vertices[i * 3 + 1] = Mesh.Vertexs[i].Y;
                vertices[i * 3 + 2] = Mesh.Vertexs[i].Z;
            }

            var uvCoords = new float[Mesh.UVs.Length * 2];
            for (int i = 0; i < Mesh.UVs.Length; i++)
            {
                uvCoords[i * 2]     = Mesh.UVs[i].X;
                uvCoords[i * 2 + 1] = Mesh.UVs[i].Y;
            }

            byte[] textureBytes = GenerateIntensityTextureData();

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                UVCoords    = uvCoords,
                Indices     = Mesh.Indices,
                VertexCount = Mesh.Vertexs.Length,
                IndexCount  = Mesh.Indices?.Length ?? 0,
                UseVAO      = true,
                ShaderSource = new ShaderSource
                {
                    VertexSource   = VertexShaderSource,
                    FragmentSource = FragmentShaderSource,
                },
                TextureData = new TextureData
                {
                    Width  = Mesh.TextureWidth,
                    Height = Mesh.TextureHeight,
                    Data   = textureBytes,
                },
                Uniforms = new Dictionary<string, object>
                {
                    ["zMin"]      = ZMin,
                    ["zMax"]      = ZMax,
                    ["colorMode"] = (int)_surfaceColorMode,
                },
            };

            return _cachedRenderData;
        }

        public void Draw(OpenGL gl, GLResourceHandle handle)
        {
            if (!handle.IsValid || IsDisposed) return;
            var data = _cachedRenderData;
            if (data == null) return;

            gl.UseProgram(handle.ShaderProgram);
            gl.BindVertexArray(handle.VaoId);

            float[] proj = new float[16];
            float[] view = new float[16];
            gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, proj);
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, view);
            gl.UniformMatrix4(gl.GetUniformLocation(handle.ShaderProgram, "view"),       1, false, view);
            gl.UniformMatrix4(gl.GetUniformLocation(handle.ShaderProgram, "projection"), 1, false, proj);

            foreach (var kv in data.Uniforms)
            {
                int loc = gl.GetUniformLocation(handle.ShaderProgram, kv.Key);
                if (kv.Value is float f) gl.Uniform1(loc, f);
                else if (kv.Value is int i) gl.Uniform1(loc, i);
            }

            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, handle.TextureId);
            gl.Uniform1(gl.GetUniformLocation(handle.ShaderProgram, "intensityTexture"), 0);

            if (_surfaceMode == SurfaceMode.PointCloud)
                gl.DrawArrays(OpenGL.GL_POINTS, 0, data.VertexCount);
            else if (_surfaceMode == SurfaceMode.Mesh)
                gl.DrawElements(OpenGL.GL_TRIANGLES, data.IndexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);
        }

        public void SetGlobalZRange(float zMin, float zMax)
        {
            if (_surfaceColorMode == SurfaceColorMode.Intensity) return;
            if (Math.Abs(ZMin - zMin) < 1e-6f && Math.Abs(ZMax - zMax) < 1e-6f) return;

            ZMin = zMin;
            ZMax = zMax;
            if (_cachedRenderData?.Uniforms != null)
            {
                _cachedRenderData.Uniforms["zMin"] = zMin;
                _cachedRenderData.Uniforms["zMax"] = zMax;
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            Mesh = null;
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
        }

        private byte[] GenerateIntensityTextureData()
        {
            int w = Mesh.TextureWidth;
            int h = Mesh.TextureHeight;
            if (w <= 0 || h <= 0) return new byte[4] { 255, 255, 255, 255 };

            var data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int si = y * w + x;
                    byte v = 255;
                    if (Mesh.Intensity != null && Mesh.Intensity.Length > si)
                        v = Mesh.Intensity[si];
                    int di = si * 4;
                    data[di] = data[di + 1] = data[di + 2] = data[di + 3] = v;
                }
            }

            return data;
        }
    }
}
