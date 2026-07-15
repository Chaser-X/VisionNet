using System;
using ScottPlot;

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
