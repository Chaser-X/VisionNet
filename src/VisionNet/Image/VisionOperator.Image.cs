using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using VisionNet.DataType;

namespace VisionNet
{
    public static partial class VisionOperator
    {
        /// <summary>
        /// Resizes an image to fit within the specified dimensions while preserving aspect ratio.
        /// Uses OpenCV linear interpolation. Returns the original image if already within bounds.
        /// </summary>
        /// <param name="image">Source image. Returns <c>null</c> if <c>null</c> or empty.</param>
        /// <param name="maxWidth">Maximum width in pixels.</param>
        /// <param name="maxHeight">Maximum height in pixels.</param>
        /// <returns>A new <see cref="CxImage"/> with the same pixel type and channel count.</returns>
        public static CxImage ResizeImage(CxImage image, int maxWidth, int maxHeight)
        {
            if (image == null || image.Data == null) return null;
            int w = image.Width, h = image.Height;
            if (w <= maxWidth && h <= maxHeight) return image;

            float scale = Math.Min((float)maxWidth / w, (float)maxHeight / h);
            int newW = Math.Max(1, (int)(w * scale));
            int newH = Math.Max(1, (int)(h * scale));
            int ch = image.Channel;

            MatType srcType;
            if (image.Type == PlainType.UInt8)
                srcType = ch == 1 ? MatType.CV_8UC1 : MatType.CV_8UC4;
            else if (image.Type == PlainType.Int16)
                srcType = ch == 1 ? MatType.CV_16SC1 : MatType.CV_16SC4;
            else if (image.Type == PlainType.Int32)
                srcType = ch == 1 ? MatType.CV_32SC1 : MatType.CV_32SC4;
            else
                srcType = ch == 1 ? MatType.CV_32FC1 : MatType.CV_32FC4;

            var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
            try
            {
                using (var src = new Mat(h, w, srcType, handle.AddrOfPinnedObject()))
                using (var dst = new Mat())
                {
                    Cv2.Resize(src, dst, new OpenCvSharp.Size(newW, newH),
                               0, 0, InterpolationFlags.Linear);

                    int total = newW * newH * ch;

                    if (image.Type == PlainType.UInt8)
                    {
                        var arr = new byte[total];
                        Marshal.Copy(dst.Data, arr, 0, total);
                        return new CxImage(newW, newH, arr, ch);
                    }
                    else if (image.Type == PlainType.Int16)
                    {
                        var arr = new short[total];
                        Marshal.Copy(dst.Data, arr, 0, total);
                        return new CxImage(newW, newH, arr, ch);
                    }
                    else if (image.Type == PlainType.Int32)
                    {
                        var arr = new int[total];
                        Marshal.Copy(dst.Data, arr, 0, total);
                        return new CxImage(newW, newH, arr, ch);
                    }
                    else
                    {
                        var arr = new float[total];
                        Marshal.Copy(dst.Data, arr, 0, total);
                        return new CxImage(newW, newH, arr, ch);
                    }
                }
            }
            finally { handle.Free(); }
        }

