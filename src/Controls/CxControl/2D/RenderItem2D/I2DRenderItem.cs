using System;
using System.Drawing;
using ScottPlot;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>
    /// Minimal interface for 2D render items rendered via ScottPlot plottables.
    /// </summary>
    public interface I2DRenderItem : IDisposable
    {
        /// <summary>Gets or sets the drawing colour.</summary>
        Color Color { get; set; }

        /// <summary>Gets or sets the point or line size in pixels.</summary>
        float Size { get; set; }

        /// <summary>Adds this item's plottable(s) to the given plot.</summary>
        void AddToPlot(Plot plot);

        /// <summary>Removes this item's plottable(s) from the given plot.</summary>
        void RemoveFromPlot(Plot plot);

        /// <summary>Rebuilds plottable(s) to reflect current data or selection state.</summary>
        void UpdatePlottable();
    }

}
