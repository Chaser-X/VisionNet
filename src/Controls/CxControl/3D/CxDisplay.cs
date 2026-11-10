using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// OpenGL-based 3D display control for rendering point clouds, meshes, and geometric primitives.
    /// Manages all GL resources internally; render items provide only CPU-side data.
    /// </summary>
    public partial class CxDisplay : GLControl, IDisposable
    {
        // ── GL context facade ────────────────────────────────────────────────────
        // The only GL-related object the render items and helper services see; it hides the
        // concrete OpenTK binding from the rest of the control and from its public surface.
        private readonly CxGlContext _gl;

        // Off-screen multisampled render target (display) plus its 1-pixel depth readback (picking).
        // Created lazily inside the frame; falls back to drawing straight to the window when the
        // driver cannot provide a complete framebuffer.
        private readonly CxMsaaRenderTarget _renderTarget = new CxMsaaRenderTarget();

        // ── Camera & interaction state ───────────────────────────────────────────
        private CxAdvancedTrackBallCamera _camera;
        private bool _isMouseDown;

        // ── Active-object selection state ────────────────────────────────────────
        private AbstractRenderItem _selectedItem     = null;
        private bool               _isDraggingItem   = false;
        private CxPoint3D?         _lastDragWorldPos = null;

        // ── Item registry (surface + overlay queues, keys, GL resource pool) ─────
        // Every mutable item collection lives here, behind one lock owned by the registry.
        private readonly CxItemRegistry _registry = new CxItemRegistry();

        // Serializes a frame with item disposal and snapshot cleanup. The render
        // snapshots are intentionally reused, so clearing or disposing their
        // members while the GL thread is iterating them is not safe.
        private readonly object _renderLock = new object();
        private readonly object _cameraLock = new object();
        private int _invalidatePending;

        // Items whose shader failed while processing the resource pool this frame. Collected during
        // the locked pass and reported through RenderItemFailed once the lock has been released, so
        // handlers may safely call back into the display. Render-thread only.
        private readonly List<ICxObjRenderItem> _failedItems = new List<ICxObjRenderItem>();

        // ── Frame snapshots ─────────────────────────────────────────────────────
        // Reused between frames: filled from the registry (under its lock) and then consumed
        // after the lock is released, so a drag/resize allocates no list per paint.
        private readonly List<ICxObjRenderItem> _surfaceSnapshot = new List<ICxObjRenderItem>();
        // GL handles aligned by index with _surfaceSnapshot, so the draw loop needs no per-item lock.
        private readonly List<GLResourceHandle> _handleSnapshot = new List<GLResourceHandle>();
        private readonly List<IRenderItem> _overlaySnapshot = new List<IRenderItem>();

        // ── HUD overlay objects ──────────────────────────────────────────────────
        private CxCoordinateSystemItem _coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem         _colorBarItem     = new CxColorBarItem();
        private CxCoordinationTagItem  _coordTagItem     = new CxCoordinationTagItem();

        // ── Mode backing fields ──────────────────────────────────────────────────
        // All defaults are read once from CxDisplayDefaults at construction; see CxDisplayDefaults.
        private SurfaceMode      _surfaceMode      = CxDisplayDefaults.SurfaceMode;
        private SurfaceColorMode _surfaceColorMode = CxDisplayDefaults.SurfaceColorMode;

        // Frame-global lighting model and IBL environment (P2). Unlike the surface/colour modes
        // these are not propagated per item: they are applied once per frame to the shared
        // surface program, so every built-in item picks them up.
        private SurfaceLightingMode      _surfaceLightingMode = CxDisplayDefaults.SurfaceLightingMode;
        private SurfaceEnvironmentPreset _environmentPreset   = CxDisplayDefaults.EnvironmentPreset;
        private float                    _iblIntensity        = CxDisplayDefaults.IblIntensity;
        private bool                     _pointImpostor       = CxDisplayDefaults.PointImpostor;
        private bool                     _environmentFollowsCamera = CxDisplayDefaults.EnvironmentFollowsCamera;

        // ── Render background colour (OpenGL clear colour); default: opaque black ──
        // The control resolves its off-screen multisampled scene into the window, so the window
        // itself is opaque: the alpha component is no longer composited with the parent. Default
        // changed from transparent black to opaque black as part of the OpenTK migration.
        private Color _backColor = Color.FromArgb(255, 0, 0, 0);

        // Optional vertical background gradient (top → bottom). null = the solid clear colour above.
        // Reused buffers so a gradient frame allocates nothing.
        private (Color Top, Color Bottom)? _backgroundGradient;
        private readonly float[] _backgroundGradientPositions = new float[8];
        private readonly float[] _backgroundGradientColors = new float[16];

        // ── Manual colour / diff display ranges (override the per-frame auto-aggregated ranges) ──
        // When non-null, the renderer propagates the range to every item instead of the
        // auto-aggregated global range, and the colour bar uses it. null = auto.
        private (float min, float max)? _manualColorRange = null;
        private (float min, float max)? _manualDiffRange  = null;

        // ── Public properties ────────────────────────────────────────────────────

        /// <summary>Gets the trackball camera controlling the 3D view.</summary>
        public CxAdvancedTrackBallCamera Camera => _camera;

        /// <summary>
        /// Gets or sets whether the right-click context menu is shown.
        /// Default <c>true</c>. Set <c>false</c> to suppress the menu — the control still
        /// receives right-click for camera / selection, but no menu pops up. The menu and its
        /// event handlers are retained, so toggling back to <c>true</c> restores full behaviour.
        /// </summary>
        public bool IsMenuVisible
        {
            get => ContextMenuStrip != null;
            set
            {
                ContextMenuStrip = value ? menu_right : null;
                UpdateMenuVisibility();
            }
        }

        private bool _enableSurfaceMode = true;
        private bool _enableSurfaceColorMode = true;

        /// <summary>
        /// Gets or sets whether the "SurfaceMode" item in the right-click menu is visible.
        /// Only effective while <see cref="IsMenuVisible"/> is true. Default <c>true</c>.
        /// </summary>
        public bool EnableSurfaceMode
        {
            get => _enableSurfaceMode;
            set { _enableSurfaceMode = value; UpdateMenuVisibility(); }
        }

        /// <summary>
        /// Gets or sets whether the "SurfaceColorMode" item in the right-click menu is visible.
        /// Only effective while <see cref="IsMenuVisible"/> is true. Default <c>true</c>.
        /// </summary>
        public bool EnableSurfaceColorMode
        {
            get => _enableSurfaceColorMode;
            set { _enableSurfaceColorMode = value; UpdateMenuVisibility(); }
        }

        /// <summary>Gets or sets the current view mode (Top, Front, Left, etc.).</summary>
        [Obsolete("Use Camera.ViewMode instead.")]
        public ViewMode SurfaceViewMode
        {
            get => _camera.ViewMode;
            set
            {
                if (_camera.ViewMode != value)
                {
                    _camera.ViewMode = value;
                    UpdateMenuItems();
                }
            }
        }

        /// <summary>Gets or sets the surface rendering mode (PointCloud or Mesh).</summary>
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set
            {
                if (_surfaceMode != value) { _surfaceMode = value; UpdateMenuItems(); }
                var snapshot = _registry.CopyItems();
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceMode = value;
            }
        }

        /// <summary>
        /// Gets or sets the surface colour mode (Color, Intensity, ColorWithIntensity, Diff or Lit)
        /// and applies it to every registered surface item.
        /// </summary>
        public SurfaceColorMode SurfaceColorMode
        {
            get => _surfaceColorMode;
            set
            {
                if (_surfaceColorMode != value) { _surfaceColorMode = value; UpdateMenuItems(); }
                var snapshot = _registry.CopyItems();
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceColorMode = value;
            }
        }

        /// <summary>
        /// Gets or sets the Mesh-mode lighting model applied to every surface item.
        /// Default <see cref="SurfaceLightingMode.Pbr"/>. <see cref="SurfaceLightingMode.Legacy"/>
        /// is the historical fixed-direction Blinn-Phong look. Only affects items drawn as meshes;
        /// point clouds use the impostor shading of <see cref="PointImpostor"/>.
        /// </summary>
        public SurfaceLightingMode SurfaceLightingMode
        {
            get => _surfaceLightingMode;
            set
            {
                if (_surfaceLightingMode == value) return;
                _surfaceLightingMode = value;
                SafeInvalidate();
            }
        }

        /// <summary>
        /// Gets or sets the procedural IBL environment used by
        /// <see cref="SurfaceLightingMode.Pbr"/>. Default <see cref="SurfaceEnvironmentPreset.Neutral"/>.
        /// Changing it re-generates/re-uploads the environment on the next frame.
        /// </summary>
        public SurfaceEnvironmentPreset EnvironmentPreset
        {
            get => _environmentPreset;
            set
            {
                if (_environmentPreset == value) return;
                _environmentPreset = value;
                SafeInvalidate();
            }
        }

        /// <summary>
        /// Gets or sets the IBL intensity multiplier for <see cref="SurfaceLightingMode.Pbr"/>
        /// (clamped to &gt;= 0; <see cref="float.NaN"/> becomes 0). Default <c>1.3</c>.
        /// </summary>
        public float IblIntensity
        {
            get => _iblIntensity;
            set
            {
                float clamped = float.IsNaN(value) ? 0f : (value < 0f ? 0f : value);
                if (_iblIntensity == clamped) return;
                _iblIntensity = clamped;
                SafeInvalidate();
            }
        }

        /// <summary>
        /// Gets or sets whether point-cloud items are drawn as pseudo-3D sphere impostors
        /// (a shaded highlight ball per point) instead of flat points. Default <c>true</c>.
        /// Only affects <see cref="SurfaceMode.PointCloud"/>; mesh items are unaffected.
        /// </summary>
        public bool PointImpostor
        {
            get => _pointImpostor;
            set
            {
                if (_pointImpostor == value) return;
                _pointImpostor = value;
                SafeInvalidate();
            }
        }

        private bool _msaaEnabled = CxDisplayDefaults.MsaaEnabled;

        /// <summary>
        /// Gets or sets whether the off-screen render target uses MSAA (default <c>true</c>).
        /// When enabled, mesh and line edges are smoothed by up to 4× multisampling; point-cloud
        /// impostor discs (cut out with <c>discard</c>) are <b>not</b> affected. Turning it off
        /// renders straight to the window instead (no off-screen target, no resolve blit). Depth
        /// picking is unaffected either way, because it reads whichever depth buffer holds the
        /// scene. Takes effect on the next frame.
        /// </summary>
        public bool MsaaEnabled
        {
            get => _msaaEnabled;
            set
            {
                if (_msaaEnabled == value) return;
                _msaaEnabled = value;
                SafeInvalidate();
            }
        }

        private float _pointSize = CxDisplayDefaults.PointSize;

        /// <summary>
        /// Gets or sets the point size in pixels applied to every surface item (clamped at draw time
        /// to &gt;= 1). Default from <see cref="CxDisplayDefaults.PointSize"/> (<c>3</c>). Only used by
        /// <see cref="SurfaceMode.PointCloud"/>; newly added items inherit the current value.
        /// </summary>
        public float PointSize
        {
            get => _pointSize;
            set
            {
                if (_pointSize == value) return;
                _pointSize = value;
                ApplyToItems(o => o.PointSize = value);
            }
        }

        private float _pbrMetallic = CxDisplayDefaults.PbrMetallic;

        /// <summary>
        /// Gets or sets the PBR metallic factor applied to every surface item (clamped to [0, 1];
        /// <see cref="float.NaN"/> becomes 0). Default <c>0.2</c>. Newly added items inherit the
        /// current value; only used by <see cref="SurfaceLightingMode.Pbr"/>.
        /// </summary>
        public float PbrMetallic
        {
            get => _pbrMetallic;
            set
            {
                float clamped = float.IsNaN(value) ? 0f : (value < 0f ? 0f : (value > 1f ? 1f : value));
                if (_pbrMetallic == clamped) return;
                _pbrMetallic = clamped;
                ApplyPbrMaterialToItems();
            }
        }

        private float _pbrRoughness = CxDisplayDefaults.PbrRoughness;

        /// <summary>
        /// Gets or sets the PBR roughness factor applied to every surface item (clamped to [0, 1];
        /// <see cref="float.NaN"/> becomes 0.5). Default <c>0.5</c>. Newly added items inherit the
        /// current value; only used by <see cref="SurfaceLightingMode.Pbr"/>.
        /// </summary>
        public float PbrRoughness
        {
            get => _pbrRoughness;
            set
            {
                float clamped = float.IsNaN(value) ? 0.5f : (value < 0f ? 0f : (value > 1f ? 1f : value));
                if (_pbrRoughness == clamped) return;
                _pbrRoughness = clamped;
                ApplyPbrMaterialToItems();
            }
        }

        /// <summary>Pushes <see cref="PbrMetallic"/>/<see cref="PbrRoughness"/> onto every item.</summary>
        private void ApplyPbrMaterialToItems()
            => ApplyToItems(o => { o.Metallic = _pbrMetallic; o.Roughness = _pbrRoughness; });

        /// <summary>Runs <paramref name="apply"/> on every surface item, then requests a repaint.</summary>
        private void ApplyToItems(Action<AbstractCxObjRenderItem> apply)
        {
            var snapshot = _registry.CopyItems();
            foreach (var item in snapshot)
                if (!item.IsDisposed && item is AbstractCxObjRenderItem obj)
                    apply(obj);
            SafeInvalidate();
        }

        private Color _solidColor = CxDisplayDefaults.SolidColor;

        /// <summary>
        /// Gets or sets the flat colour used by <see cref="SurfaceColorMode.Lit"/> on every surface
        /// item. Default <c>RGB(217,217,217)</c>. Newly added items inherit the current value; it is
        /// only visible while <see cref="SurfaceColorMode"/> is <see cref="SurfaceColorMode.Lit"/>.
        /// </summary>
        public Color SolidColor
        {
            get => _solidColor;
            set
            {
                if (_solidColor == value) return;
                _solidColor = value;
                ApplyToItems(o => o.SolidColor = _solidColor);
            }
        }

        private float _opacity = CxDisplayDefaults.Opacity;

        /// <summary>
        /// Gets or sets the opacity applied to every surface item (clamped to [0, 1];
        /// <see cref="float.NaN"/> becomes 1). Default <c>1</c>. Values below 1 draw the items in the
        /// translucent pass (no depth write, blended over the opaque scene). Newly added items
        /// inherit the current value.
        /// </summary>
        public float Opacity
        {
            get => _opacity;
            set
            {
                float clamped = float.IsNaN(value) ? 1f : (value < 0f ? 0f : (value > 1f ? 1f : value));
                if (_opacity == clamped) return;
                _opacity = clamped;
                ApplyToItems(o => o.Opacity = clamped);
            }
        }

        private bool _translucentWritesDepth = CxDisplayDefaults.TranslucentWritesDepth;

        /// <summary>
        /// Gets or sets whether translucent items (<see cref="Opacity"/> below 1) still write the
        /// depth buffer. Default <c>true</c>, which keeps depth-based picking (selection, coordinate
        /// tooltip) and occlusion working for translucent items. Set to <c>false</c> for the
        /// historical behaviour where translucent items blend over each other in draw order but are
        /// invisible to picking. Newly added items inherit the current value.
        /// </summary>
        public bool TranslucentWritesDepth
        {
            get => _translucentWritesDepth;
            set
            {
                if (_translucentWritesDepth == value) return;
                _translucentWritesDepth = value;
                ApplyToItems(o => o.TranslucentWritesDepth = value);
            }
        }

        /// <summary>
        /// Gets or sets whether the PBR environment rotates with the camera. Default <c>true</c>:
        /// the environment is sampled in view space, so orbiting the view relights the object
        /// (headlight-like). Set to <c>false</c> to keep the environment fixed in world space,
        /// where a face's brightness is a property of the object and does not change with the view.
        /// Only used by <see cref="SurfaceLightingMode.Pbr"/>.
        /// </summary>
        public bool EnvironmentFollowsCamera
        {
            get => _environmentFollowsCamera;
            set
            {
                if (_environmentFollowsCamera == value) return;
                _environmentFollowsCamera = value;
                SafeInvalidate();
            }
        }

        /// <summary>Gets or sets whether the 3D coordinate system axes are shown in world space.</summary>
        public bool ShowCoordinateSystem { get; set; } = CxDisplayDefaults.ShowCoordinateSystem;

        private bool _isColorBarVisible = CxDisplayDefaults.IsColorBarVisible;

        /// <summary>
        /// Gets or sets whether the colour bar HUD overlay is drawn.
        /// Default <c>true</c>. When <c>false</c>, the bar is suppressed even if
        /// surface items share a uniform colour mode.
        /// </summary>
        public bool IsColorBarVisible
        {
            get => _isColorBarVisible;
            set
            {
                if (_isColorBarVisible == value) return;
                _isColorBarVisible = value;
                SafeInvalidate();
            }
        }

        /// <summary>
        /// Gets the currently selected geometry overlay item, or <c>null</c> if nothing is selected.
        /// Only one item can be selected at a time within this display.
        /// </summary>
        public AbstractRenderItem SelectedItem => _selectedItem;

        /// <summary>
        /// Gets or sets whether the coordinate system is left-handed.
        /// Delegates to <see cref="CxAdvancedTrackBallCamera.IsLeftHanded"/>.
        /// </summary>
        [Obsolete("Use Camera.IsLeftHanded instead.")]
        public bool IsLeftHanded
        {
            get => _camera.IsLeftHanded;
            set => _camera.IsLeftHanded = value;
        }

        // ── Constructors ─────────────────────────────────────────────────────────

        /// <summary>Initializes a new instance with the defaults from <see cref="CxDisplayDefaults"/>.</summary>
        public CxDisplay() : this(CxDisplayDefaults.ViewMode, CxDisplayDefaults.SurfaceMode, CxDisplayDefaults.SurfaceColorMode) { }

        /// <summary>
        /// Initializes a new instance with the specified view mode and surface rendering settings.
        /// </summary>
        /// <param name="viewMode">Initial camera view direction.</param>
        /// <param name="surfaceMode">Initial surface rendering mode.</param>
        /// <param name="surfaceColorMode">Initial surface color mode.</param>
        public CxDisplay(ViewMode viewMode = ViewMode.Top,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.ColorWithIntensity)
            : base(CreateGraphicsMode(), 3, 3, GraphicsContextFlags.Default)
        {
            _gl = new CxGlContext(this);
            _gl.DepthTarget = _renderTarget;
            // On-demand rendering: do not let SwapBuffers block on the display refresh.
            VSync = false;

            // The group manager is independent of the GL context and must exist in every mode
            // (including the WinForms designer). It takes the registry and this control's narrow
            // IDisplayHost contract rather than the control itself, so the two do not reference
            // each other.
            _groupManager = new CxGroupManager(_registry, this);

            if (!DesignMode)
            {
                InitializeComponent();
                _camera = new CxAdvancedTrackBallCamera(_gl);
                _camera.ViewMode = viewMode;
                SurfaceMode      = surfaceMode;
                SurfaceColorMode = surfaceColorMode;
                UpdateMenuItems();
                UpdateMenuVisibility();
            }
        }

        /// <summary>
        /// Clears a coalesced invalidation left over from a newly created handle and (re)applies the
        /// context-owned GL state. The base handler creates the context and makes it current, so
        /// this is the migration's replacement for the old <c>DoOpenGLInitialized</c> hook.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Interlocked.Exchange(ref _invalidatePending, 0);
            InitializeGlState();
        }

        /// <summary>
        /// Requests a double-buffered 32-bit colour / 24-bit depth context with no MSAA on the
        /// <b>window</b> framebuffer: the scene is drawn into an off-screen multisampled target
        /// (see <see cref="CxMsaaRenderTarget"/>) and resolved to the window, so the window itself
        /// does not need multisampling and depth-based picking stays exact. The context is created
        /// as a <b>compatibility</b> profile 3.3 context: the shared shaders are
        /// <c>#version 330 core</c>, while wide lines and point sprites still rely on
        /// compatibility-profile behaviour.
        /// </summary>
        private static GraphicsMode CreateGraphicsMode()
            => new GraphicsMode(new ColorFormat(32), 24, 8, 0, new ColorFormat(0), 2);

        // Scratch for the one-time context capability log (see LogContextCapabilities).
        private static readonly float[] _lineWidthRange = new float[2];

        /// <summary>
        /// Applies the context-owned GL defaults. Runs after the base handle-created handler has
        /// created the context and made it current, and again whenever the handle is recreated.
        /// </summary>
        private void InitializeGlState()
        {
            // Field initializers and the constructor body may not have run yet if the handle is
            // created during the base constructor; nothing to do in that case.
            if (DesignMode || _gl == null || _renderLock == null || !_gl.IsValid) return;
            lock (_renderLock)
            {
                // A context can be recreated on the same control. Forget all names belonging to the
                // old context and rebuild active items on the next frame.
                _registry.ForEachPoolHandle(handle => handle.ResetForContextRecreate());
                _registry.DropReleases();
                ModernGLRenderer.ResetForContextRecreate(_gl);
                GL.ClearColor(_backColor.R / 255f, _backColor.G / 255f, _backColor.B / 255f, _backColor.A / 255f);
                GL.PointSize(InitialPointSize);
                LogContextCapabilities();
            }
        }

        /// <summary>
        /// Writes the actual context version/renderer and the aliased line-width range to the debug
        /// output. Diagnostics only — nothing in the render path depends on the result.
        /// </summary>
        /// <remarks>
        /// The context is requested as a compatibility-profile 3.3 context (see
        /// <see cref="CreateGraphicsMode"/> and the base-constructor call), and the wide-line /
        /// point-sprite behaviour this control relies on is compatibility-only. Logging what the
        /// driver actually handed back turns "does compatibility 3.3 work on this machine?" from an
        /// unanswerable question into a line in the debug output.
        /// </remarks>
        [Conditional("DEBUG")]
        private void LogContextCapabilities()
        {
            try
            {
                GL.GetFloat(GetPName.AliasedLineWidthRange, _lineWidthRange);
                int maxSamples = GL.GetInteger(GetPName.MaxSamples);
                Debug.WriteLine(string.Format(
                    "[CxControl] GL context: {0} | renderer: {1} | shading language: {2} | aliased line width: [{3}, {4}] | max MSAA samples: {5}",
                    GL.GetString(StringName.Version) ?? "?",
                    GL.GetString(StringName.Renderer) ?? "?",
                    GL.GetString(StringName.ShadingLanguageVersion) ?? "?",
                    _lineWidthRange[0], _lineWidthRange[1], maxSamples));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CxControl] GL capability probe failed: " + ex.Message);
            }
        }

        /// <summary>Allows invalidation requests to be queued after the handle is recreated.</summary>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            Interlocked.Exchange(ref _invalidatePending, 0);
        }

        // ── Shared private helpers ───────────────────────────────────────────────

        /// <summary>
        /// Thread-safe invalidation: marshals to the UI thread when called from another thread.
        /// </summary>
        internal void SafeInvalidate()
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated
                    || Interlocked.CompareExchange(ref _invalidatePending, 1, 0) != 0)
                    return;

                try
                {
                    BeginInvoke(new Action(CompletePendingInvalidate));
                }
                catch (InvalidOperationException)
                {
                    // The handle may be destroyed between IsHandleCreated and BeginInvoke.
                    Interlocked.Exchange(ref _invalidatePending, 0);
                }
                return;
            }

            Interlocked.Exchange(ref _invalidatePending, 0);
            Invalidate();
        }

        private void CompletePendingInvalidate()
        {
            Interlocked.Exchange(ref _invalidatePending, 0);
            if (!IsDisposed && IsHandleCreated)
                Invalidate();
        }

        /// <summary>Synchronizes the context-menu check marks with the current mode settings.</summary>
        private void UpdateMenuItems()
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => UpdateMenuItems())); return; }

            SyncMenuSelection(viewModeToolStripMenuItem, _camera.ViewMode.ToString());
            SyncMenuSelection(surfaceModeToolStripMenuItem, _surfaceMode.ToString());
            SyncMenuSelection(surfaceColorModeToolStripMenuItem, _surfaceColorMode.ToString());
        }

        /// <summary>
        /// Checks the item in <paramref name="dropDownOwner"/> whose text equals
        /// <paramref name="selected"/> and unchecks every other item. Menu item texts are the
        /// enum names, which is what makes this text comparison the selection key.
        /// </summary>
        internal static void SyncMenuSelection(ToolStripMenuItem dropDownOwner, string selected)
        {
            if (dropDownOwner == null) return;
            foreach (ToolStripMenuItem item in dropDownOwner.DropDownItems)
                item.Checked = item.Text == selected;
        }

        /// <summary>Unchecks every item in <paramref name="dropDownOwner"/>.</summary>
        internal static void UncheckAllMenuItems(ToolStripMenuItem dropDownOwner)
        {
            if (dropDownOwner == null) return;
            foreach (ToolStripMenuItem item in dropDownOwner.DropDownItems)
                item.Checked = false;
        }

        /// <summary>
        /// Applies <see cref="EnableSurfaceMode"/> / <see cref="EnableSurfaceColorMode"/> to the
        /// visibility of their corresponding menu items, gated by <see cref="IsMenuVisible"/>.
        /// </summary>
        private void UpdateMenuVisibility()
        {
            if (InvokeRequired) { BeginInvoke(new Action(UpdateMenuVisibility)); return; }

            if (surfaceModeToolStripMenuItem != null)
                surfaceModeToolStripMenuItem.Visible = IsMenuVisible && _enableSurfaceMode;
            if (surfaceColorModeToolStripMenuItem != null)
                surfaceColorModeToolStripMenuItem.Visible = IsMenuVisible && _enableSurfaceColorMode;
        }

        /// <summary>
        /// Computes the world-space bounding box that encloses all active surface items,
        /// accounting for each item's model matrix (pose). The eight corners of each item's
        /// local bounding box are transformed and the union is accumulated.
        /// Returns <c>null</c> when no items are loaded or none have a valid bounding box.
        /// </summary>
        private CxBox3D? GetCombinedBoundingBox()
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool any = false;

            foreach (var item in _registry.SnapshotItems())
            {
                var bb = item.BoundingBox;
                if (bb == null) continue;
                var m = GetItemModelMatrix(item);

                float cx = bb.Value.Center.X, cy = bb.Value.Center.Y, cz = bb.Value.Center.Z;
                float hw = bb.Value.Size.Width  / 2f;
                float hh = bb.Value.Size.Height / 2f;
                float hd = bb.Value.Size.Depth  / 2f;

                for (int i = 0; i < 8; i++)
                {
                    var wp = m.TransformPoint3D(new CxPoint3D(
                        cx + ((i & 1) == 0 ? -hw : hw),
                        cy + ((i & 2) == 0 ? -hh : hh),
                        cz + ((i & 4) == 0 ? -hd : hd)));
                    if (wp.X < minX) minX = wp.X;
                    if (wp.X > maxX) maxX = wp.X;
                    if (wp.Y < minY) minY = wp.Y;
                    if (wp.Y > maxY) maxY = wp.Y;
                    if (wp.Z < minZ) minZ = wp.Z;
                    if (wp.Z > maxZ) maxZ = wp.Z;
                    any = true;
                }
            }

            if (!any) return null;
            return CxExtension.BuildBox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        /// <summary>
        /// Returns the model matrix (pose) of a poseable render item, or identity for
        /// items that do not support poses.
        /// </summary>
        private static CxMatrix4X4 GetItemModelMatrix(ICxObjRenderItem item)
        {
            if (item is ICxPoseable poseable)
                return poseable.ModelMatrix ?? CxMatrix4X4.Identity();
            return CxMatrix4X4.Identity();
        }

        /// <summary>
        /// Releases all GL resources, surface items, and overlay items held by this control.
        /// Called by <see cref="Dispose(bool)"/> in Designer.cs.
        /// </summary>
        private void DisposeResources()
        {
            lock (_renderLock)
            {
                // Deleting GL objects needs a current context. Dispose may run on any thread and
                // OpenTK does not make the context current implicitly.
                bool glValid = _gl != null && _gl.IsValid;
                if (glValid)
                {
                    try { _gl.EnsureCurrent(); }
                    catch { glValid = false; }
                }

                if (glValid)
                    _registry.DrainReleases(p => CxGLResourceManager.Release(p));

                Groups.Clear();
                _registry.DisposeAll(
                    (item, handle) =>
                    {
                        if (handle?.RenderDataChangedHandler != null)
                            item.OnRenderDataChanged -= handle.RenderDataChangedHandler;

                        // Disposal runs with the context current, so the handle is released directly
                        // instead of being queued for the render pass.
                        if (glValid && handle != null) CxGLResourceManager.Release(handle);
                        item.Dispose();
                    },
                    overlay => overlay.Dispose());

                _surfaceSnapshot.Clear();
                _handleSnapshot.Clear();
                _overlaySnapshot.Clear();

                if (glValid)
                {
                    _renderTarget.Dispose();
                    ModernGLRenderer.Release(_gl);
                }
            }
        }
    }
}
