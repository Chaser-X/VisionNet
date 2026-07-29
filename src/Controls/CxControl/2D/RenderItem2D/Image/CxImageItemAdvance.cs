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
    public class CxImageItemAdvance : Abstract2DImageRenderItem
    {
        private const int GlobalMaxSize = 1024;
        private CxImage _globalThumb;
        private ScottPlot.Image _globalScottImage;
        private ScottPlot.Image _detailScottImage;

        private ScottPlot.Plottables.ImageRect _globalPlot;
        private ScottPlot.Plottables.ImageRect _detailPlot;
        private ScottPlot.Plot _plot;

        private CxBox2D _worldRect;

        private bool _hasDetail;
        private int _lastL, _lastR, _lastT, _lastB;


        public override void SetImage(CxImage image)
        {
            if (image == null || image.Data == null) return;
            base.SetImage(image);


            _globalThumb = image.Width > GlobalMaxSize || image.Height > GlobalMaxSize
                ? VisionOperator.ResizeImage(image, GlobalMaxSize, GlobalMaxSize)
                : image;

            _globalScottImage = BuildScottImage(_globalThumb);

            if (_plot != null)
            {
                if (_globalPlot != null) _plot.PlottableList.Remove(_globalPlot);
                if (_detailPlot != null) _plot.PlottableList.Remove(_detailPlot);
                _hasDetail = false;

                _globalPlot = _plot.Add.ImageRect(_globalScottImage,
                    new CoordinateRect(0, orignalImage.Width, orignalImage.Height, 0));
                _plot.PlottableList.Remove(_globalPlot);
                _plot.PlottableList.Insert(0, _globalPlot);
            }
        }

        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            if (_globalScottImage != null)
            {
                _globalPlot = plot.Add.ImageRect(_globalScottImage,
                    new CoordinateRect(0, orignalImage.Width, orignalImage.Height, 0));
                plot.PlottableList.Remove(_globalPlot);
                plot.PlottableList.Insert(0, _globalPlot);
                _hasDetail = false;
            }
        }

        public override void RemoveFromPlot(Plot plot)
        {
            if (_globalPlot != null) { plot.PlottableList.Remove(_globalPlot); _globalPlot = null; }
            if (_detailPlot != null) { plot.PlottableList.Remove(_detailPlot); _detailPlot = null; }
            _plot = null;
            _hasDetail = false;
        }

        public override void UpdatePlottable()
        {
            RefreshViewport();
        }

        public override void Dispose()
        {
            base.Dispose();
            _globalThumb = null;
            _globalScottImage = null;
            _detailScottImage = null;
            _globalPlot = null;
            _detailPlot = null;
            _plot = null;
            _hasDetail = false;
        }

        /// <summary>Repositions the image plottable to the given world-space rectangle.</summary>
        public override void UpdateWorldRect(CxBox2D rect)
        {
            _worldRect = rect;
            if (_globalPlot != null)
                _globalPlot.Rect = new CoordinateRect(rect.Left, rect.Right, rect.Bottom, rect.Top);
        }

        private void RefreshViewport()
        {
            if (_plot == null || orignalImage == null || orignalImage.Width <= 0 || orignalImage.Height <= 0) return;

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

            double scaleX = orignalImage.Width / worldW;
            double scaleY = orignalImage.Height / worldH;

            int pxL_raw = (int)Math.Round((vpL - _worldRect.Left) * scaleX);
            int pxR_raw = (int)Math.Round((vpR - _worldRect.Left) * scaleX);
            int pxT_raw = (int)Math.Round((vpT - _worldRect.Top) * scaleY);
            int pxB_raw = (int)Math.Round((vpB - _worldRect.Top) * scaleY);

            int vpPxW = pxR_raw - pxL_raw;
            int vpPxH = pxB_raw - pxT_raw;

            // Show detail only when viewport is smaller than global thumbnail resolution
            bool needDetail = vpPxW < orignalImage.Width * 0.5 && vpPxH < orignalImage.Height * 0.5;

            if (!needDetail)
            {
                RemoveDetail();
                return;
            }

            // Check cache using raw (unclamped) viewport coords for movement detection
            int marginW = Math.Max(1, vpPxW / 2);
            int marginH = Math.Max(1, vpPxH / 2);
            if (_hasDetail &&
                Math.Abs(pxL_raw - _lastL) < marginW &&
                Math.Abs(pxR_raw - _lastR) < marginW &&
                Math.Abs(pxT_raw - _lastT) < marginH &&
                Math.Abs(pxB_raw - _lastB) < marginH)
            {
                return;
            }

            // Clamp to image bounds — ClipImage already does this internally,
            // but we need clamped values for correct world positioning
            int pxL = Math.Max(0, Math.Min(orignalImage.Width, pxL_raw));
            int pxR = Math.Max(0, Math.Min(orignalImage.Width, pxR_raw));
            int pxT = Math.Max(0, Math.Min(orignalImage.Height, pxT_raw));
            int pxB = Math.Max(0, Math.Min(orignalImage.Height, pxB_raw));

            int cropW = pxR - pxL;
            int cropH = pxB - pxT;
            if (cropW < 1 || cropH < 1)
            {
                RemoveDetail();
                return;
            }

            // Clip detail from original (box already in-bounds)
            var box = new CxBox2D(
                new CxPoint2D((pxL + pxR) / 2f, (pxT + pxB) / 2f),
                new CxSize2D(cropW, cropH));

            var detailImage = VisionOperator.ClipImage(orignalImage, box);
            if (detailImage == null) return;

            _detailScottImage = BuildScottImage(detailImage);
            _lastL = pxL_raw; _lastR = pxR_raw;
            _lastT = pxT_raw; _lastB = pxB_raw;

            double detailWorldL = _worldRect.Left + pxL / scaleX;
            double detailWorldR = _worldRect.Left + pxR / scaleX;
            double detailWorldT = _worldRect.Top  + pxT / scaleY;
            double detailWorldB = _worldRect.Top  + pxB / scaleY;

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

        private void RemoveDetail()
        {
            if (_detailPlot != null) _plot.PlottableList.Remove(_detailPlot);
            _detailPlot = null;
            _detailScottImage = null;
            _hasDetail = false;
        }
    }
}
