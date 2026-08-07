using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// High-performance mesh renderer using a VAO + GLSL shader pipeline.
    /// Colour is computed per-fragment in the GPU using the <c>zMin</c>/<c>zMax</c> uniforms,
    /// so switching colour mode costs only a uniform update — no VBO rebuild required.
    /// </summary>
    public class CxMeshAdvancedItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxMesh Mesh { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public CxBox3D? BoundingBox { get; private set; }

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
                    ZMin = _diffMin; ZMax = _diffMax;
                }
                else if (old == SurfaceColorMode.Diff)
                {
                    ZMin = _trueZMin; ZMax = _trueZMax;
                }
                // 其它切换不动 ZMin/ZMax

                // ③ 决定是否 invalidate 缓存
                if (wantDiff)
                {
                    _cachedRenderData = null;
                    OnRenderDataChanged?.Invoke();
                }
                else if (_cachedRenderData?.Uniforms != null)
                {
                    _cachedRenderData.Uniforms["colorMode"] = EffectiveColorMode;
                    _cachedRenderData.Uniforms["zMin"] = ZMin;
                    _cachedRenderData.Uniforms["zMax"] = ZMax;
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
        private float _trueZMin, _trueZMax;   // BoundingBox Z 范围
        private float _diffMin, _diffMax;     // Mesh.Diff 范围（null → 0,0）

        #region Shader 源码（与 CxSurfaceAdvancedItem 内容一致，为解耦独立维护）
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

            uniform float zMin;
            uniform float zMax;
            uniform int colorMode;
            uniform int surfaceMode;
            uniform sampler2D intensityTexture;

            vec3 getColorByHeight(float h)
            {
                float span = max(zMax - zMin, 1e-6);
                float n = clamp((h - zMin) / span, 0.0, 1.0);
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

        public CxMeshAdvancedItem(CxMesh mesh,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            Mesh = mesh;
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;

            BoundingBox = CxExtension.CalculateBoundingBox(mesh?.Vertices);

            // 预计算差分范围（UpdateWorldZRange 在 Diff 模式时引用 _diff 范围，须先算）
            ComputeDiffRange(mesh?.Diff);
            UpdateWorldZRange();
        }

        /// <summary>
        /// Recomputes the world-space Z range (after applying the model matrix) used for
        /// height-based colour mapping. <see cref="_trueZMin"/>/<see cref="_trueZMax"/> always
        /// track the world range (used when leaving Diff mode); in Diff mode the active
        /// <see cref="ZMin"/>/<see cref="ZMax"/> track the diff range instead.
        /// </summary>
        private void UpdateWorldZRange()
        {
            CxExtension.ComputeWorldZRange(BoundingBox, _modelMatrix, out float zMin, out float zMax);
            _trueZMin = zMin;
            _trueZMax = zMax;

            if (_surfaceColorMode == SurfaceColorMode.Diff)
            {
                ZMin = _diffMin;
                ZMax = _diffMax;
            }
            else
            {
                ZMin = zMin;
                ZMax = zMax;
            }

            if (_cachedRenderData?.Uniforms != null)
            {
                _cachedRenderData.Uniforms["zMin"] = ZMin;
                _cachedRenderData.Uniforms["zMax"] = ZMax;
            }
        }

        private void ComputeDiffRange(float[] diffData)
        {
            if (diffData == null || diffData.Length == 0)
            {
                _diffMin = 0f; _diffMax = 0f;
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
            _diffMin = dmin; _diffMax = dmax;
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
                VertexCount = Mesh.Vertices.Length,
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
                    ["colorMode"] = EffectiveColorMode,
                    ["surfaceMode"] = _surfaceMode == SurfaceMode.Mesh ? 1 : 0,
                },
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
            if (_surfaceColorMode == SurfaceColorMode.Lit) return;
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
