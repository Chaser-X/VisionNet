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
        private CxAdvancedTrackBallCamera _camera;
        private bool _isMouseDown;

        // GL resource pool: CxDisplay owns all GL objects; items hold only CPU data.
        private readonly Dictionary<ICxObjRenderItem, GLResourceHandle> _resourcePool
            = new Dictionary<ICxObjRenderItem, GLResourceHandle>();
        private readonly object _resourceLock = new object();

        // GL resources waiting to be released inside the next render frame (GL-context thread).
        private readonly ConcurrentQueue<GLResourceHandle> _pendingRelease
            = new ConcurrentQueue<GLResourceHandle>();

        private readonly List<ICxObjRenderItem> _surfaceItems = new List<ICxObjRenderItem>();
        private CxCoordinateSystemItem _coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem _colorBarItem = new CxColorBarItem();
        private CxCoordinationTagItem _coordTagItem = new CxCoordinationTagItem();
        private readonly List<IRenderItem> _renderItems = new List<IRenderItem>();

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

        private SurfaceMode _surfaceMode = SurfaceMode.PointCloud;

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

        private SurfaceColorMode _surfaceColorMode = SurfaceColorMode.ColorWithIntensity;

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

        /// <summary>Gets or sets whether the 3D coordinate system axes are shown.</summary>
        public bool ShowCoordinateSystem { get; set; }

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
                SurfaceMode = surfaceMode;
                SurfaceColorMode = surfaceColorMode;
                UpdateMenuItems();
            }
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
    }
}
