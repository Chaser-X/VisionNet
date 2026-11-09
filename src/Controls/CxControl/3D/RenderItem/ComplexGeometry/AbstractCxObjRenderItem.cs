using System;
using System.Collections.Generic;
using System.Drawing;
using OpenTK.Graphics.OpenGL;
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

        private CxMatrix4X4 _modelMatrix = CxMatrix4X4.Identity();

        /// <summary>
        /// Model matrix applied to this item's geometry before the camera transform.
        /// Represents the item's pose (position and orientation) in world space.
        /// Defaults to identity (no transform).
        /// Setting a new pose recomputes the world-space Z range used for colour mapping.
        /// </summary>
        /// <remarks>
        /// Treat the matrix as immutable: the inverse used by the picking path is cached and only
        /// recomputed when this property is assigned. Mutating the assigned matrix's
        /// <see cref="CxMatrix4X4.Data"/> array in place would leave that cache stale — assign a new
        /// matrix instead.
        /// </remarks>
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

        private Color _solidColor = CxDisplayDefaults.SolidColor;

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

        private float _metallic = CxDisplayDefaults.PbrMetallic;

        /// <summary>
        /// Gets or sets the PBR metallic factor in the range [0, 1] (clamped; <see cref="float.NaN"/>
        /// becomes 0). Default <c>0</c> (dielectric). Only used by
        /// <see cref="SurfaceLightingMode.Pbr"/>. Changing this value updates a shader uniform
        /// only — no GL resources are rebuilt, and it does not by itself request a repaint.
        /// </summary>
        public float Metallic
        {
            get => _metallic;
            set
            {
                float clamped = ClampUnit(value, 0f);
                if (_metallic == clamped) return;
                _metallic = clamped;
                if (_cachedRenderData?.Uniforms != null)
                    _cachedRenderData.Uniforms["metallic"] = clamped;
            }
        }

        private float _roughness = CxDisplayDefaults.PbrRoughness;

        /// <summary>
        /// Gets or sets the PBR roughness factor in the range [0, 1] (clamped; <see cref="float.NaN"/>
        /// becomes 0.5). Default <c>0.5</c>. Only used by <see cref="SurfaceLightingMode.Pbr"/>.
        /// Changing this value updates a shader uniform only — no GL resources are rebuilt.
        /// </summary>
        public float Roughness
        {
            get => _roughness;
            set
            {
                float clamped = ClampUnit(value, 0.5f);
                if (_roughness == clamped) return;
                _roughness = clamped;
                if (_cachedRenderData?.Uniforms != null)
                    _cachedRenderData.Uniforms["roughness"] = clamped;
            }
        }

        private static float ClampUnit(float value, float nanFallback)
            => float.IsNaN(value) ? nanFallback : (value < 0f ? 0f : (value > 1f ? 1f : value));

        private float _opacity = CxDisplayDefaults.Opacity;

        /// <summary>
        /// Gets or sets the item's opacity in the range [0, 1] (values outside are clamped, and
        /// <see cref="float.NaN"/> is treated as fully opaque). Default <c>1</c> (fully opaque).
        /// Values below 1 make the item translucent: it is drawn after the opaque items and stops
        /// writing depth, so it blends with what is already in the framebuffer instead of occluding
        /// it. Changing this value updates a shader uniform only — no GL resources are rebuilt.
        /// </summary>
        /// <remarks>
        /// Translucent items are not sorted back-to-front: multiple translucent items blend in
        /// draw order, and a closed mesh blends its front and back faces (so e.g. 0.5 opacity
        /// looks closer to 0.75 on a box). Intended for a few translucent overlays on an opaque
        /// scene, not for many intersecting translucent solids.
        /// <para>Like the other uniform-only settings (<see cref="SolidColor"/>), this does not
        /// request a repaint by itself: call <c>CxDisplay.Redraw()</c> after changing it outside a
        /// data update.</para>
        /// </remarks>
        public float Opacity
        {
            get => _opacity;
            set
            {
                // NaN would pass both comparisons below and reach the shader as an alpha of NaN.
                float clamped = float.IsNaN(value)
                    ? 1f
                    : (value < 0f ? 0f : (value > 1f ? 1f : value));
                if (_opacity == clamped) return;
                _opacity = clamped;
                if (_cachedRenderData?.Uniforms != null)
                    _cachedRenderData.Uniforms["opacity"] = clamped;
            }
        }

        /// <summary>True when this item must be drawn in the translucent pass.</summary>
        protected bool IsTranslucent => _opacity < 1f;

        private bool _translucentWritesDepth = CxDisplayDefaults.TranslucentWritesDepth;

        /// <summary>
        /// Gets or sets whether a translucent item (<see cref="Opacity"/> below 1) still writes the
        /// depth buffer. Default <c>true</c>: the translucent surface is then part of the depth
        /// buffer, so depth-based picking (selection, coordinate tooltip) and occlusion keep
        /// working, and a farther translucent item can no longer blend over a nearer one. Set to
        /// <c>false</c> for the historical behaviour where translucent items do not write depth
        /// (they blend over each other in draw order, but are invisible to picking).
        /// </summary>
        public bool TranslucentWritesDepth
        {
            get => _translucentWritesDepth;
            set => _translucentWritesDepth = value;
        }

        private float _pointSize = CxDisplayDefaults.PointSize;

        /// <summary>
        /// Gets or sets the point size in pixels used when <see cref="SurfaceMode.PointCloud"/>
        /// is active. Default from <see cref="CxDisplayDefaults.PointSize"/> (<c>3</c>); values below
        /// 1 are clamped at draw time. Ignored in <see cref="SurfaceMode.Mesh"/>.
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
            layout (location = 4) in vec3 aNormal;

            uniform mat4 view;
            uniform mat4 projection;
            uniform mat4 model;
            uniform mat3 normalMatrix;

            out float height;
            out vec2 TexCoord;
            out float diffValue;
            out float vertexIntensity;
            out vec3 viewPos;
            out vec3 vNormal;
            out vec3 vWorldPos;
            out vec3 vWorldNormal;
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
                // normalMatrix maps the local normal to world space; the view rotation
                // brings it into the same frame as viewPos / lightDirView so the legacy
                // lighting dot products stay consistent. The world-space pair feeds the
                // PBR/IBL path, whose cubemap lookups live in world space.
                vWorldNormal = normalMatrix * aNormal;
                vNormal = mat3(view) * vWorldNormal;
                vWorldPos = worldPos.xyz;
                lightDirView = normalize(vec3(0.3, 0.4, 0.85));
            }";

        protected static readonly string FragmentShaderSource =
            @"#version 330 core
            in float height;
            in vec2 TexCoord;
            in float diffValue;
            in float vertexIntensity;
            in vec3 viewPos;
            in vec3 vNormal;
            in vec3 vWorldPos;
            in vec3 vWorldNormal;
            in vec3 lightDirView;
            out vec4 FragColor;

            uniform mat4 view;
            uniform float colorMin;
            uniform float colorMax;
            uniform int colorMode;
            uniform int surfaceMode;
            uniform int useVertexNormal;
            uniform int lightingMode;
            uniform int impostor;
            uniform int envFollowsCamera;
            uniform float metallic;
            uniform float roughness;
            uniform float iblIntensity;
            uniform float prefilterMaxMip;
            uniform samplerCube irradianceMap;
            uniform samplerCube prefilterMap;
            uniform sampler2D brdfLUT;
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

            vec3 fresnelSchlickRoughness(float cosTheta, vec3 f0, float r)
            {
                return f0 + (max(vec3(1.0 - r), f0) - f0)
                    * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
            }

            // Legacy per-fragment Blinn-Phong term (kept identical to the pre-PBR path).
            // N is a view-space normal supplied by the caller.
            vec3 legacyLightFactor(vec3 N)
            {
                vec3 V = normalize(-viewPos);
                if (dot(N, V) < 0.0) N = -N;
                if (!(dot(N, N) > 1e-6)) N = V;
                vec3 L = normalize(lightDirView);
                vec3 H = normalize(V + L);
                float diff = max(dot(N, L), 0.0);
                float spec = pow(max(dot(N, H), 0.0), 24.0) * 0.4;
                return vec3((0.35 + 0.8 * diff) + 0.4 * spec);
            }

            // Split-sum image-based lighting. Everything is evaluated in world space so the
            // irradiance / prefilter cubemaps (baked in world space) are sampled correctly.
            // N is a world-space normal supplied by the caller.
            vec3 pbrShade(vec3 albedo, vec3 N)
            {
                vec3 camPos = -transpose(mat3(view)) * view[3].xyz;
                vec3 V = normalize(camPos - vWorldPos);
                if (dot(N, V) < 0.0) N = -N;
                if (!(dot(N, N) > 1e-6)) N = V;

                float NdotV = max(dot(N, V), 0.0);
                vec3 R = reflect(-V, N);

                vec3 F0 = mix(vec3(0.04), albedo, metallic);
                vec3 F = fresnelSchlickRoughness(NdotV, F0, roughness);
                vec3 kD = (1.0 - F) * (1.0 - metallic);

                // The environment is baked in world space (world-fixed lighting: rotating the
                // camera does not relight a face). When envFollowsCamera is on, look it up in
                // view space instead, so the environment rotates with the camera.
                vec3 envN = envFollowsCamera == 1 ? mat3(view) * N : N;
                vec3 envR = envFollowsCamera == 1 ? mat3(view) * R : R;

                vec3 irradiance = texture(irradianceMap, envN).rgb;
                vec3 diffuse = irradiance * albedo;

                vec3 prefiltered = textureLod(prefilterMap, envR, roughness * prefilterMaxMip).rgb;
                vec2 brdf = texture(brdfLUT, vec2(NdotV, roughness)).rg;
                vec3 specular = prefiltered * (F * brdf.x + brdf.y);

                return (kD * diffuse + specular) * iblIntensity;
            }

            void main()
            {
                if (isinf(height)) discard;
                float intensity = useIntensityAttribute == 1
                    ? vertexIntensity : texture(intensityTexture, TexCoord).r;

                vec3 baseColor;
                if (colorMode == 3) {
                    if (isinf(diffValue)) discard;
                    baseColor = getColorByHeight(diffValue);
                } else if (colorMode == 0) {
                    baseColor = getColorByHeight(height);
                } else if (colorMode == 1) {
                    baseColor = vec3(intensity);
                } else if (colorMode == 4) {
                    baseColor = solidColor.rgb;
                } else {
                    baseColor = mix(vec3(intensity), getColorByHeight(height), 0.5);
                }

                vec3 shaded = baseColor;
                if (surfaceMode == 1)
                {
                    // Mesh: real normals when available, else the screen-space derivative.
                    if (lightingMode == 1)
                    {
                        vec3 N = useVertexNormal == 1
                            ? normalize(vWorldNormal)
                            : normalize(cross(dFdx(vWorldPos), dFdy(vWorldPos)));
                        shaded = pbrShade(baseColor, N);
                    }
                    else
                    {
                        vec3 N = useVertexNormal == 1
                            ? normalize(vNormal)
                            : normalize(cross(dFdx(viewPos), dFdy(viewPos)));
                        shaded = baseColor * legacyLightFactor(N);
                    }
                }
                else if (impostor == 1)
                {
                    // Point cloud: shade each point as a small sphere (point-sprite impostor).
                    // gl_PointCoord is (0,0) at the top-left of the sprite, so negate y to get a
                    // view-space up direction; the discard trims the square sprite to a disc.
                    vec2 pc = gl_PointCoord * 2.0 - 1.0;
                    float r2 = dot(pc, pc);
                    if (r2 > 1.0) discard;
                    vec3 Nview = normalize(vec3(pc.x, -pc.y, sqrt(max(0.0, 1.0 - r2))));
                    shaded = lightingMode == 1
                        ? pbrShade(baseColor, transpose(mat3(view)) * Nview)
                        : baseColor * legacyLightFactor(Nview);
                }
                FragColor = vec4(shaded, opacity);
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

        /// <summary>
        /// Contributes this item's answer to a world-space pick query, lowering the running best
        /// when this item is a better match than whatever earlier items reported.
        /// </summary>
        /// <remarks>
        /// Overridden by each built-in item so the picking path needs no per-type branching: a
        /// structured surface searches its grid, while a mesh or point cloud has no grid and
        /// answers with the query point itself. The default implementation contributes nothing.
        /// </remarks>
        /// <param name="world">Query point in world space.</param>
        /// <param name="useDiff">True in Diff colour mode: report Diff values instead of Intensity.</param>
        /// <param name="bestDist">Best squared distance so far; lowered when this item is closer.</param>
        /// <param name="bestPoint">Best world-space point so far; updated with <paramref name="bestDist"/>.</param>
        /// <param name="bestIntensity">Value at the best point (diff or intensity, per <paramref name="useDiff"/>).</param>
        internal virtual void PickAt(CxPoint3D world, bool useDiff,
            ref float bestDist, ref CxPoint3D? bestPoint, ref float? bestIntensity)
        {
        }

        /// <summary>
        /// True when the item draws with the context-owned shared surface program instead of
        /// compiling a private one from <see cref="RenderData.ShaderSource"/>.
        /// </summary>
        /// <remarks>
        /// Every built-in item shares the context-owned program (see the class remarks), so this
        /// defaults to <c>true</c>. A derived item that supplies its own GLSL pair must override
        /// it to <c>false</c>, otherwise its source is ignored. This replaces comparing shader
        /// source text at resource-creation time.
        /// </remarks>
        internal virtual bool UsesSharedSurfaceProgram => true;

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
                data.Uniforms["metallic"] = _metallic;
                data.Uniforms["roughness"] = _roughness;
                // Lit fragments use the interpolated normal only when the item actually built
                // one; otherwise the shader falls back to the screen-space derivative normal.
                data.Uniforms["useVertexNormal"] = data.Normals != null ? 1 : 0;
            }
            return data;
        }

        public void Draw(CxGlContext gl, GLResourceHandle handle)
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
            if (changesState) previous = renderer.CaptureState();

            GL.UseProgram(handle.ShaderProgram);
            GL.BindVertexArray(handle.VaoId);

            renderer.UploadSceneMatrices(handle, ModelMatrix);
            // Reuses the cached pose inverse (also used by picking) instead of inverting again.
            renderer.UploadNormalMatrix(handle, ModelMatrixInverse);

            foreach (var kv in data.Uniforms)
            {
                int loc = handle.GetUniformLocation(kv.Key);
                if (kv.Value is float f) GL.Uniform1(loc, f);
                else if (kv.Value is int i) GL.Uniform1(loc, i);
                else if (kv.Value is Color c)
                    GL.Uniform4(loc, c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            }

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, handle.TextureId);
            GL.Uniform1(handle.GetUniformLocation("intensityTexture"), 0);

            // Translucent items write depth by default, so they stay part of the depth buffer
            // (depth-based picking and occlusion keep working). Opting out restores the historical
            // behaviour where they only blend over what is already in the framebuffer.
            if (IsTranslucent && !_translucentWritesDepth) GL.DepthMask(false);

            if (_surfaceMode == SurfaceMode.PointCloud)
            {
                GL.PointSize(Math.Max(1f, _pointSize));
                GL.DrawArrays(PrimitiveType.Points, 0, data.VertexCount);
            }
            else
            {
                GL.DrawElements(PrimitiveType.Triangles, data.IndexCount, DrawElementsType.UnsignedInt, IntPtr.Zero);
            }

            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
            if (changesState) renderer.RestoreState(previous);
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
