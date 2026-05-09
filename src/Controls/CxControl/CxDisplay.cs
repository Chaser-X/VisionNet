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
    public partial class CxDisplay : OpenGLControl, IDisposable
    {
        private CxAdvancedTrackBallCamera camera;
        private bool isMouseDown = false;

        private readonly Dictionary<ICxObjRenderItem, GLResourceHandle> _resourcePool
            = new Dictionary<ICxObjRenderItem, GLResourceHandle>();
        private readonly object _resourceLock = new object();

        private readonly ConcurrentQueue<GLResourceHandle> _pendingRelease
            = new ConcurrentQueue<GLResourceHandle>();

        private readonly List<ICxObjRenderItem> _surfaceItems = new List<ICxObjRenderItem>();
        private CxCoordinateSystemItem coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem colorBarItem = new CxColorBarItem();
        private CxCoordinationTagItem coorTagItem = new CxCoordinationTagItem();
        private readonly List<IRenderItem> renderItem = new List<IRenderItem>();

        public CxAdvancedTrackBallCamera Camera => camera;

        public ViewMode SurfaceViewMode
        {
            get => camera.ViewMode;
            set
            {
                if (camera.ViewMode != value)
                {
                    camera.ViewMode = value;
                    updataMenuItem();
                }
            }
        }

        private SurfaceMode pSufaceMode = SurfaceMode.PointCloud;
        public SurfaceMode SurfaceMode
        {
            get => pSufaceMode;
            set
            {
                if (pSufaceMode != value) { pSufaceMode = value; updataMenuItem(); }
                List<ICxObjRenderItem> snapshot;
                lock (_resourceLock) snapshot = new List<ICxObjRenderItem>(_surfaceItems);
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceMode = value;
            }
        }

        private SurfaceColorMode pSurfaceColorMode = SurfaceColorMode.ColorWithIntensity;
        public SurfaceColorMode SurfaceColorMode
        {
            get => pSurfaceColorMode;
            set
            {
                if (pSurfaceColorMode != value) { pSurfaceColorMode = value; updataMenuItem(); }
                List<ICxObjRenderItem> snapshot;
                lock (_resourceLock) snapshot = new List<ICxObjRenderItem>(_surfaceItems);
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceColorMode = value;
            }
        }

        public bool ShowCoordinateSystem { get; set; } = false;

        public CxDisplay() : this(ViewMode.Top, SurfaceMode.PointCloud, SurfaceColorMode.ColorWithIntensity) { }

        public CxDisplay(ViewMode viewMode = ViewMode.Top,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.ColorWithIntensity)
        {
            if (!DesignMode)
            {
                InitializeComponent();
                camera = new CxAdvancedTrackBallCamera(this);
                camera.ViewMode = viewMode;
                SurfaceMode = surfaceMode;
                SurfaceColorMode = surfaceColorMode;
                updataMenuItem();
            }
        }

        private void updataMenuItem()
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => updataMenuItem())); return; }

            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == camera.ViewMode.ToString();
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == pSufaceMode.ToString();
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == pSurfaceColorMode.ToString();
        }
    }
}
