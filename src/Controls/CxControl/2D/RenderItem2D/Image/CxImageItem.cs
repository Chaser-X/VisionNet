using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScottPlot;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a <see cref="CxImage{T}"/> as a ScottPlot <c>ImageRect</c> plottable.
    /// Supports byte, float, short, and ushort pixel types with optional per-pixel colour mapping.
    /// This item is non-interactive; it always occupies the background layer (index 0).
    /// </summary>
    public class CxImageItem : I2DRenderItem
    {
        private ScottPlot.Plottables.ImageRect _plottable;
        private ScottPlot.Plot _plot;

        private int _width;
        private int _height;

        Color I2DRenderItem.Color { get => Color.White; set { } }
        float I2DRenderItem.Size  { get => 1f;          set { } }

        /// <summary>Gets the image width in pixels.</summary>
        public int Width  => _width;

        /// <summary>Gets the image height in pixels.</summary>
        public int Height => _height;

        /// <summary>
        /// Replaces the displayed image with a new <see cref="CxImage{T}"/>.
        /// </summary>
        /// <typeparam name="T">Pixel element type (byte, float, short, ushort).</typeparam>
        /// <param name="image">Source image.</param>
        /// <param name="colorMap">
        /// Optional per-pixel colour mapping. When <c>null</c>, the pixel is rendered as grayscale
        /// using built-in normalisation (byte: direct; float/double: [0,1]→[0,255]; short/ushort: full-range).
        /// </param>
        public void SetImage<T>(CxImage<T> image, Func<T, Color> colorMap = null)
        {
            if (image == null || image.Data == null) return;

            _width  = image.Width;
            _height = image.Height;

            var scottImage = BuildScottImage(image, colorMap);

            if (_plot != null)
            {
                // Replace existing plottable
                if (_plottable != null)
                    _plot.PlottableList.Remove(_plottable);

                _plottable = _plot.Add.ImageRect(scottImage,
                    new CoordinateRect(0, _width, 0, _height));
                _plot.PlottableList.Remove(_plottable);
                _plot.PlottableList.Insert(0, _plottable);
            }
            else
            {
                // Deferred: stored until AddToPlot is called
                _pendingImage = scottImage;
                _plottable    = null;
            }
        }

        private ScottPlot.Image _pendingImage;

        /// <inheritdoc/>
        public void AddToPlot(Plot plot)
        {
            _plot = plot;

            if (_pendingImage != null)
            {
                _plottable = plot.Add.ImageRect(_pendingImage,
                    new CoordinateRect(0, _width, 0, _height));
                _pendingImage = null;

                // Ensure image stays at index 0 (background)
                plot.PlottableList.Remove(_plottable);
                plot.PlottableList.Insert(0, _plottable);
            }
        }

        /// <inheritdoc/>
        public void RemoveFromPlot(Plot plot)
        {
            if (_plottable != null)
            {
                plot.PlottableList.Remove(_plottable);
                _plottable = null;
            }
            _plot = null;
        }

        /// <inheritdoc/>
        public void UpdatePlottable() { }

        /// <inheritdoc/>
        public void Dispose()
        {
            _pendingImage = null;
            _plottable    = null;
            _plot         = null;
        }

        // ── Image conversion ─────────────────────────────────────────────────────

        private static unsafe ScottPlot.Image BuildScottImage<T>(CxImage<T> image, Func<T, Color> colorMap)
        {
            int w = image.Width;
            int h = image.Height;

            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            try
            {
                var lockRect = new Rectangle(0, 0, w, h);
                var bd = bmp.LockBits(lockRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                byte* ptr = (byte*)bd.Scan0.ToPointer();

                if (colorMap != null)
                {
                    for (int i = 0; i < w * h; i++)
                    {
                        Color c = colorMap(image.Data[i]);
                        ptr[i * 4 + 0] = c.B;
                        ptr[i * 4 + 1] = c.G;
                        ptr[i * 4 + 2] = c.R;
                        ptr[i * 4 + 3] = c.A;
                    }
                }
                else
                {
                    for (int i = 0; i < w * h; i++)
                    {
                        byte g = DefaultToGray(image.Data[i]);
                        ptr[i * 4 + 0] = g;
                        ptr[i * 4 + 1] = g;
                        ptr[i * 4 + 2] = g;
                        ptr[i * 4 + 3] = 255;
                    }
                }

                bmp.UnlockBits(bd);

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

        private static byte DefaultToGray<T>(T value)
        {
            if (typeof(T) == typeof(byte))   return (byte)(object)value;
            if (typeof(T) == typeof(float))  { float  f = (float)(object)value;  return (byte)Math.Max(0, Math.Min(255, (int)(f * 255))); }
            if (typeof(T) == typeof(double)) { double d = (double)(object)value; return (byte)Math.Max(0, Math.Min(255, (int)(d * 255))); }
            if (typeof(T) == typeof(short))
            {
                short s = (short)(object)value;
                return (byte)((s - (long)short.MinValue) * 255L / (short.MaxValue - (long)short.MinValue));
            }
            if (typeof(T) == typeof(ushort))
            {
                ushort us = (ushort)(object)value;
                return (byte)(us * 255 / ushort.MaxValue);
            }
            if (typeof(T) == typeof(int))
            {
                int ii = (int)(object)value;
                return (byte)(((long)ii - int.MinValue) * 255L / ((long)int.MaxValue - int.MinValue));
            }
            return 128;
        }
    }
}
