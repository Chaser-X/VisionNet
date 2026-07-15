using System;
using ScottPlot;
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
        }

        /// <summary>
        /// Sets axis limits to display the full image with Y=0 at the top (image pixel convention).
        /// Y limits are inverted: bottom=imageHeight, top=0 → Y=0 appears at screen top.
        /// </summary>
        internal void FitToImage()
        {
            if (_imageWidth <= 0 || _imageHeight <= 0) return;

            double margin = Math.Max(_imageWidth, _imageHeight) * 0.02;
            _formsPlot.Plot.Axes.SetLimits(
                left:   -margin,
                right:  _imageWidth  + margin,
                bottom: _imageHeight + margin,
                top:    -margin);

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
}
