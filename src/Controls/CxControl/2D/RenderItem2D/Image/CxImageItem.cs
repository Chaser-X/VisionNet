using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScottPlot;
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

        // Cached single-channel float values for Z-coordinate query
        private float[] _pixelFloats;
        private int _queryWidth;
        private int _queryHeight;

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

            _queryWidth = image.Width;
            _queryHeight = image.Height;
            _pixelFloats = image.Channel == 1 ? BuildPixelFloats(image) : null;

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
            _pixelFloats = null;
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
            if (_pixelFloats == null || x < 0 || x >= _queryWidth || y < 0 || y >= _queryHeight)
                return null;
            return _pixelFloats[y * _queryWidth + x];
        }

        // ── Image conversion (no BitConverter — direct typed array casts) ─────────

        private static unsafe ScottPlot.Image BuildScottImage(CxImage image)
        {
            int w = image.Width;
            int h = image.Height;
            int ch = image.Channel;

            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            try
            {
                var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                byte* ptr = (byte*)bd.Scan0.ToPointer();

                switch (image.Type)
                {
                    case PlainType.UInt8:
                        {
                            var data = (byte[])image.Data;
                            if (ch == 4)
                            {
                                // BGRA — direct copy, zero conversion
                                for (int i = 0; i < w * h; i++)
                                {
                                    ptr[i * 4] = data[i * 4];
                                    ptr[i * 4 + 1] = data[i * 4 + 1];
                                    ptr[i * 4 + 2] = data[i * 4 + 2];
                                    ptr[i * 4 + 3] = data[i * 4 + 3];
                                }
                            }
                            else if (ch == 3)
                            {
                                for (int i = 0; i < w * h; i++)
                                {
                                    ptr[i * 4] = data[i * 3];      // B
                                    ptr[i * 4 + 1] = data[i * 3 + 1];    // G
                                    ptr[i * 4 + 2] = data[i * 3 + 2];    // R
                                    ptr[i * 4 + 3] = 255;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < w * h; i++)
                                {
                                    byte g = data[i * ch];
                                    ptr[i * 4] = ptr[i * 4 + 1] = ptr[i * 4 + 2] = g;
                                    ptr[i * 4 + 3] = 255;
                                }
                            }
                            break;
                        }
                    case PlainType.Int16:
                        {
                            var data = (short[])image.Data;
                            for (int i = 0; i < w * h; i++)
                            {
                                byte g = NormalizeShort(data[i * ch]);
                                ptr[i * 4] = ptr[i * 4 + 1] = ptr[i * 4 + 2] = g;
                                ptr[i * 4 + 3] = 255;
                            }
                            break;
                        }
                    case PlainType.Int32:
                        {
                            var data = (int[])image.Data;
                            for (int i = 0; i < w * h; i++)
                            {
                                byte g = NormalizeInt32(data[i * ch]);
                                ptr[i * 4] = ptr[i * 4 + 1] = ptr[i * 4 + 2] = g;
                                ptr[i * 4 + 3] = 255;
                            }
                            break;
                        }
                    case PlainType.Real:
                        {
                            var data = (float[])image.Data;
                            for (int i = 0; i < w * h; i++)
                            {
                                byte g = NormalizeFloat(data[i * ch]);
                                ptr[i * 4] = ptr[i * 4 + 1] = ptr[i * 4 + 2] = g;
                                ptr[i * 4 + 3] = 255;
                            }
                            break;
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

        private static float[] BuildPixelFloats(CxImage image)
        {
            int n = image.Width * image.Height;
            var floats = new float[n];

            switch (image.Type)
            {
                case PlainType.UInt8:
                    { var d = (byte[])image.Data; for (int i = 0; i < n; i++) floats[i] = d[i]; break; }
                case PlainType.Int16:
                    { var d = (short[])image.Data; for (int i = 0; i < n; i++) floats[i] = d[i]; break; }
                case PlainType.Int32:
                    { var d = (int[])image.Data; for (int i = 0; i < n; i++) floats[i] = d[i]; break; }
                case PlainType.Real:
                    { var d = (float[])image.Data; for (int i = 0; i < n; i++) floats[i] = d[i]; break; }
            }

            return floats;
        }

        private static byte NormalizeShort(short v)
        {
            return (byte)((v - (long)short.MinValue) * 255L / (short.MaxValue - (long)short.MinValue));
        }

        private static byte NormalizeInt32(int v)
        {
            return (byte)(((long)v - int.MinValue) * 255L / (uint.MaxValue));
        }

        private static byte NormalizeFloat(float v)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)(v * 255)));
        }
    }
}
