using System;
using System.Drawing;
using System.IO;
using ScottPlot;
using VisionNet.DataType;
using static System.Net.Mime.MediaTypeNames;
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


    public abstract class Abstract2DImageRenderItem : I2DRenderItem
    {
        protected CxImage orignalImage;
        public virtual Color Color { get; set; }
        public virtual float Size { get; set; }

        public virtual void AddToPlot(Plot plot) { }
        public virtual void Dispose() {
            orignalImage = null;
        }
        public virtual void RemoveFromPlot(Plot plot) { }
        public virtual void UpdatePlottable() { }
        public virtual void UpdateWorldRect(CxBox2D rect) { }

        public virtual void SetImage(CxImage image) {
            orignalImage = image;
        }

        /// <summary>Returns the raw pixel value at image coordinate (x, y) as float, or null if out of range.</summary>
        public virtual float? GetPixelFloat(int x, int y)
        {
            var _imageData = orignalImage.Channel == 1 ? orignalImage.Data : null;
            var _width = orignalImage?.Width ?? 0;
            var _height = orignalImage?.Height ?? 0;
            var _imageType = orignalImage?.Type ?? PlainType.UInt8;
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

        // ©¤©¤ Image conversion (no BitConverter ¡ª direct typed array casts) ©¤©¤©¤©¤©¤©¤©¤©¤©¤
        protected ScottPlot.Image BuildScottImage(CxImage image)
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
