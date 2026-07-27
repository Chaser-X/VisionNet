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
    /// Renders a large <see cref="CxImage"/> using two layers:
    /// a low-resolution global thumbnail and a sharp full-resolution viewport tile.
    /// The viewport tile is cropped from the original at full resolution when zoomed in.
    /// </summary>
    public class CxImageItemAdvance : I2DRenderItem
    {
        private const int GlobalMaxSize = 1024;

        private CxImage _original;
        private CxImage _globalThumb;
        private ScottPlot.Image _globalScottImage;
        private ScottPlot.Image _detailScottImage;

        private ScottPlot.Plottables.ImageRect _globalPlot;
        private ScottPlot.Plottables.ImageRect _detailPlot;
        private ScottPlot.Plot _plot;

        private int _imgW;
        private int _imgH;
        private CxBox2D _worldRect;

        private bool _hasDetail;
        private int _lastL, _lastR, _lastT, _lastB;

        Color I2DRenderItem.Color { get => Color.White; set { } }
        float I2DRenderItem.Size { get => 1f; set { } }

        public int Width => _imgW;
        public int Height => _imgH;

        public void SetImage(CxImage image)
        {
            if (image == null || image.Data == null) return;

            _original = image;
            _imgW = image.Width;
            _imgH = image.Height;

            _globalThumb = image.Width > GlobalMaxSize || image.Height > GlobalMaxSize
                ? VisionOperator.ResizeImage(image, GlobalMaxSize, GlobalMaxSize)
                : image;

            _globalScottImage = CxImageToScottImage(_globalThumb);

            if (_plot != null)
            {
                if (_globalPlot != null) _plot.PlottableList.Remove(_globalPlot);
                if (_detailPlot != null) _plot.PlottableList.Remove(_detailPlot);
                _hasDetail = false;

                _globalPlot = _plot.Add.ImageRect(_globalScottImage,
                    new CoordinateRect(0, _imgW, _imgH, 0));
                _plot.PlottableList.Remove(_globalPlot);
                _plot.PlottableList.Insert(0, _globalPlot);
            }
        }

        public void AddToPlot(Plot plot)
        {
            _plot = plot;
            if (_globalScottImage != null)
            {
                _globalPlot = plot.Add.ImageRect(_globalScottImage,
                    new CoordinateRect(0, _imgW, _imgH, 0));
                plot.PlottableList.Remove(_globalPlot);
                plot.PlottableList.Insert(0, _globalPlot);
                _hasDetail = false;
            }
        }

        public void RemoveFromPlot(Plot plot)
        {
            if (_globalPlot != null) { plot.PlottableList.Remove(_globalPlot); _globalPlot = null; }
            if (_detailPlot != null) { plot.PlottableList.Remove(_detailPlot); _detailPlot = null; }
            _plot = null;
            _hasDetail = false;
        }

        public void UpdatePlottable()
        {
            RefreshViewport();
        }

        public void Dispose()
        {
            _original = null;
            _globalThumb = null;
            _globalScottImage = null;
            _detailScottImage = null;
            _globalPlot = null;
            _detailPlot = null;
            _plot = null;
            _hasDetail = false;
        }

        /// <summary>Repositions the image plottable to the given world-space rectangle.</summary>
        public void UpdateWorldRect(CxBox2D rect)
        {
            _worldRect = rect;
            if (_globalPlot != null)
                _globalPlot.Rect = new CoordinateRect(rect.Left, rect.Right, rect.Bottom, rect.Top);
        }

        private void RefreshViewport()
        {
            if (_plot == null || _original == null || _imgW <= 0 || _imgH <= 0) return;

            double vpL = _plot.Axes.Bottom.Min;
            double vpR = _plot.Axes.Bottom.Max;
            double vpT = _plot.Axes.Left.Max;   
            double vpB = _plot.Axes.Left.Min;  

            double vpW = vpR - vpL;
            double vpH = Math.Abs(vpB - vpT);

            // Convert to pixel coordinates
            double worldW = _worldRect.Right - _worldRect.Left;
            double worldH = Math.Abs(_worldRect.Bottom - _worldRect.Top);
            if (worldW <= 0 || worldH <= 0) return;

            double scaleX = _imgW / worldW;
            double scaleY = _imgH / worldH;

            int pxL = (int)Math.Round((vpL - _worldRect.Left) * scaleX);
            int pxR = (int)Math.Round((vpR - _worldRect.Left) * scaleX);
            int pxT = (int)Math.Round((vpT - _worldRect.Top) * scaleY);
            int pxB = (int)Math.Round((vpB - _worldRect.Top) * scaleY);

            int vpPxW = pxR - pxL;
            int vpPxH = pxB - pxT;

            // Show detail only when viewport is smaller than global thumbnail resolution
            bool needDetail = vpPxW < _imgW * 0.9 && vpPxH < _imgH * 0.9;

            if (!needDetail)
            {
                if (_hasDetail)
                {
                    if (_detailPlot != null) _plot.PlottableList.Remove(_detailPlot);
                    _detailPlot = null;
                    _detailScottImage = null;
                    _hasDetail = false;
                }
                return;
            }

            // Check cache: recalculate only if viewport moved >50% in any direction
            int marginW = Math.Max(1, vpPxW / 2);
            int marginH = Math.Max(1, vpPxH / 2);
            if (_hasDetail &&
                Math.Abs(pxL - _lastL) < marginW &&
                Math.Abs(pxR - _lastR) < marginW &&
                Math.Abs(pxT - _lastT) < marginH &&
                Math.Abs(pxB - _lastB) < marginH)
            {
                return;
            }

            // Clip detail from original
            var box = new CxBox2D(
                new CxPoint2D((pxL + pxR) / 2f, (pxT + pxB) / 2f),
                new CxSize2D(pxR - pxL, pxB - pxT));

            var detailImage = VisionOperator.ClipImage(_original, box, 0f);
            if (detailImage == null) return;

            _detailScottImage = CxImageToScottImage(detailImage);
            _lastL = pxL; _lastR = pxR;
            _lastT = pxT; _lastB = pxB;

            // Use actual clipped pixel bounds (clamped to image) for world positioning
            int actualL = Math.Max(0, pxL);
            int actualR = Math.Min(_imgW, pxR);
            int actualT = Math.Max(0, pxT);
            int actualB = Math.Min(_imgH, pxB);

            double detailWorldL = _worldRect.Left + actualL / scaleX;
            double detailWorldR = _worldRect.Left + actualR / scaleX;
            double detailWorldT = _worldRect.Top  + actualT / scaleY;
            double detailWorldB = _worldRect.Top  + actualB / scaleY;

            if (_detailPlot != null)
                _plot.PlottableList.Remove(_detailPlot);

            _detailPlot = _plot.Add.ImageRect(_detailScottImage,
                new CoordinateRect(detailWorldL, detailWorldR, detailWorldB, detailWorldT));
            _plot.PlottableList.Remove(_detailPlot);

            int globalIdx = _plot.PlottableList.IndexOf(_globalPlot);
            if (globalIdx >= 0)
                _plot.PlottableList.Insert(globalIdx + 1, _detailPlot);
            else
                _plot.PlottableList.Add(_detailPlot);
            _hasDetail = true;
        }

        private static ScottPlot.Image CxImageToScottImage(CxImage image)
        {
            using (var bmp = VisionOperator.ToBitmap(image))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return new ScottPlot.Image(ms.ToArray());
            }
        }
    }
}
