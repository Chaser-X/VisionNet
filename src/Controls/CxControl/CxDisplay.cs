using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
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

        // ── GL resource pool (all GL objects owned here; items hold only CPU data) ─
        private readonly Dictionary<ICxObjRenderItem, GLResourceHandle> _resourcePool
            = new Dictionary<ICxObjRenderItem, GLResourceHandle>();
        private readonly object _resourceLock = new object();

        // Resources dequeued by Dispose/Replace that must be freed inside the GL thread.
        private readonly ConcurrentQueue<GLResourceHandle> _pendingRelease
            = new ConcurrentQueue<GLResourceHandle>();

        // ── Render item lists ────────────────────────────────────────────────────
        private readonly List<ICxObjRenderItem> _surfaceItems = new List<ICxObjRenderItem>();
        private readonly List<IRenderItem>      _renderItems  = new List<IRenderItem>();

        // ── HUD overlay objects ──────────────────────────────────────────────────
        private CxCoordinateSystemItem _coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem         _colorBarItem     = new CxColorBarItem();
        private CxCoordinationTagItem  _coordTagItem     = new CxCoordinationTagItem();

        // ── Mode backing fields ──────────────────────────────────────────────────
        private SurfaceMode      _surfaceMode      = SurfaceMode.PointCloud;
        private SurfaceColorMode _surfaceColorMode = SurfaceColorMode.ColorWithIntensity;

        // ── Public properties ────────────────────────────────────────────────────

        /// <summary>Gets the trackball camera controlling the 3D view.</summary>
        public CxAdvancedTrackBallCamera Camera => _camera;

        /// <summary>Gets or sets the current view mode (Top, Front, Left, etc.).</summary>
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
                List<ICxObjRenderItem> snapshot;
                lock (_resourceLock) snapshot = new List<ICxObjRenderItem>(_surfaceItems);
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceMode = value;
            }
        }

        /// <summary>Gets or sets the surface color mode (Color, Intensity, or ColorWithIntensity).</summary>
        public SurfaceColorMode SurfaceColorMode
        {
            get => _surfaceColorMode;
            set
            {
                if (_surfaceColorMode != value) { _surfaceColorMode = value; UpdateMenuItems(); }
                List<ICxObjRenderItem> snapshot;
                lock (_resourceLock) snapshot = new List<ICxObjRenderItem>(_surfaceItems);
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceColorMode = value;
            }
        }

        /// <summary>Gets or sets whether the 3D coordinate system axes are shown in world space.</summary>
        public bool ShowCoordinateSystem { get; set; }

        /// <summary>
        /// Gets or sets whether the coordinate system is left-handed.
        /// Delegates to <see cref="CxAdvancedTrackBallCamera.IsLeftHanded"/>.
        /// </summary>
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
            if (!DesignMode)
            {
                InitializeComponent();
                _camera = new CxAdvancedTrackBallCamera(this);
                _camera.ViewMode = viewMode;
                SurfaceMode      = surfaceMode;
                SurfaceColorMode = surfaceColorMode;
                UpdateMenuItems();
            }
        }

        // ── Shared private helpers ───────────────────────────────────────────────

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
        /// Computes the axis-aligned bounding box that encloses all active surface items.
        /// Returns <c>null</c> when no items are loaded or none have a valid bounding box.
        /// Caller must hold <see cref="_resourceLock"/>.
        /// </summary>
        private Box3D? GetCombinedBoundingBox()
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var item in _surfaceItems)
            {
                var bb = item.BoundingBox;
                if (bb == null) continue;
                float x0 = bb.Value.Center.X - bb.Value.Size.Width  / 2f;
                float x1 = bb.Value.Center.X + bb.Value.Size.Width  / 2f;
                float y0 = bb.Value.Center.Y - bb.Value.Size.Height / 2f;
                float y1 = bb.Value.Center.Y + bb.Value.Size.Height / 2f;
                float z0 = bb.Value.Center.Z - bb.Value.Size.Depth  / 2f;
                float z1 = bb.Value.Center.Z + bb.Value.Size.Depth  / 2f;
                if (x0 < minX) minX = x0; if (x1 > maxX) maxX = x1;
                if (y0 < minY) minY = y0; if (y1 > maxY) maxY = y1;
                if (z0 < minZ) minZ = z0; if (z1 > maxZ) maxZ = z1;
            }

            if (minX == float.MaxValue) return null;
            return new Box3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        /// <summary>
        /// Releases all GL resources, surface items, and overlay items held by this control.
        /// Called by <see cref="Dispose(bool)"/> in Designer.cs.
        /// </summary>
        private void DisposeResources(OpenGL gl)
        {
            if (gl != null)
                while (_pendingRelease.TryDequeue(out var p)) ReleaseGLResources(gl, p);

            lock (_resourceLock)
            {
                foreach (var item in _surfaceItems)
                {
                    if (_resourcePool.TryGetValue(item, out var h))
                    {
                        if (gl != null) ReleaseGLResources(gl, h);
                        _resourcePool.Remove(item);
                    }
                    item.OnRenderDataChanged -= OnItemRenderDataChanged;
                    item.Dispose();
                }
                _surfaceItems.Clear();
            }

            while (_renderItems.Count > 0)
            {
                _renderItems[0].Dispose();
                _renderItems.RemoveAt(0);
            }
        }
    }
}
