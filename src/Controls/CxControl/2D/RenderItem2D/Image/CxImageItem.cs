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
    public class CxImageItem : I2DRenderItem
    {
        private ScottPlot.Plottables.ImageRect _plottable;
        private ScottPlot.Plot _plot;

        private int _width;
        private int _height;

        // Cached single-channel data for Z-coordinate query (zero-copy reference)
        private Array _imageData;
        private PlainType _imageType;

        Color I2DRenderItem.Color { get => Color.White; set { } }
        float I2DRenderItem.Size { get => 1f; set { } }

        /// <summary>Gets the image width in pixels.</summary>
        public int Width => _width;

        /// <summary>Gets the image height in pixels.</summary>
        public int Height => _height;

        // ── I2DRenderItem ─────────────────────────────────────────────────────────

        /// <summary>Sets the image to render.</summary>
        public void SetImage(CxImage image)
        {
            if (image == null || image.Data == null) return;

            _width = image.Width;
            _height = image.Height;

            _imageData = image.Channel == 1 ? image.Data : null;
            _imageType = image.Type;

            var displayImage = image;
            if (displayImage.Width > 2048 || displayImage.Height > 2048)
                displayImage = VisionOperator.ResizeImage(displayImage, 2048, 2048);

            var scottImage = BuildScottImage(displayImage);

            if (_plot != null)
            {
                if (_plottable != null) _plot.PlottableList.Remove(_plottable);
                _plottable = _plot.Add.ImageRect(scottImage, new CoordinateRect(0, _width, _height, 0));
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
        public void AddToPlot(Plot plot)
        {
            _plot = plot;
            if (_pendingImage != null)
            {
                _plottable = plot.Add.ImageRect(_pendingImage, new CoordinateRect(0, _width, _height, 0));
                _pendingImage = null;
                plot.PlottableList.Remove(_plottable);
                plot.PlottableList.Insert(0, _plottable);
            }
        }

        /// <inheritdoc/>
        public void RemoveFromPlot(Plot plot)
        {
            if (_plottable != null) { plot.PlottableList.Remove(_plottable); _plottable = null; }
            _plot = null;
        }

        /// <inheritdoc/>
        public void UpdatePlottable() { }

        /// <inheritdoc/>
        public void Dispose()
        {
            _pendingImage = null;
            _plottable = null;
            _plot = null;
            _imageData = null;
        }

        /// <summary>Repositions the image plottable to the given world-space rectangle.</summary>
        public void UpdateWorldRect(CxBox2D rect)
        {
            if (_plottable != null)
                _plottable.Rect = new ScottPlot.CoordinateRect(rect.Left, rect.Right, rect.Bottom, rect.Top);
        }

        /// <summary>Returns the raw pixel value at image coordinate (x, y) as float, or null if out of range.</summary>
        public float? GetPixelFloat(int x, int y)
        {
            if (_imageData == null || x < 0 || x >= _width || y < 0 || y >= _height)
                return null;
            int idx = y * _width + x;
            var item = _imageData.GetValue(idx);
            switch (_imageType)
            {
                case PlainType.UInt8: return (byte)item;
                case PlainType.Int16: return (short)item;
                case PlainType.Int32: return (int)item;
                case PlainType.Real: return (float)item;
            }
            return null;
        }

        // ── Image conversion (no BitConverter — direct typed array casts) ─────────
        private static unsafe ScottPlot.Image BuildScottImage(CxImage image)
        {
            int w = image.Width;
            int h = image.Height;
            int ch = image.Channel;
            var bmp = image.ToBitmap();
            try
            {
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return new ScottPlot.Image(ms.ToArray());
                }
            }
            finally
            {
                bmp.Dispose();
            }
        }
    }
}
