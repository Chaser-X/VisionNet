using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// OpenGL-based 3D display control for rendering point clouds, meshes, and geometric primitives.
    /// Manages all GL resources internally; render items provide only CPU-side data.
    /// </summary>
    public partial class CxDisplay : OpenGLControl, IDisposable
    {
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
        private int _fboWidth = -1;
        private int _fboHeight = -1;

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
        private SurfaceMode      _surfaceMode      = SurfaceMode.PointCloud;
        private SurfaceColorMode _surfaceColorMode = SurfaceColorMode.ColorWithIntensity;

        // ── Render background colour (OpenGL clear colour); default: transparent black ──
        private Color _backColor = Color.FromArgb(0, 0, 0, 0);

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

        /// <summary>Gets or sets whether the 3D coordinate system axes are shown in world space.</summary>
        public bool ShowCoordinateSystem { get; set; }

        private bool _isColorBarVisible = true;

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

        /// <summary>Initializes a new instance with default settings.</summary>
        public CxDisplay() : this(ViewMode.Top, SurfaceMode.PointCloud, SurfaceColorMode.ColorWithIntensity) { }

        /// <summary>
        /// Initializes a new instance with the specified view mode and surface rendering settings.
        /// </summary>
        /// <param name="viewMode">Initial camera view direction.</param>
        /// <param name="surfaceMode">Initial surface rendering mode.</param>
        /// <param name="surfaceColorMode">Initial surface color mode.</param>
        public CxDisplay(ViewMode viewMode = ViewMode.Top,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.ColorWithIntensity)
        {
            // The group manager is independent of the GL context and must exist in every mode
            // (including the WinForms designer).
            _groupManager = new CxGroupManager(this);

            if (!DesignMode)
            {
                InitializeComponent();
                _camera = new CxAdvancedTrackBallCamera(this);
                _camera.ViewMode = viewMode;
                SurfaceMode      = surfaceMode;
                SurfaceColorMode = surfaceColorMode;
                UpdateMenuItems();
                UpdateMenuVisibility();
            }
        }

        /// <summary>Clears a coalesced invalidation left over from a newly created handle.</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Interlocked.Exchange(ref _invalidatePending, 0);
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

            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == _camera.ViewMode.ToString();
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == _surfaceMode.ToString();
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == _surfaceColorMode.ToString();
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
            return new CxBox3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
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
        /// Inverse of an item's model matrix. Built-in surface items cache their inverse (it is
        /// only recomputed when the pose changes); anything else is inverted on demand.
        /// </summary>
        private static CxMatrix4X4 GetItemInverseModelMatrix(ICxObjRenderItem item)
            => item is AbstractCxObjRenderItem surfaceItem
                ? surfaceItem.ModelMatrixInverse
                : GetItemModelMatrix(item).Inverse();

        /// <summary>
        /// Releases all GL resources, surface items, and overlay items held by this control.
        /// Called by <see cref="Dispose(bool)"/> in Designer.cs.
        /// </summary>
        private void DisposeResources(OpenGL gl)
        {
            lock (_renderLock)
            {
                if (gl != null)
                    _registry.DrainReleases(p => ReleaseGLResources(gl, p));

                _registry.Sync(() =>
                {
                    Groups.Clear();
                    foreach (var item in _registry.ItemsInLock)
                    {
                        if (_registry.PoolInLock.TryGetValue(item, out var h))
                        {
                            if (h.RenderDataChangedHandler != null)
                                item.OnRenderDataChanged -= h.RenderDataChangedHandler;

                            if (gl != null) ReleaseGLResources(gl, h);
                            _registry.PoolInLock.Remove(item);
                        }
                        item.Dispose();
                    }
                    _registry.ItemsInLock.Clear();
                    _registry.KeysInLock.Clear();

                    _registry.OverlayKeysInLock.Clear();
                    var overlayItems = _registry.RenderItemsInLock;
                    while (overlayItems.Count > 0)
                    {
                        overlayItems[0].Dispose();
                        overlayItems.RemoveAt(0);
                    }
                    _surfaceSnapshot.Clear();
                    _handleSnapshot.Clear();
                    _overlaySnapshot.Clear();
                });

                ModernOverlayRenderer.Release(gl);
            }
        }
    }
}
