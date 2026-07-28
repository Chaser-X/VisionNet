using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScottPlot;
using ScottPlot.Colormaps;
using SkiaSharp;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a <see cref="CxImage"/> as a ScottPlot <c>ImageRect</c> plottable.
    /// This item is non-interactive; it always occupies the background layer (index 0).
    /// </summary>
    public class CxImageItem : Abstract2DImageRenderItem
    {
        private ScottPlot.Plottables.ImageRect _plottable;
        private ScottPlot.Plot _plot;

        // ── I2DRenderItem ─────────────────────────────────────────────────────────

        /// <summary>Sets the image to render.</summary>
        public override void SetImage(CxImage image)
        {
            if (image == null || image.Data == null) return;
            base.SetImage(image);

            //_imageData = image.Channel == 1 ? image.Data : null;

            var displayImage = image;
            if (displayImage.Width > 2048 || displayImage.Height > 2048)
                displayImage = VisionOperator.ResizeImage(displayImage, 2048, 2048);

            var scottImage = BuildScottImage(displayImage);

            if (_plot != null)
            {
                if (_plottable != null) _plot.PlottableList.Remove(_plottable);
                _plottable = _plot.Add.ImageRect(scottImage, new CoordinateRect(0, orignalImage.Width, orignalImage.Height, 0));
                _plot.PlottableList.Remove(_plottable);
                _plot.PlottableList.Insert(0, _plottable);
            }
            else
            {
                _pendingImage = scottImage;
                _plottable = null;
            }
        }

        private ScottPlot.Image _pendingImage;

        /// <inheritdoc/>
        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            if (_pendingImage != null)
            {
                _plottable = plot.Add.ImageRect(_pendingImage, new CoordinateRect(0, orignalImage.Width, orignalImage.Height, 0));
                _pendingImage = null;
                plot.PlottableList.Remove(_plottable);
                plot.PlottableList.Insert(0, _plottable);
            }
        }

        /// <inheritdoc/>
        public override void RemoveFromPlot(Plot plot)
        {
            if (_plottable != null) { plot.PlottableList.Remove(_plottable); _plottable = null; }
            _plot = null;
        }

        /// <inheritdoc/>
        public override void UpdatePlottable() { }

        /// <inheritdoc/>
        public override void Dispose()
        {
            base.Dispose();
            _pendingImage = null;
            _plottable = null;
            _plot = null;
        }

        /// <summary>Repositions the image plottable to the given world-space rectangle.</summary>
        public override void UpdateWorldRect(CxBox2D rect)
        {
            if (_plottable != null)
                _plottable.Rect = new ScottPlot.CoordinateRect(rect.Left, rect.Right, rect.Bottom, rect.Top);
        }
    }
}
