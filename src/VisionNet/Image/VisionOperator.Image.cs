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

        private static byte NormalizeShort(short v)
        {
            return (byte)((v - (long)short.MinValue) * 255L / (short.MaxValue - (long)short.MinValue));
        }

        private static byte NormalizeInt32(int v)
        {
            return (byte)(((long)v - int.MinValue) * 255L / uint.MaxValue);
        }

        private static byte NormalizeFloat(float v)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)(v * 255)));
        }
    }
}
