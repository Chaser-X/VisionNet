using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxSurfaceAdvancedItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        // colorMode 通过 Uniform 传入 Shader，不需要重建 GL 资源，不触发此事件
        public event Action OnRenderDataChanged;

        public CxSurface Surface { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }
        public int MaxPointCount { get; set; } = int.MaxValue;

        private int _samplingFactorX = 1;
        private int _samplingFactorY = 1;

        private SurfaceMode _surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set => _surfaceMode = value; // 只影响 Draw() 选择 DrawArrays/DrawElements
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
                    // colorMode 以 Uniform 传入 Shader，只需更新缓存中的 Uniform 值
                    if (_cachedRenderData?.Uniforms != null)
                        _cachedRenderData.Uniforms["colorMode"] = (int)value;
                }
            }
        }

        private RenderData _cachedRenderData;

        #region Shader 源码（静态，与 MeshAdvancedItem 共用）
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

        public CxSurfaceAdvancedItem(CxSurface surface,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color,
            int maxPointCount = int.MaxValue)
        {
            Surface = surface;
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;
            MaxPointCount = maxPointCount;

            CalculateSamplingFactors();

            BoundingBox = CalculateBoundingBox();
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
        }

        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || Surface == null || Surface.Data == null || Surface.Data.Length == 0)
                return null;

            CalculateSamplingFactors();

            int sampledWidth  = (Surface.Width  + _samplingFactorX - 1) / _samplingFactorX;
            int sampledLength = (Surface.Length + _samplingFactorY - 1) / _samplingFactorY;
            int totalVertices = sampledWidth * sampledLength;

            var vertices = new float[totalVertices * 3];
            var uvCoords = new float[totalVertices * 2];

            int vi = 0;
            for (int y = 0; y < Surface.Length && vi < totalVertices; y += _samplingFactorY)
            {
                for (int x = 0; x < Surface.Width && vi < totalVertices; x += _samplingFactorX)
                {
                    int si = y * Surface.Width + x;
                    float xPos, yPos, zPos;

                    if (Surface.Type == SurfaceType.Surface)
                    {
                        xPos = Surface.XOffset + x * Surface.XScale;
                        yPos = Surface.YOffset + y * Surface.YScale;
                        zPos = Surface.Data[si] == -32768
                            ? float.NegativeInfinity
                            : Surface.ZOffset + Surface.Data[si] * Surface.ZScale;
                    }
                    else
                    {
                        xPos = Surface.Data[si * 3]     == -32768 ? float.NegativeInfinity : Surface.XOffset + Surface.Data[si * 3]     * Surface.XScale;
                        yPos = Surface.Data[si * 3 + 1] == -32768 ? float.NegativeInfinity : Surface.YOffset + Surface.Data[si * 3 + 1] * Surface.YScale;
                        zPos = Surface.Data[si * 3 + 2] == -32768 ? float.NegativeInfinity : Surface.ZOffset + Surface.Data[si * 3 + 2] * Surface.ZScale;
                    }

                    vertices[vi * 3]     = xPos;
                    vertices[vi * 3 + 1] = yPos;
                    vertices[vi * 3 + 2] = zPos;
                    uvCoords[vi * 2]     = (float)x / Math.Max(Surface.Width  - 1, 1);
                    uvCoords[vi * 2 + 1] = (float)y / Math.Max(Surface.Length - 1, 1);
                    vi++;
                }
            }

            uint[] indices = GenerateMeshIndices(sampledWidth, sampledLength);
            byte[] textureBytes = GenerateIntensityTextureData();

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                UVCoords    = uvCoords,
                Indices     = indices,
                VertexCount = totalVertices,
                IndexCount  = indices.Length,
                UseVAO      = true,
                ShaderSource = new ShaderSource
                {
                    VertexSource   = VertexShaderSource,
                    FragmentSource = FragmentShaderSource,
                },
                TextureData = new TextureData
                {
                    Width  = Surface.Width,
                    Height = Surface.Length,
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
            else
                gl.DrawElements(OpenGL.GL_TRIANGLES, data.IndexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            Surface?.Dispose();
            Surface = null;
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
        }

        private byte[] GenerateIntensityTextureData()
        {
            int w = Surface.Width;
            int h = Surface.Length;
            var data = new byte[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int si = y * w + x;
                    byte v = 255;
                    if (Surface.Intensity != null && Surface.Intensity.Length > si)
                        v = Surface.Intensity[si];
                    int di = si * 4;
                    data[di] = data[di + 1] = data[di + 2] = data[di + 3] = v;
                }
            }

            return data;
        }

        private uint[] GenerateMeshIndices(int width, int height)
        {
            if (width <= 1 || height <= 1) return new uint[0];

            int total = (width - 1) * (height - 1) * 6;
            var indices = new uint[total];
            int idx = 0;

            for (int y = 0; y < height - 1; y++)
            {
                uint rowStart  = (uint)(y * width);
                uint nextStart = (uint)((y + 1) * width);
                for (uint x = 0; x < width - 1; x++)
                {
                    uint tl = rowStart  + x;
                    uint tr = tl + 1;
                    uint bl = nextStart + x;
                    uint br = bl + 1;
                    indices[idx++] = tl;
                    indices[idx++] = bl;
                    indices[idx++] = tr;
                    indices[idx++] = tr;
                    indices[idx++] = bl;
                    indices[idx++] = br;
                }
            }

            return indices;
        }

        private void CalculateSamplingFactors()
        {
            if (Surface == null || Surface.Width <= 0 || Surface.Length <= 0) return;

            int total = Surface.Width * Surface.Length;
            if (total <= MaxPointCount)
            {
                _samplingFactorX = _samplingFactorY = 1;
                return;
            }

            double rate = Math.Sqrt((double)MaxPointCount / total);
            _samplingFactorX = Math.Max(1, (int)(1.0 / rate));
            _samplingFactorY = Math.Max(1, (int)(1.0 / rate));

            while ((Surface.Width / _samplingFactorX) * (Surface.Length / _samplingFactorY) > MaxPointCount)
            {
                if (_samplingFactorX <= _samplingFactorY) _samplingFactorX++;
                else _samplingFactorY++;
            }

            int sw = (Surface.Width  + _samplingFactorX - 1) / _samplingFactorX;
            int sl = (Surface.Length + _samplingFactorY - 1) / _samplingFactorY;
            if (sw * sl > MaxPointCount)
            {
                double ratio = Math.Sqrt((double)(sw * sl) / MaxPointCount);
                _samplingFactorX = Math.Max(1, (int)(_samplingFactorX * ratio));
                _samplingFactorY = Math.Max(1, (int)(_samplingFactorY * ratio));
            }
        }

        private Box3D? CalculateBoundingBox()
        {
            if (Surface == null || Surface.Data == null || Surface.Data.Length == 0)
                return null;

            var points = Surface.ToPoints();
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var p in points)
            {
                if (float.IsInfinity(p.X) || float.IsInfinity(p.Y) || float.IsInfinity(p.Z))
                    continue;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            if (minX == float.MaxValue) return null;
            return new Box3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }
    }
}