        /// <summary>
        /// Converts a <see cref="CxImage"/> to a <see cref="Bitmap"/> in Format32bppArgb.
        /// </summary>
        /// <param name="image">Source image. Returns <c>null</c> if <c>null</c> or empty.</param>
        /// <returns>A new <see cref="Bitmap"/>. The caller must dispose it.</returns>
        /// <remarks>
        /// <list type="bullet">
        ///   <item><b>UInt8 ch=4</b> — BGRA direct copy.</item>
        ///   <item><b>UInt8 ch=3</b> — BGR → BGRA, A=255.</item>
        ///   <item><b>UInt8 ch=1</b> — grayscale → replicated to B/G/R, A=255.</item>
        ///   <item><b>Int16/Int32/Real</b> — normalized to [0,255], grayscale output, A=255.</item>
        /// </list>
        /// </remarks>
        public static unsafe Bitmap ToBitmap(CxImage image)
        {
            if (image == null || image.Data == null) return null;

            int w = image.Width;
            int h = image.Height;
            int ch = image.Channel;

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte* ptr = (byte*)bd.Scan0.ToPointer();

                switch (image.Type)
                {
                    case PlainType.UInt8:
                        {
                            var data = (byte[])image.Data;
                            if (ch == 4)
                            {
                                for (int i = 0; i < w * h; i++)
                                {
                                    ptr[i * 4]     = data[i * 4];
                                    ptr[i * 4 + 1] = data[i * 4 + 1];
                                    ptr[i * 4 + 2] = data[i * 4 + 2];
                                    ptr[i * 4 + 3] = data[i * 4 + 3];
                                }
                            }
                            else if (ch == 3)
                            {
                                for (int i = 0; i < w * h; i++)
                                {
                                    ptr[i * 4]     = data[i * 3];
                                    ptr[i * 4 + 1] = data[i * 3 + 1];
                                    ptr[i * 4 + 2] = data[i * 3 + 2];
                                    ptr[i * 4 + 3] = 255;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < w * h; i++)
                                {
                                    byte g = data[i];
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
                                byte g = NormalizeShort(data[i]);
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
                                byte g = NormalizeInt32(data[i]);
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
                                byte g = NormalizeFloat(data[i]);
                                ptr[i * 4] = ptr[i * 4 + 1] = ptr[i * 4 + 2] = g;
                                ptr[i * 4 + 3] = 255;
                            }
                            break;
                        }
                }

                return bmp;
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
        }

        /// <summary>
        /// Creates a <see cref="CxImage"/> from a <see cref="Bitmap"/>, adapting the pixel type
        /// and channel count to the Bitmap's <see cref="PixelFormat"/>.
        /// </summary>
        /// <remarks>
        /// <list type="table">
        ///   <item><term>Format24bppRgb</term><description>UInt8, 3 channels (BGR)</description></item>
        ///   <item><term>Format32bppArgb / Format32bppRgb / Format32bppPArgb</term><description>UInt8, 4 channels (BGRA)</description></item>
        ///   <item><term>Format48bppRgb</term><description>Int16, 3 channels</description></item>
        ///   <item><term>Format64bppArgb</term><description>Int16, 4 channels</description></item>
        ///   <item><term>Format16bppGrayScale</term><description>Int16, 1 channel</description></item>
        ///   <item><term>Format8bppIndexed</term><description>UInt8, 1 channel (palette → BT.601 grayscale)</description></item>
        ///   <item><term>Others (CMYK, 1bpp, etc.)</term><description>Fallback: cloned to Format32bppArgb then read</description></item>
        /// </list>
        /// </remarks>
        /// <param name="bitmap">Source bitmap. Returns <c>null</c> if <c>null</c>.</param>
        /// <returns>A new <see cref="CxImage"/> with matching pixel type and channel count.</returns>
        public static CxImage FromBitmap(Bitmap bitmap)
        {
            if (bitmap == null) return null;
            int w = bitmap.Width, h = bitmap.Height;
            var pf = bitmap.PixelFormat;

            if (pf == PixelFormat.Format24bppRgb)
                return LockAndCopy(bitmap, w, h, PixelFormat.Format24bppRgb, PlainType.UInt8, 3);
            if (pf == PixelFormat.Format32bppArgb ||
                pf == PixelFormat.Format32bppRgb ||
                pf == PixelFormat.Format32bppPArgb)
                return LockAndCopy(bitmap, w, h, pf, PlainType.UInt8, 4);
            if (pf == PixelFormat.Format48bppRgb)
                return LockAndCopy(bitmap, w, h, PixelFormat.Format48bppRgb, PlainType.Int16, 3);
            if (pf == PixelFormat.Format64bppArgb)
                return LockAndCopy(bitmap, w, h, PixelFormat.Format64bppArgb, PlainType.Int16, 4);
            if (pf == PixelFormat.Format16bppGrayScale)
                return LockAndCopy(bitmap, w, h, PixelFormat.Format16bppGrayScale, PlainType.Int16, 1);
            if (pf == PixelFormat.Format8bppIndexed)
                return IndexedToGray(bitmap);

            using (var converted = bitmap.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb))
                return FromBitmap(converted);
        }
        #region helper methods
        private static CxImage LockAndCopy(Bitmap bmp, int w, int h, PixelFormat pf, PlainType type, int ch)
        {
            var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, pf);
            try
            {
                var cx = new CxImage();
                cx.SetData(w, h, bd.Scan0, type, ch);
                return cx;
            }
            finally { bmp.UnlockBits(bd); }
        }

        private static CxImage IndexedToGray(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
            try
            {
                var palette = bmp.Palette;
                int n = w * h;
                var indices = new byte[n];
                Marshal.Copy(bd.Scan0, indices, 0, n);

                var gray = new byte[n];
                for (int i = 0; i < n; i++)
                {
                    int idx = indices[i] % palette.Entries.Length;
                    var c = palette.Entries[idx];
                    gray[i] = (byte)((c.R * 77 + c.G * 150 + c.B * 29 + 128) >> 8);
                }
                return new CxImage(w, h, gray, 1);
            }
            finally { bmp.UnlockBits(bd); }
        }

        /// <summary>Normalizes a <see cref="short"/> value to [0, 255] using full range mapping.</summary>
        private static byte NormalizeShort(short v)
        {
            return (byte)((v - (long)short.MinValue) * 255L / (short.MaxValue - (long)short.MinValue));
        }

        /// <summary>Normalizes an <see cref="int"/> value to [0, 255] using full 32-bit range mapping.</summary>
        private static byte NormalizeInt32(int v)
        {
            return (byte)(((long)v - int.MinValue) * 255L / uint.MaxValue);
        }

        /// <summary>Scales a [0, 1] float value to [0, 255] byte, clamping out-of-range inputs.</summary>
        private static byte NormalizeFloat(float v)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)(v * 255)));
        }
        #endregion
    }
}
