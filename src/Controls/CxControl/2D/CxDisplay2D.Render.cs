using System;
using System.Windows.Forms;
using ScottPlot;
using ScottPlot.WinForms;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Render helpers: plot configuration, image fitting, and display refresh.</summary>
    public partial class CxDisplay2D
    {
        private void ConfigureDefaultPlot()
        {
            var plot = _formsPlot.Plot;
            plot.Legend.IsVisible = false;
            plot.Grid.IsVisible   = false;

            _formsPlot.Menu = new CxDisplay2DMenu(this, _formsPlot);

            _formsPlot.UserInputProcessor.DoubleLeftClickBenchmark(false);

            // Create and register the 1:1+inverted-Y rule by default.
            _squareRule = new SquareWithInvertedY(plot.Axes.Bottom, plot.Axes.Left);
            plot.Axes.Rules.Add(_squareRule);
        }

        /// <summary>
        /// Fits the image to the window with a 1:1 X/Y aspect ratio and Y=0 at the screen top.
        /// Only sets the initial axis limits; the persistent <see cref="SquareWithInvertedY"/> rule
        /// maintains the 1:1 ratio on subsequent pan/zoom operations.
        /// </summary>
        internal void FitImage1to1()
        {
            if (_imageWidth <= 0 || _imageHeight <= 0) { FitToImage(); return; }

            var box    = GetImageWorldRect();
            double imgW   = Math.Abs(box.Size.Width);
            double imgH   = Math.Abs(box.Size.Height);
            double margin = Math.Max(imgW, imgH) * 0.02;

            int pxW = _formsPlot.Width  > 0 ? _formsPlot.Width  : 800;
            int pxH = _formsPlot.Height > 0 ? _formsPlot.Height : 600;

            double scale = Math.Max((imgW + 2 * margin) / pxW, (imgH + 2 * margin) / pxH);
            double halfX = scale * pxW / 2;
            double halfY = scale * pxH / 2;
            double cx    = box.Center.X;
            double cy    = box.Center.Y;

            // Inverted Y (bottom > top) keeps Y=0 at the screen top (image pixel convention).
            _formsPlot.Plot.Axes.SetLimits(
                left:   cx - halfX,
                right:  cx + halfX,
                bottom: cy + halfY,   // larger Y value → screen bottom
                top:    cy - halfY);  // smaller Y value → screen top

            RefreshDisplay();
        }

        /// <summary>
        /// Sets axis limits to display the full image with Y=0 at the top (image pixel convention).
        /// Y limits are inverted: bottom=imageHeight, top=0 → Y=0 appears at screen top.
        /// </summary>
        internal void FitToImage()
        {
            if (_imageWidth <= 0 || _imageHeight <= 0) return;

            var box    = GetImageWorldRect();
            double margin = Math.Max(Math.Abs(box.Size.Width), Math.Abs(box.Size.Height)) * 0.02;

            _formsPlot.Plot.Axes.SetLimits(
                left:   box.Left   - margin,
                right:  box.Right  + margin,
                bottom: box.Bottom + margin,
                top:    box.Top    - margin);

            RefreshDisplay();
        }

        /// <summary>
        /// Displays a world-coordinate annotation at the given plot position.
        /// Recreates the plottable on each call so it is always rendered on top.
        /// </summary>
        internal void ShowCoordAnnotation(CxPoint2D plotPos, float wx, float wy, float? wz)
        {
            if (_coordAnnotation != null)
                _formsPlot.Plot.PlottableList.Remove(_coordAnnotation);

            string text = wz.HasValue
                ? $"X: {wx:F3}\nY: {wy:F3}\nZ: {wz.Value:F3}"
                : $"X: {wx:F3}\nY: {wy:F3}";

            _coordAnnotation = _formsPlot.Plot.Add.Text(text, plotPos.X, plotPos.Y);
            _coordAnnotation.LabelStyle.ForeColor  = new ScottPlot.Color(255, 255, 50, 230);
            _coordAnnotation.LabelStyle.Bold        = true;
            _coordAnnotation.LabelStyle.FontSize    = 11;
            _coordAnnotation.LabelBackgroundColor   = new ScottPlot.Color(0, 0, 0, 160);
        }

        /// <summary>Removes the world-coordinate annotation from the plot.</summary>
        internal void HideCoordAnnotation()
        {
            if (_coordAnnotation != null)
            {
                _formsPlot.Plot.PlottableList.Remove(_coordAnnotation);
                _coordAnnotation = null;
                RefreshDisplay();
            }
        }

        /// <summary>Forces an immediate redraw of the plot.</summary>
        public void RefreshDisplay()
        {
            if (_formsPlot.InvokeRequired)
                _formsPlot.Invoke(new Action(_formsPlot.Refresh));
            else
                _formsPlot.Refresh();
        }
    }

    /// <summary>
    /// Axis rule that enforces a 1:1 X/Y scale while preserving Y-axis inversion
    /// (Range.Min &gt; Range.Max → Y=0 at screen top, image pixel convention).
    /// Unlike ScottPlot's built-in SquareZoomOut, this rule does not normalise the Y range direction.
    /// </summary>
    sealed class SquareWithInvertedY : ScottPlot.IAxisRule
    {
        private readonly ScottPlot.IXAxis _x;
        private readonly ScottPlot.IYAxis _y;

        public SquareWithInvertedY(ScottPlot.IXAxis x, ScottPlot.IYAxis y)
        {
            _x = x; _y = y;
        }

        public void Apply(ScottPlot.RenderPack rp, bool beforeLayout)
        {
            double pxW = rp.DataRect.Width;
            double pxH = rp.DataRect.Height;
            if (pxW <= 0 || pxH <= 0) return;

            double xMin = _x.Min; double xMax = _x.Max;
            double yMin = _y.Min; double yMax = _y.Max;   // may be yMin > yMax (inverted Y)

            double xSpan = Math.Abs(xMax - xMin);
            double ySpan = Math.Abs(yMax - yMin);
            if (xSpan == 0 || ySpan == 0) return;

            double xScale = xSpan / pxW;
            double yScale = ySpan / pxH;

            // Already 1:1 within 0.01% tolerance — skip to avoid jitter.
            if (Math.Abs(xScale - yScale) / Math.Max(xScale, yScale) < 0.0001) return;

            // Zoom-out approach: expand the narrow axis to match the wider scale.
            double s  = Math.Max(xScale, yScale);
            double cx = (xMin + xMax) / 2;
            double cy = (yMin + yMax) / 2;
            double hx = s * pxW / 2;
            double hy = s * pxH / 2;

            _x.Range.Set(cx - hx, cx + hx);

            // Preserve Y inversion: if stored as Min > Max (inverted), keep it that way.
            if (yMin > yMax)
                _y.Range.Set(cy + hy, cy - hy);   // Min > Max → Y=0 stays at screen top
            else
                _y.Range.Set(cy - hy, cy + hy);
        }
    }

    /// <summary>Custom context menu replacing ScottPlot's default.</summary>
    sealed class CxDisplay2DMenu : ScottPlot.IPlotMenu
    {
        private readonly ScottPlot.WinForms.FormsPlot _fp;
        private readonly CxDisplay2D _form;
        public CxDisplay2DMenu(CxDisplay2D parent, ScottPlot.WinForms.FormsPlot fp)
        {
            _form = parent;
            _fp = fp;
        }

        public void Reset() { }
        public void Clear() { }
        public void Add(string label, System.Action<ScottPlot.Plot> action) { }
        public void AddSeparator() { }

        public void ShowContextMenu(ScottPlot.Pixel position)
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();

            var itemFit = new System.Windows.Forms.ToolStripMenuItem("Autoscale");
            itemFit.Click += (s, e) => { _form.FitImage1to1(); _fp.Refresh(); };
            menu.Items.Add(itemFit);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var modeItem = new System.Windows.Forms.ToolStripMenuItem("自动适配") { Checked = _form.Mode == CxDisplay2D.DisplayMode.Normal };
            modeItem.Click += (s, e) =>
            {
                _form.Mode = _form.Mode == CxDisplay2D.DisplayMode.Normal
                    ? CxDisplay2D.DisplayMode.None
                    : CxDisplay2D.DisplayMode.Normal;
            };
            menu.Items.Add(modeItem);

            var itemSave = new System.Windows.Forms.ToolStripMenuItem("Save Image");
            itemSave.Click += (s, e) =>
            {
                using (var dlg = new System.Windows.Forms.SaveFileDialog())
                {
                    dlg.Filter = "PNG 图像|*.png";
                    dlg.FileName = "image.png";
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        _fp.Plot.SavePng(dlg.FileName, _fp.Width, _fp.Height);
                }
            };
            menu.Items.Add(itemSave);

            var itemOpen = new System.Windows.Forms.ToolStripMenuItem("Open in New Window");
            itemOpen.Click += (s, e) => FormsPlotViewer.Launch(_fp.Plot, _form.Name);
            menu.Items.Add(itemOpen);

            menu.Show(_fp, new System.Drawing.Point((int)position.X, (int)position.Y));
        }
    }
}
