using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Base class for the shader-backed primary render items (structured surface, mesh and point
    /// cloud). Implements the shared part of <see cref="ICxObjRenderItem"/> and
    /// <see cref="ICxGroupMember"/>: pose handling, colour-mode and colour-range bookkeeping, the
    /// GLSL program pair, the uniform set and the draw/dispose skeleton.
    /// </summary>
    /// <remarks>
    /// Derived classes add the data-specific work (geometry upload, sampling, textures) and call
    /// <see cref="Initialize"/> once their data object has been assigned. Use
    /// <see cref="CxSurfaceItem"/>, <see cref="CxMeshItem"/> or <see cref="CxPointCloudItem"/>
    /// rather than deriving from this type directly.
    /// </remarks>
    public abstract class AbstractCxObjRenderItem : ICxObjRenderItem, ICxGroupMember
    {
        public event Action OnDisposed;
        // colorMode 通过 Uniform 传入 Shader，不需要重建 GL 资源，不触发此事件
        public event Action OnRenderDataChanged;

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
        /// Upload budget shared by the sampling-based items (structured surface / point cloud).
        /// The concrete items expose it as <c>MaxPointCount</c>.
        /// </summary>
        protected int MaxPointCountCore
        {
            get => _maxPointCount;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("MaxPointCount", value,
                        "MaxPointCount must be greater than zero.");
                _maxPointCount = value;
            }
        }
        private int _maxPointCount = int.MaxValue;

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
            set
            {
                _modelMatrix = value;
                _modelMatrixInverseDirty = true;
                UpdateWorldZRange();
            }
        }

        private SurfaceMode _surfaceMode;

        /// <summary>Gets the current surface mode (read-only accessor for derived render data).</summary>
        protected SurfaceMode CurrentSurfaceMode => _surfaceMode;

        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set
            {
                if (_surfaceMode == value) return;

                _surfaceMode = value;
                // 模式切换时刷新 shader 的 colorMode / surfaceMode；Lit 的逐片元光照只在
                // Mesh（surfaceMode == 1）下叠加。
                if (value == SurfaceMode.Mesh
                    && (_cachedRenderData == null || _cachedRenderData.Indices == null))
                {
                    // PointCloud render data deliberately omits the mesh index buffer. Rebuild
                    // lazily when the caller switches to Mesh; the existing GL handle is marked
                    // dirty by CxDisplay through this event.
                    _cachedRenderData = null;
                    OnRenderDataChanged?.Invoke();
                }
                else if (_cachedRenderData?.Uniforms != null)
                {
                    _cachedRenderData.Uniforms["colorMode"] = EffectiveColorMode;
                    _cachedRenderData.Uniforms["surfaceMode"] = value == SurfaceMode.Mesh ? 1 : 0;
                }
            }
        }

        private SurfaceColorMode _surfaceColorMode;

        /// <summary>Gets the current colour mode (read-only accessor for derived render data).</summary>
        protected SurfaceColorMode CurrentSurfaceColorMode => _surfaceColorMode;

        public SurfaceColorMode SurfaceColorMode
        {
            get => _surfaceColorMode;
            set
            {
                if (_surfaceColorMode == value) return;
                var old = _surfaceColorMode;

                // ① 数据缺失回退（直接改 backing field，不递归事件）
                bool wantDiff = value == SurfaceColorMode.Diff;
                bool hasDiffData = HasSufficientDiffData();
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
        /// Effective colour-mode value sent to the shader. Every mode is honoured in both
        /// <see cref="SurfaceMode.Mesh"/> and <see cref="SurfaceMode.PointCloud"/>; only the
        /// per-fragment lighting term (Mesh) differs, so no mode needs a fallback.
        /// </summary>
        protected int EffectiveColorMode => (int)_surfaceColorMode;

        private Color _solidColor = Color.FromArgb(217, 217, 217);

        /// <summary>
        /// Gets or sets the flat colour used by <see cref="SurfaceColorMode.Lit"/>. In
        /// <see cref="SurfaceMode.Mesh"/> the colour is modulated by the per-fragment lighting
        /// term (so curvature stays visible); in <see cref="SurfaceMode.PointCloud"/> it is drawn
        /// flat. Defaults to <c>RGB(217,217,217)</c>, matching the previously hard-coded 0.85 grey.
        /// Changing this value updates a shader uniform only — no GL resources are rebuilt.
        /// </summary>
        public Color SolidColor
        {
            get => _solidColor;
            set
            {
                if (_solidColor == value) return;
                _solidColor = value;
                if (_cachedRenderData?.Uniforms != null)
                    _cachedRenderData.Uniforms["solidColor"] = value;
            }
        }

        private float _opacity = 1f;

        /// <summary>
        /// Gets or sets the item's opacity in the range [0, 1] (values outside are clamped).
        /// Default <c>1</c> (fully opaque). Values below 1 make the item translucent: it is
        /// drawn after the opaque items and stops writing depth, so it blends with what is
        /// already in the framebuffer instead of occluding it. Changing this value updates a
        /// shader uniform only — no GL resources are rebuilt.
        /// </summary>
        /// <remarks>
        /// Translucent items are not sorted back-to-front: multiple translucent items blend in
        /// draw order, and a closed mesh blends its front and back faces (so e.g. 0.5 opacity
        /// looks closer to 0.75 on a box). Intended for a few translucent overlays on an opaque
        /// scene, not for many intersecting translucent solids.
        /// </remarks>
        public float Opacity
        {
            get => _opacity;
            set
            {
                float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);
                if (_opacity == clamped) return;
                _opacity = clamped;
                if (_cachedRenderData?.Uniforms != null)
                    _cachedRenderData.Uniforms["opacity"] = clamped;
            }
        }

        /// <summary>True when this item must be drawn in the translucent pass.</summary>
        protected bool IsTranslucent => _opacity < 1f;

        private float _pointSize = 2f;

        /// <summary>
        /// Gets or sets the point size in pixels used when <see cref="SurfaceMode.PointCloud"/>
        /// is active. Default <c>2</c> (values below 1 are clamped at draw time). Ignored in
        /// <see cref="SurfaceMode.Mesh"/>.
        /// </summary>
        public float PointSize
        {
            get => _pointSize;
            set => _pointSize = value;
        }

        /// <summary>Cached CPU-side render data; <c>null</c> means it must be rebuilt.</summary>
        protected RenderData _cachedRenderData;

        // Lazily computed inverse of ModelMatrix, used by the picking path so a mouse move does
        // not invert the pose of every item. Invalidated by the ModelMatrix setter.
        private CxMatrix4X4 _modelMatrixInverse = CxMatrix4X4.Identity();
        private bool _modelMatrixInverseDirty = true;

        /// <summary>
        /// Inverse of <see cref="ModelMatrix"/>, computed on first use after each pose change.
        /// Used to move the picking point into the item's local frame.
        /// </summary>
        internal CxMatrix4X4 ModelMatrixInverse
        {
            get
            {
                if (_modelMatrixInverseDirty)
                {
                    _modelMatrixInverse = (_modelMatrix ?? CxMatrix4X4.Identity()).Inverse();
                    _modelMatrixInverseDirty = false;
                }
                return _modelMatrixInverse;
            }
        }

        // 构造时一次性预计算的范围缓存（供 SurfaceColorMode 切换时快速赋值）
        private float _baseZMin, _baseZMax;   // BoundingBox Z 范围
        private float _baseDiffMin, _baseDiffMax;     // DiffValues 范围（null → 0,0）

        #region Shader 源码
        protected static readonly string VertexShaderSource =
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

        protected static readonly string FragmentShaderSource =
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
            uniform vec4 solidColor;
            uniform float opacity;
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
                    lightFactor = vec3((0.35 + 0.8 * diff) + 0.4 * spec);
                }
                if (colorMode == 3) {
                    if (isinf(diffValue)) discard;
                    FragColor = vec4(getColorByHeight(diffValue) * lightFactor, opacity);
                } else if (colorMode == 0) {
                    FragColor = vec4(getColorByHeight(height) * lightFactor, opacity);
                } else if (colorMode == 1) {
                    FragColor = vec4(vec3(intensity) * lightFactor, opacity);
                } else if (colorMode == 4) {
                    FragColor = vec4(solidColor.rgb * lightFactor, opacity);
                } else {
                    FragColor = vec4(mix(vec3(intensity), getColorByHeight(height), 0.5) * lightFactor, opacity);
                }
            }";
        #endregion Shader 源码

        // CxDisplay shares this exact shader pair across all built-in surface items. Keep the
        // protected fields for derived-item compatibility and expose internal read-only views for
        // the context-owned program cache.
        internal static string SharedVertexShaderSource => VertexShaderSource;
        internal static string SharedFragmentShaderSource => FragmentShaderSource;

        /// <summary>
        /// Initializes the shared mode fields without running the property setters (they depend on
        /// the bounding box and diff range computed later by <see cref="Initialize"/>).
        /// </summary>
        protected AbstractCxObjRenderItem(SurfaceMode surfaceMode, SurfaceColorMode surfaceColorMode)
        {
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;
        }

        /// <summary>
        /// Called by derived constructors once the data object has been assigned: computes the
        /// bounding box, the diff range and the initial colour range, in that order.
        /// </summary>
        protected void Initialize()
        {
            BoundingBox = ComputeBoundingBox();
            ComputeDiffRange(GetDiffData());
            UpdateWorldZRange();
        }

        /// <summary>Computes the local bounding box of the current data object (may be <c>null</c>).</summary>
        protected abstract CxBox3D? ComputeBoundingBox();

        /// <summary>Gets the data object's diff channel (may be <c>null</c>).</summary>
        protected abstract float[] GetDiffData();

        /// <summary>True when the data object carries a diff channel usable by the Diff colour mode.</summary>
        protected abstract bool HasSufficientDiffData();

        /// <summary>True when the data object holds uploadable geometry.</summary>
        protected abstract bool HasRenderableData { get; }

        /// <summary>Drops the reference to the data object (which the item does not own).</summary>
        protected abstract void ClearDataReference();

        /// <summary>Builds and caches the render data for the current data object.</summary>
        protected abstract RenderData BuildRenderData();

        /// <summary>Prepares CPU-side render data. Does not make GL calls.</summary>
        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || !HasRenderableData)
                return null;

            var data = BuildRenderData();
            // Base-class uniforms shared by all three items are injected here so the derived
            // BuildRenderData implementations do not have to know about them.
            if (data?.Uniforms != null)
            {
                data.Uniforms["solidColor"] = _solidColor;
                data.Uniforms["opacity"] = _opacity;
            }
            return data;
        }

        public void Draw(OpenGL gl, GLResourceHandle handle)
        {
            if (!handle.IsValid || IsDisposed) return;
            var data = _cachedRenderData;
            if (data == null) return;

            // A PointCloud -> Mesh mode switch invalidates the CPU cache and may be observed
            // before the render thread has rebuilt the GL handle. Do not issue DrawElements(0).
            if (_surfaceMode == SurfaceMode.Mesh && data.IndexCount <= 0) return;

            var renderer = ModernGLRenderer.For(gl);
            // Only items that actually change GL state (translucent -> depth write off,
            // point cloud -> point size) pay for the capture/restore round trip.
            bool changesState = IsTranslucent || _surfaceMode == SurfaceMode.PointCloud;
            ModernGLRenderer.RenderStateSnapshot previous = default;
            if (changesState) previous = renderer.CaptureState(gl);

            gl.UseProgram(handle.ShaderProgram);
            gl.BindVertexArray(handle.VaoId);

            renderer.UploadSceneMatrices(gl, handle, ModelMatrix);

            foreach (var kv in data.Uniforms)
            {
                int loc = handle.GetUniformLocation(gl, kv.Key);
                if (kv.Value is float f) gl.Uniform1(loc, f);
                else if (kv.Value is int i) gl.Uniform1(loc, i);
                else if (kv.Value is Color c)
                    gl.Uniform4(loc, c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            }

            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, handle.TextureId);
            gl.Uniform1(handle.GetUniformLocation(gl, "intensityTexture"), 0);

            // Translucent items must not write depth: they are drawn after the opaque pass and
            // have to blend with what is already in the framebuffer.
            if (IsTranslucent) gl.DepthMask(0);

            if (_surfaceMode == SurfaceMode.PointCloud)
            {
                gl.PointSize(Math.Max(1f, _pointSize));
                gl.DrawArrays(OpenGL.GL_POINTS, 0, data.VertexCount);
            }
            else
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, data.IndexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
            }

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);
            if (changesState) renderer.RestoreState(gl, previous);
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
            ClearDataReference();
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
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
    }
}
