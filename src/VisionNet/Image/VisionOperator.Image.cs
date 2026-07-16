using System;
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
                    Cv2.Resize(src, dst, new Size(newW, newH),
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
    }
}
