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

            // Restore ScottPlot defaults (includes "Open in New Window"), then customise.
            //_formsPlot.Menu.Reset();
            _formsPlot.Menu = new CxDisplay2DMenu(this, _formsPlot);

            // Default: enforce 1:1 aspect ratio
            _formsPlot.Plot.Axes.SquareUnits();

            //var strip = _formsPlot.ContextMenuStrip;
            //if (strip != null && strip.Items.Count >= 2)
            //{
            //    // Remove "Copy Image" (index 1) first to avoid index shift, then "Save Image" (index 0).
            //    strip.Items.RemoveAt(1);
            //    strip.Items.RemoveAt(0);

            //    var fitItem = new ToolStripMenuItem("Fit Image");
            //    fitItem.Click += (s, e) => FitImage1to1();
            //    strip.Items.Insert(0, fitItem);
            //}
            //else
            //{
            //    // Fallback: IPlotMenu API only.
            //    //menu.Clear();
            //    //menu.Add("Fit Image", _ => FitImage1to1());
            //    //menu.Add("")
            //}
        }

        /// <summary>
        /// Fits the image to the window and enforces a 1:1 X/Y aspect ratio so image pixels appear as squares.
        /// </summary>
        internal void FitImage1to1()
        {
            FitToImage();
            _formsPlot.Plot.Axes.SquareUnits();
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

            // Y limits are inverted (bottom > top) to match image convention: Y=0 at screen top.
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
        internal void RefreshDisplay()
        {
            if (_formsPlot.InvokeRequired)
                _formsPlot.Invoke(new Action(_formsPlot.Refresh));
            else
                _formsPlot.Refresh();
        }
    }
    /// <summary>
    /// 替换 ScottPlot 默认右键菜单。
    /// </summary>
    sealed class CxDisplay2DMenu : ScottPlot.IPlotMenu
    {
        private readonly ScottPlot.WinForms.FormsPlot _fp;
        private readonly CxDisplay2D _form;
        public CxDisplay2DMenu(CxDisplay2D parent, ScottPlot.WinForms.FormsPlot fp)
        {
            _form = parent;
            _fp = fp;
        }

        // 接口的其余成员（不需要额外实现，留空即可）
        public void Reset() { }
        public void Clear() { }
        public void Add(string label, System.Action<ScottPlot.Plot> action) { }
        public void AddSeparator() { }

        public void ShowContextMenu(ScottPlot.Pixel position)
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            // 1:1 自适应
            var itemFit = new System.Windows.Forms.ToolStripMenuItem("Autoscale");
            itemFit.Click += (s, e) => { _form.FitImage1to1(); _fp.Refresh(); };
            menu.Items.Add(itemFit);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            // 保存图像
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
            //open in new window
            var itemOpen = new System.Windows.Forms.ToolStripMenuItem("Open in New Window");
            itemOpen.Click += (s, e) =>
            {
                FormsPlotViewer.Launch(_fp.Plot, _form.Name);
            };
            menu.Items.Add(itemOpen);
            menu.Show(_fp, new System.Drawing.Point((int)position.X, (int)position.Y));
        }
    }
}
