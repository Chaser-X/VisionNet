using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// High-performance point cloud renderer using a VAO + GLSL shader pipeline with intensity texture.
    /// Supports automatic down-sampling when the point count exceeds <c>maxPointCount</c>.
    /// Colour mode changes update shader uniforms only — no VBO rebuild needed.
    /// </summary>
    public class CxPointCloudAdvancedItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxPointCloud PointCloud { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ColorMin { get; set; }
        public float ColorMax { get; set; }
        public float BaseZMin => _baseZMin;
        public float BaseZMax => _baseZMax;
        public float BaseDiffMin => _baseDiffMin;
        public float BaseDiffMax => _baseDiffMax;
        public CxBox3D? BoundingBox { get; private set; }
        public int MaxPointCount { get; set; } = int.MaxValue;

        /// <summary>
        /// Per-frame diff range propagation from <see cref="CxDisplay"/>. Only applies in
        /// Diff mode; lightweight uniform update (no cache rebuild, no auto-range reset).
        /// </summary>
        public void SetGlobalDiffRange(float min, float max)
        {
            if (_surfaceColorMode != SurfaceColorMode.Diff) return;
            if (Math.Abs(ColorMin - min) < 1e-6f && Math.Abs(ColorMax - max) < 1e-6f) return;

            ColorMin = min;
            ColorMax = max;
            if (_cachedRenderData?.Uniforms != null)
            {
                _cachedRenderData.Uniforms["colorMin"] = min;
                _cachedRenderData.Uniforms["colorMax"] = max;
            }
        }

        /// <summary>
        /// Model matrix applied to this item's geometry before the camera transform.
        /// Represents the item's pose (position and orientation) in world space.
        /// Defaults to identity (no transform).
        /// Setting a new pose recomputes the world-space Z range used for colour mapping.
        /// </summary>
        private CxMatrix4X4 _modelMatrix = CxMatrix4X4.Identity();
        public CxMatrix4X4 ModelMatrix
        {
            get => _modelMatrix;
            set { _modelMatrix = value; UpdateWorldZRange(); }
        }

        private int _samplingFactorX = 1;
        private int _samplingFactorY = 1;

        private SurfaceMode _surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set
            {
                _surfaceMode = value;
                // Lit 仅在 Mesh 模式有效；模式切换时刷新 shader 的 colorMode / surfaceMode
                if (_cachedRenderData?.Uniforms != null)
                {
                    _cachedRenderData.Uniforms["colorMode"] = EffectiveColorMode;
                    _cachedRenderData.Uniforms["surfaceMode"] = value == SurfaceMode.Mesh ? 1 : 0;
                }
            }
        }

        private SurfaceColorMode _surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get => _surfaceColorMode;
            set
            {
                if (_surfaceColorMode == value) return;
                var old = _surfaceColorMode;

                // ① 数据缺失回退（直接改 backing field，不递归事件）
                bool wantDiff = value == SurfaceColorMode.Diff;
                int gridCount = PointCloud?.Width * PointCloud?.Length ?? 0;
                var pcDiff = PointCloud?.Diff;
                bool hasDiffData = pcDiff != null && pcDiff.Length >= gridCount && gridCount > 0;
                if (wantDiff && !hasDiffData)
                {
                    _surfaceColorMode = SurfaceColorMode.Color;
                    value = SurfaceColorMode.Color;
                    wantDiff = false;
                }
                else
                {
                    _surfaceColorMode = value;
                }

                // ② 重新设定活跃映射范围
                if (value == SurfaceColorMode.Diff)
                {
                    ColorMin = _baseDiffMin; ColorMax = _baseDiffMax;
                }
                else if (old == SurfaceColorMode.Diff)
                {
                    ColorMin = _baseZMin; ColorMax = _baseZMax;
                }
                // 其它切换不动 ColorMin/ColorMax

                // ③ 决定是否 invalidate 缓存
                if (wantDiff)
                {
                    _cachedRenderData = null;
                    OnRenderDataChanged?.Invoke();
                }
                else if (_cachedRenderData?.Uniforms != null)
                {
                    _cachedRenderData.Uniforms["colorMode"] = EffectiveColorMode;
                    _cachedRenderData.Uniforms["colorMin"] = ColorMin;
                    _cachedRenderData.Uniforms["colorMax"] = ColorMax;
                }
            }
        }

        /// <summary>
        /// Effective colour-mode value sent to the shader. <see cref="SurfaceColorMode.Lit"/>
        /// is honoured only in <see cref="SurfaceMode.Mesh"/>; in point-cloud mode it falls back
        /// to <see cref="SurfaceColorMode.Color"/> (the requested mode is preserved so switching
        /// back to Mesh re-enables lighting automatically).
        /// </summary>
        private int EffectiveColorMode =>
            _surfaceColorMode == SurfaceColorMode.Lit && _surfaceMode != SurfaceMode.Mesh
                ? (int)SurfaceColorMode.Color
                : (int)_surfaceColorMode;

        private RenderData _cachedRenderData;

        // 构造时一次性预计算的范围缓存（供 SurfaceColorMode 切换时快速赋值）
        private float _baseZMin, _baseZMax;   // BoundingBox Z 范围
        private float _baseDiffMin, _baseDiffMax;     // DiffValues 范围（null → 0,0）

        #region Shader 源码
        internal static readonly string VertexShaderSource =
            @"#version 330 core
            layout (location = 0) in vec3 aPos;
            layout (location = 1) in vec2 aTexCoord;
            layout (location = 2) in float aDiff;

            uniform mat4 view;
            uniform mat4 projection;
            uniform mat4 model;

            out float height;
            out vec2 TexCoord;
            out float diffValue;
            out vec3 viewPos;
            out vec3 lightDirView;

            void main()
            {
                vec4 worldPos = model * vec4(aPos, 1.0);
                gl_Position = projection * view * worldPos;
                height = worldPos.z;
                TexCoord = aTexCoord;
                diffValue = aDiff;
                viewPos = (view * worldPos).xyz;
                lightDirView = normalize(vec3(0.3, 0.4, 0.85));
            }";

        internal static readonly string FragmentShaderSource =
            @"#version 330 core
            in float height;
            in vec2 TexCoord;
            in float diffValue;
            in vec3 viewPos;
            in vec3 lightDirView;
            out vec4 FragColor;

            uniform float colorMin;
            uniform float colorMax;
            uniform int colorMode;
            uniform int surfaceMode;
            uniform sampler2D intensityTexture;

            vec3 getColorByHeight(float h)
            {
                float span = max(colorMax - colorMin, 1e-6);
                float n = clamp((h - colorMin) / span, 0.0, 1.0);
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

                vec3 lightFactor = vec3(1.0);
                if (surfaceMode == 1)
                {
                    vec3 N = normalize(cross(dFdx(viewPos), dFdy(viewPos)));
                    vec3 V = normalize(-viewPos);
                    if (dot(N, V) < 0.0) N = -N;
                    if (!(dot(N, N) > 1e-6)) N = V;
                    vec3 L = normalize(lightDirView);
                    vec3 H = normalize(V + L);
                    float diff = max(dot(N, L), 0.0);
                    float spec = pow(max(dot(N, H), 0.0), 24.0) * 0.4;
                    lightFactor = vec3((0.35f + 0.8f * diff) + 0.4f * spec);
                }
                if (colorMode == 3) {
                    if (isinf(diffValue)) discard;
                    FragColor = vec4(getColorByHeight(diffValue) * lightFactor, 1.0);
                } else if (colorMode == 0) {
                    FragColor = vec4(getColorByHeight(height) * lightFactor, 1.0);
                } else if (colorMode == 1) {
                    FragColor = vec4(vec3(intensity) * lightFactor, 1.0);
                } else if (colorMode == 4) {
                    FragColor = vec4(vec3(0.85) * lightFactor, 1.0);
                } else {
                    FragColor = vec4(mix(vec3(intensity), getColorByHeight(height), 0.5) * lightFactor, 1.0);
                }
            }";
        #endregion

        public CxPointCloudAdvancedItem(CxPointCloud pointCloud,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color,
            int maxPointCount = int.MaxValue)
        {
            PointCloud = pointCloud;
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;
            MaxPointCount = maxPointCount;

            CalculateSamplingFactors();

            BoundingBox = pointCloud?.Data != null && pointCloud.Data.Length > 0
                ? CxExtension.CalculateBoundingBox(pointCloud.ToPoints())
                : null;

            ComputeDiffRange(pointCloud?.Diff);
            UpdateWorldZRange();
        }

        /// <summary>
        /// Recomputes the world-space Z range (after applying the model matrix) used for
        /// height-based colour mapping. <see cref="_baseZMin"/>/<see cref="_baseZMax"/> always
        /// track the world range (used when leaving Diff mode); in Diff mode the active
        /// <see cref="ColorMin"/>/<see cref="ColorMax"/> track the diff range instead.
        /// </summary>
        private void UpdateWorldZRange()
        {
            CxExtension.ComputeWorldZRange(BoundingBox, _modelMatrix, out float zMin, out float zMax);
            _baseZMin = zMin;
            _baseZMax = zMax;

            if (_surfaceColorMode == SurfaceColorMode.Diff)
            {
                ColorMin = _baseDiffMin;
                ColorMax = _baseDiffMax;
            }
            else
            {
                ColorMin = zMin;
                ColorMax = zMax;
            }

            if (_cachedRenderData?.Uniforms != null)
            {
                _cachedRenderData.Uniforms["colorMin"] = ColorMin;
                _cachedRenderData.Uniforms["colorMax"] = ColorMax;
            }
        }

        private void ComputeDiffRange(float[] diffData)
        {
            if (diffData == null || diffData.Length == 0)
            {
                _baseDiffMin = 0f; _baseDiffMax = 0f;
                return;
            }
            float dmin = float.MaxValue, dmax = float.MinValue;
            foreach (var d in diffData)
            {
                if (float.IsInfinity(d) || float.IsNaN(d)) continue;
                if (d < dmin) dmin = d;
                if (d > dmax) dmax = d;
            }
            if (dmax - dmin < 1e-6f) dmax = dmin + 1e-6f;
            _baseDiffMin = dmin; _baseDiffMax = dmax;
        }

        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || PointCloud == null || PointCloud.Data == null || PointCloud.Data.Length == 0)
                return null;

            CalculateSamplingFactors();

            int sampledWidth  = (PointCloud.Width  + _samplingFactorX - 1) / _samplingFactorX;
            int sampledLength = (PointCloud.Length + _samplingFactorY - 1) / _samplingFactorY;
            int totalVertices = sampledWidth * sampledLength;

            var vertices = new float[totalVertices * 3];
            var uvCoords = new float[totalVertices * 2];

            // Diff 模式下按同一采样步长抽样 PointCloud.Diff（W×L 网格 → 采样顶点 1:1）
            var srcDiff = PointCloud.Diff;
            bool useDiff = _surfaceColorMode == SurfaceColorMode.Diff
                && srcDiff != null && srcDiff.Length >= PointCloud.Width * PointCloud.Length;
            float[] diffOut = useDiff ? new float[totalVertices] : null;

            int vi = 0;
            for (int y = 0; y < PointCloud.Length && vi < totalVertices; y += _samplingFactorY)
            {
                for (int x = 0; x < PointCloud.Width && vi < totalVertices; x += _samplingFactorX)
                {
                    int si = y * PointCloud.Width + x;

                    bool invalid = PointCloud.Data[si * 3]     == -32768
                                || PointCloud.Data[si * 3 + 1] == -32768
                                || PointCloud.Data[si * 3 + 2] == -32768;
                    float xPos = PointCloud.Data[si * 3]     == -32768
                        ? float.NegativeInfinity
                        : PointCloud.XOffset + PointCloud.Data[si * 3]     * PointCloud.XScale;
                    float yPos = PointCloud.Data[si * 3 + 1] == -32768
                        ? float.NegativeInfinity
                        : PointCloud.YOffset + PointCloud.Data[si * 3 + 1] * PointCloud.YScale;
                    float zPos = PointCloud.Data[si * 3 + 2] == -32768
                        ? float.NegativeInfinity
                        : PointCloud.ZOffset + PointCloud.Data[si * 3 + 2] * PointCloud.ZScale;

                    vertices[vi * 3]     = xPos;
                    vertices[vi * 3 + 1] = yPos;
                    vertices[vi * 3 + 2] = zPos;
                    uvCoords[vi * 2]     = (float)x / Math.Max(PointCloud.Width  - 1, 1);
                    uvCoords[vi * 2 + 1] = (float)y / Math.Max(PointCloud.Length - 1, 1);

                    if (diffOut != null)
                        diffOut[vi] = invalid ? float.NegativeInfinity : srcDiff[si];

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
                    Width  = PointCloud.Width,
                    Height = PointCloud.Length,
                    Data   = textureBytes,
                },
                Uniforms = new Dictionary<string, object>
                {
                    ["colorMin"]      = ColorMin,
                    ["colorMax"]      = ColorMax,
                    ["colorMode"] = EffectiveColorMode,
                    ["surfaceMode"] = _surfaceMode == SurfaceMode.Mesh ? 1 : 0,
                },
            };

            if (diffOut != null)
                _cachedRenderData.DiffValues = diffOut;

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
            var modelData = (ModelMatrix ?? CxMatrix4X4.Identity()).Data;
            gl.UniformMatrix4(gl.GetUniformLocation(handle.ShaderProgram, "model"), 1, true, modelData);

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

        public void SetGlobalZRange(float zMin, float zMax)
        {
            if (_surfaceColorMode == SurfaceColorMode.Intensity) return;
            if (_surfaceColorMode == SurfaceColorMode.Lit) return;
            if (_surfaceColorMode == SurfaceColorMode.Diff) return;
            if (Math.Abs(ColorMin - zMin) < 1e-6f && Math.Abs(ColorMax - zMax) < 1e-6f) return;

            ColorMin = zMin;
            ColorMax = zMax;
            if (_cachedRenderData?.Uniforms != null)
            {
                _cachedRenderData.Uniforms["colorMin"] = zMin;
                _cachedRenderData.Uniforms["colorMax"] = zMax;
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            //PointCloud?.Dispose();
            PointCloud = null;
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
        }

        private byte[] GenerateIntensityTextureData()
        {
            int w = PointCloud.Width;
            int h = PointCloud.Length;
            var data = new byte[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int si = y * w + x;
                    byte v = 255;
                    if (PointCloud.Intensity != null && PointCloud.Intensity.Length > si)
                        v = PointCloud.Intensity[si];
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
            if (PointCloud == null || PointCloud.Width <= 0 || PointCloud.Length <= 0) return;

            int total = PointCloud.Width * PointCloud.Length;
            if (total <= MaxPointCount)
            {
                _samplingFactorX = _samplingFactorY = 1;
                return;
            }

            double rate = Math.Sqrt((double)MaxPointCount / total);
            _samplingFactorX = Math.Max(1, (int)(1.0 / rate));
            _samplingFactorY = Math.Max(1, (int)(1.0 / rate));

            while ((PointCloud.Width / _samplingFactorX) * (PointCloud.Length / _samplingFactorY) > MaxPointCount)
            {
                if (_samplingFactorX <= _samplingFactorY) _samplingFactorX++;
                else _samplingFactorY++;
            }

            int sw = (PointCloud.Width  + _samplingFactorX - 1) / _samplingFactorX;
            int sl = (PointCloud.Length + _samplingFactorY - 1) / _samplingFactorY;
            if (sw * sl > MaxPointCount)
            {
                double ratio = Math.Sqrt((double)(sw * sl) / MaxPointCount);
                _samplingFactorX = Math.Max(1, (int)(_samplingFactorX * ratio));
                _samplingFactorY = Math.Max(1, (int)(_samplingFactorY * ratio));
            }
        }
    }
}
