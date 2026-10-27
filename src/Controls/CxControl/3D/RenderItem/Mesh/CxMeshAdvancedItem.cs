using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Shader-backed mesh renderer using VAO/VBO resources and GLSL.
    /// Colour is computed per-fragment in the GPU using the <c>colorMin</c>/<c>colorMax</c> uniforms,
    /// so switching colour mode costs only a uniform update — no VBO rebuild required.
    /// </summary>
    public class CxMeshAdvancedItem : ICxObjRenderItem, ICxGroupMember
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxMesh Mesh { get; private set; }
        public bool IsDisposed { get; private set; } = false;

        /// <inheritdoc/>
        public bool SupportsGroupTransform => true;
        public float ColorMin { get; set; }
        public float ColorMax { get; set; }
        public float BaseZMin => _baseZMin;
        public float BaseZMax => _baseZMax;
        public float BaseDiffMin => _baseDiffMin;
        public float BaseDiffMax => _baseDiffMax;
        public CxBox3D? BoundingBox { get; private set; }

        /// <summary>
        /// Per-frame diff range propagation from <see cref="CxDisplay"/>. Only applies in
        /// Diff mode; lightweight uniform update (no cache rebuild, no auto-range reset).
        /// </summary>
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

        private SurfaceMode _surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set
            {
                _surfaceMode = value;
                // Lit 仅在 Mesh 模式有效；模式切换时刷新 shader 的 colorMode
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
                bool hasDiffData = Mesh?.Diff != null && Mesh.Diff.Length >= Mesh.Vertices.Length;
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
            layout (location = 3) in float aIntensity;

            uniform mat4 view;
            uniform mat4 projection;
            uniform mat4 model;

            out float height;
            out vec2 TexCoord;
            out float diffValue;
            out float vertexIntensity;
            out vec3 viewPos;
            out vec3 lightDirView;

            void main()
            {
                vec4 worldPos = model * vec4(aPos, 1.0);
                gl_Position = projection * view * worldPos;
                height = worldPos.z;
                TexCoord = aTexCoord;
                diffValue = aDiff;
                vertexIntensity = aIntensity;
                viewPos = (view * worldPos).xyz;
                lightDirView = normalize(vec3(0.3, 0.4, 0.85));
            }";

        internal static readonly string FragmentShaderSource =
            @"#version 330 core
            in float height;
            in vec2 TexCoord;
            in float diffValue;
            in float vertexIntensity;
            in vec3 viewPos;
            in vec3 lightDirView;
            out vec4 FragColor;

            uniform float colorMin;
            uniform float colorMax;
            uniform int colorMode;
            uniform int surfaceMode;
            uniform sampler2D intensityTexture;
            uniform int useIntensityAttribute;

            vec3 getColorByHeight(float h)
            {
                float span = max(colorMax - colorMin, 1e-6);
                float n = clamp((h - colorMin) / span, 0.0, 1.0);
                if (n < 1.0/7.0) return mix(vec3(0,0,0.5), vec3(0,0,1), n * 7.0);
                if (n < 2.0/7.0) return mix(vec3(0,0,1),   vec3(0,1,1), (n - 1.0/7.0) * 7.0);
                if (n < 3.0/7.0) return mix(vec3(0,1,1),   vec3(0,1,0), (n - 2.0/7.0) * 7.0);
                if (n < 4.0/7.0) return mix(vec3(0,1,0),   vec3(1,1,0), (n - 3.0/7.0) * 7.0);
                if (n < 5.0/7.0) return mix(vec3(1,1,0),   vec3(1,0,0), (n - 4.0/7.0) * 7.0);
                if (n < 6.0/7.0) return mix(vec3(1,0,0),   vec3(1,0,1), (n - 5.0/7.0) * 7.0);
                return mix(vec3(1,0,1), vec3(1,1,1), (n - 6.0/7.0) * 7.0);
            }

            void main()
            {
                if (isinf(height)) discard;
                float intensity = useIntensityAttribute == 1
                    ? vertexIntensity : texture(intensityTexture, TexCoord).r;
                
                vec3 lightFactor = vec3(1.0);
                if(surfaceMode == 1)
                {
                    vec3 N = normalize(cross(dFdx(viewPos), dFdy(viewPos)));
                    vec3 V = normalize(-viewPos);
                    if (dot(N, V) < 0.0) N = -N;
                    if (!(dot(N, N) > 1e-6)) N = V;
                    vec3 L = normalize(lightDirView);
                    vec3 H = normalize(V + L);
                    float diff = max(dot(N, L), 0.0);
                    float spec = pow(max(dot(N, H), 0.0), 24.0) * 0.4;
                    lightFactor = vec3((0.35 + 0.8 * diff) + 0.4 * spec);
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

        public CxMeshAdvancedItem(CxMesh mesh,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            Mesh = mesh;
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;

            BoundingBox = CxExtension.CalculateBoundingBox(mesh?.Vertices);

            // 预计算差分范围（UpdateWorldZRange 在 Diff 模式时引用 _diff 范围，须先算）。
            ComputeDiffRange(mesh?.Diff);
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

            if (IsDisposed || Mesh == null || Mesh.Vertices == null || Mesh.Vertices.Length == 0)
                return null;

            var vertices = new float[Mesh.Vertices.Length * 3];
            for (int i = 0; i < Mesh.Vertices.Length; i++)
            {
                vertices[i * 3]     = Mesh.Vertices[i].X;
                vertices[i * 3 + 1] = Mesh.Vertices[i].Y;
                vertices[i * 3 + 2] = Mesh.Vertices[i].Z;
            }

            int vertexCount = Mesh.Vertices.Length;
            var meshUvs = Mesh.UVs ?? new CxPoint2D[0];
            bool hasTextureUvs = Mesh.TextureWidth > 0 && Mesh.TextureHeight > 0
                && meshUvs.Length >= vertexCount;
            bool hasVertexIntensity = !hasTextureUvs && Mesh.Intensity != null
                && Mesh.Intensity.Length >= vertexCount;
            int textureWidth = hasTextureUvs ? Mesh.TextureWidth : 1;
            int textureHeight = hasTextureUvs ? Mesh.TextureHeight : 1;
            var uvCoords = new float[vertexCount * 2];
            float[] intensityValues = hasVertexIntensity ? new float[vertexCount] : null;
            for (int i = 0; i < vertexCount; i++)
            {
                if (hasTextureUvs)
                {
                    uvCoords[i * 2] = meshUvs[i].X;
                    uvCoords[i * 2 + 1] = meshUvs[i].Y;
                }
                else if (hasVertexIntensity)
                {
                    uvCoords[i * 2] = 0.5f;
                    uvCoords[i * 2 + 1] = 0.5f;
                    intensityValues[i] = Mesh.Intensity[i] / 255f;
                }
                else
                {
                    uvCoords[i * 2] = 0.5f;
                    uvCoords[i * 2 + 1] = 0.5f;
                }
            }

            byte[] textureBytes = hasTextureUvs
                ? GenerateIntensityTextureData(textureWidth, textureHeight)
                : new byte[] { 255, 255, 255, 255 };

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                UVCoords    = uvCoords,
                Indices     = Mesh.Indices,
                VertexCount = Mesh.Vertices.Length,
                IndexCount  = Mesh.Indices?.Length ?? 0,
                ShaderSource = new ShaderSource
                {
                    VertexSource   = VertexShaderSource,
                    FragmentSource = FragmentShaderSource,
                },
                TextureData = new TextureData
                {
                    Width  = textureWidth,
                    Height = textureHeight,
                    Data   = textureBytes,
                },
                Uniforms = new Dictionary<string, object>
                {
                    ["colorMin"]      = ColorMin,
                    ["colorMax"]      = ColorMax,
                    ["colorMode"] = EffectiveColorMode,
                    ["surfaceMode"] = _surfaceMode == SurfaceMode.Mesh ? 1 : 0,
                    ["useIntensityAttribute"] = hasVertexIntensity ? 1 : 0,
                },
                IntensityValues = intensityValues,
            };

            // 惰性填充 DiffValues VBO（只在 Diff 模式且数据充足时）
            if (_surfaceColorMode == SurfaceColorMode.Diff
                && Mesh?.Diff != null && Mesh.Diff.Length >= Mesh.Vertices.Length)
            {
                _cachedRenderData.DiffValues = new float[Mesh.Vertices.Length];
                Array.Copy(Mesh.Diff, _cachedRenderData.DiffValues, _cachedRenderData.DiffValues.Length);
            }

            return _cachedRenderData;
        }

        public void Draw(OpenGL gl, GLResourceHandle handle)
        {
            if (!handle.IsValid || IsDisposed) return;
            var data = _cachedRenderData;
            if (data == null) return;

            gl.UseProgram(handle.ShaderProgram);
            gl.BindVertexArray(handle.VaoId);

            ModernOverlayRenderer.For(gl).UploadSceneMatrices(gl, handle, ModelMatrix);

            foreach (var kv in data.Uniforms)
            {
                int loc = handle.GetUniformLocation(gl, kv.Key);
                if (kv.Value is float f) gl.Uniform1(loc, f);
                else if (kv.Value is int i) gl.Uniform1(loc, i);
            }

            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, handle.TextureId);
            gl.Uniform1(handle.GetUniformLocation(gl, "intensityTexture"), 0);

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
            if (_surfaceColorMode == SurfaceColorMode.Diff) return;
            if (_surfaceColorMode == SurfaceColorMode.Lit && _surfaceMode == SurfaceMode.Mesh) return;
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
            Mesh = null;
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
        }

        private byte[] GenerateIntensityTextureData(int width, int height)
        {
            if (width <= 0 || height <= 0) return new byte[4] { 255, 255, 255, 255 };

            var data = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int si = y * width + x;
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
