using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ScottPlot.WinForms;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// 2D display control for rendering <see cref="CxImage{T}"/> data and 2D geometric overlays.
    /// Uses ScottPlot as the rendering backend. Analogous to <see cref="CxDisplay"/> for 3D content.
    /// </summary>
    /// <remarks>
    /// <para>The plot coordinate system follows image convention: X increases right, Y increases downward, origin at top-left.</para>
    /// <para>Zoom and pan are handled natively by ScottPlot (scroll to zoom, right-drag to pan).</para>
    /// </remarks>
    public partial class CxDisplay2D : UserControl
    {
        // ── Coordinate scale and offset (default: identity transform) ────────────
        public float XScale  { get; set; } = 1f;
        public float YScale  { get; set; } = 1f;
        public float ZScale  { get; set; } = 1f;
        public float XOffset { get; set; } = 0f;
        public float YOffset { get; set; } = 0f;
        public float ZOffset { get; set; } = 0f;

        // ── Click-position world-coordinate annotation (lazy init) ────────────────
        private ScottPlot.Plottables.Text _coordAnnotation;

        // ── Core rendering widget ─────────────────────────────────────────────────
        internal readonly FormsPlot _formsPlot;

        // ── Image layer ───────────────────────────────────────────────────────────
        private CxImageItem _imageItem;
        private int _imageWidth;
        private int _imageHeight;

        // ── Overlay items ─────────────────────────────────────────────────────────
        private readonly List<Abstract2DRenderItem> _overlayItems = new List<Abstract2DRenderItem>();

        // ── Selection/drag state ──────────────────────────────────────────────────
        private Abstract2DRenderItem _selectedItem;
        private bool                 _isDragging;
        private CxPoint2D            _lastDragPos;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised whenever the mouse moves over the plot area.
        /// Provides the plot-space coordinate (image pixel coordinates).
        /// </summary>
        public event Action<CxPoint2D> CoordinatesChanged;

        /// <summary>Raised when an overlay item is selected or deselected.</summary>
        public event Action<Abstract2DRenderItem> SelectionChanged;

        // ── Constructor ───────────────────────────────────────────────────────────

        /// <summary>Initializes a new <see cref="CxDisplay2D"/> with default settings.</summary>
        public CxDisplay2D()
        {
            InitializeComponent();

            ScottPlot.Fonts.Default = "Microsoft YaHei";

            _formsPlot = new FormsPlot { Dock = DockStyle.Fill };
            Controls.Add(_formsPlot);
            _formsPlot.BringToFront();

            ConfigureDefaultPlot();
            WireMouseEvents();
        }

        // ── Internal accessors ────────────────────────────────────────────────────

        /// <summary>Gets the underlying ScottPlot plot object for advanced configuration.</summary>
        public ScottPlot.Plot Plot => _formsPlot.Plot;

        /// <summary>Gets the currently selected overlay item, or <c>null</c> if none.</summary>
        public Abstract2DRenderItem SelectedItem => _selectedItem;

        /// <summary>Gets the width of the currently displayed image in pixels, or 0 if no image is set.</summary>
        public int ImageWidth => _imageWidth;

        /// <summary>Gets the height of the currently displayed image in pixels, or 0 if no image is set.</summary>
        public int ImageHeight => _imageHeight;
    }
}
