using System;
using System.Drawing;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>Public API surface: image data, geometric overlays, and view management.</summary>
    public partial class CxDisplay2D
    {
        // ── Coordinate system ─────────────────────────────────────────────────────

        /// <summary>
        /// Sets the scale factors for converting image pixel coordinates to world coordinates.
        /// Default: (1, 1, 1) — no scaling applied.
        /// </summary>
        public void SetCoordinateScale(float xScale, float yScale, float zScale = 1f)
        {
            XScale = xScale;
            YScale = yScale;
            ZScale = zScale;
            if (_imageItem != null) _imageItem.UpdateWorldRect(GetImageWorldRect());
            FitToImage();
        }

        /// <summary>
        /// Sets the offsets applied after scaling to produce world (X, Y, Z) values.
        /// Default: (0, 0, 0) — no offset applied.
        /// </summary>
        public void SetCoordinateOffset(float xOffset, float yOffset, float zOffset = 0f)
        {
            XOffset = xOffset;
            YOffset = yOffset;
            ZOffset = zOffset;
            if (_imageItem != null) _imageItem.UpdateWorldRect(GetImageWorldRect());
            FitToImage();
        }

        /// <summary>
        /// Converts a plot-space coordinate (already in world units) to (X, Y, Z).
        /// Z is derived from the image pixel value at the corresponding pixel position.
        /// </summary>
        internal (float X, float Y, float? Z) PlotToWorld(CxPoint2D plotCoord)
        {
            // The plot coordinate system IS world coordinates; no scale/offset conversion needed.
            float wx = plotCoord.X;
            float wy = plotCoord.Y;
            // Reverse-map world → pixel for image data lookup
            int px = XScale != 0f ? (int)Math.Round((plotCoord.X - XOffset) / XScale) : 0;
            int py = YScale != 0f ? (int)Math.Round((plotCoord.Y - YOffset) / YScale) : 0;
            float? rawZ = _imageItem?.GetPixelFloat(px, py);
            float? wz   = rawZ.HasValue ? rawZ.Value * ZScale + ZOffset : (float?)null;
            return (wx, wy, wz);
        }

        /// <summary>Returns the image bounding rectangle in world coordinates (Y-inverted for image convention).</summary>
        public CxBox2D GetImageWorldRect()
        {
            float w = _imageWidth  * XScale;
            float h = _imageHeight * YScale;
            return new CxBox2D(
                new CxPoint2D(XOffset + w / 2f, YOffset + h / 2f),
                new CxSize2D(w, h));
        }

        // ── Image management ──────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the displayed image with a new <see cref="CxImage"/>.
        /// The view is automatically fitted to the image after loading.
        /// Rendering is automatic based on <see cref="CxImage.Type"/> and <see cref="CxImage.Channel"/>:
        /// 1-channel → grayscale; 3-channel → RGB; 4-channel → BGRA.
        /// </summary>
        /// <param name="image">Source image. Pass <c>null</c> to clear.</param>
        public void SetImage(CxImage image)
        {
            if (_imageItem == null)
            {
                _imageItem = new CxImageItem();
                _imageItem.AddToPlot(_formsPlot.Plot);
            }

            if (image == null)
            {
                ClearImage();
                return;
            }

            _imageItem.SetImage(image);
            _imageWidth  = image.Width;
            _imageHeight = image.Height;

            // Move image to world-coordinate rect so axes show world values
            _imageItem.UpdateWorldRect(GetImageWorldRect());
            FitToImage();
        }

        /// <summary>Removes the currently displayed image.</summary>
        public void ClearImage()
        {
            if (_imageItem != null)
            {
                _imageItem.RemoveFromPlot(_formsPlot.Plot);
                _imageItem.Dispose();
                _imageItem   = null;
                _imageWidth  = 0;
                _imageHeight = 0;
            }
            HideCoordAnnotation();
            RefreshDisplay();
        }

        // ── Overlay management ────────────────────────────────────────────────────

        /// <summary>
        /// Adds a set of 2D point markers to the display.
        /// Each call appends a new item; use the returned handle to remove or modify it.
        /// </summary>
        /// <param name="points">Point coordinates in image pixel space.</param>
        /// <param name="color">Marker colour.</param>
        /// <param name="size">Marker size in pixels (default 5).</param>
        /// <returns>The created <see cref="CxPoint2DItem"/>.</returns>
        public CxPoint2DItem SetPoint(CxPoint2D[] points, Color color, float size = 5f)
        {
            var item = new CxPoint2DItem(points, color, size);
            return AppendOverlay(item);
        }

        /// <summary>
        /// Adds a set of 2D line segments to the display.
        /// </summary>
        /// <param name="segments">Segment data in image pixel space.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels (default 1).</param>
        /// <returns>The created <see cref="CxSegment2DItem"/>.</returns>
        public CxSegment2DItem SetSegment(CxSegment2D[] segments, Color color, float size = 1f)
        {
            var item = new CxSegment2DItem(segments, color, size);
            return AppendOverlay(item);
        }

        /// <summary>
        /// Adds a set of 2D polygons (open or closed) to the display.
        /// </summary>
        /// <param name="polygons">Polygon data in image pixel space.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels (default 1).</param>
        /// <returns>The created <see cref="CxPolygon2DItem"/>.</returns>
        public CxPolygon2DItem SetPolygon(CxPolygon2D[] polygons, Color color, float size = 1f)
        {
            var item = new CxPolygon2DItem(polygons, color, size);
            return AppendOverlay(item);
        }

        /// <summary>
        /// Adds a set of 2D circles to the display.
        /// </summary>
        /// <param name="circles">Circle data in image pixel space.</param>
        /// <param name="color">Outline colour.</param>
        /// <param name="size">Line width in pixels (default 1).</param>
        /// <returns>The created <see cref="CxCircle2DItem"/>.</returns>
        public CxCircle2DItem SetCircle(CxCircle2D[] circles, Color color, float size = 1f)
        {
            var item = new CxCircle2DItem(circles, color, size);
            return AppendOverlay(item);
        }

        /// <summary>
        /// Adds a set of 2D text labels to the display.
        /// </summary>
        /// <param name="texts">Text entries in image pixel space.</param>
        /// <param name="color">Text colour.</param>
        /// <returns>The created <see cref="CxText2DPlotItem"/>.</returns>
        public CxText2DPlotItem SetText2D(CxText2D[] texts, Color color)
        {
            var item = new CxText2DPlotItem(texts, color);
            return AppendOverlay(item);
        }

        /// <summary>Removes all overlay items from the display.</summary>
        public void ClearOverlays()
        {
            foreach (var item in _overlayItems)
            {
                item.RemoveFromPlot(_formsPlot.Plot);
                item.Dispose();
            }
            _overlayItems.Clear();
            _selectedItem = null;
            RefreshDisplay();
        }

        /// <summary>Removes a specific overlay item from the display.</summary>
        public void RemoveOverlay(Abstract2DRenderItem item)
        {
            if (!_overlayItems.Contains(item)) return;
            item.RemoveFromPlot(_formsPlot.Plot);
            item.Dispose();
            _overlayItems.Remove(item);
            if (_selectedItem == item)
            {
                _selectedItem = null;
                SelectionChanged?.Invoke(null);
            }
            RefreshDisplay();
        }

        // ── View management ───────────────────────────────────────────────────────

        /// <summary>Resets the view to fit the currently loaded image, or autoscales if no image.</summary>
        public void ResetView()
        {
            if (_imageWidth > 0 && _imageHeight > 0)
                FitToImage();
            else
            {
                _formsPlot.Plot.Axes.AutoScale();
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Fits the current view to the image with an equal aspect ratio (1:1 pixel scale).
        /// Useful when displaying circles that must appear as true circles.
        /// </summary>
        public void SetAspectLock(bool locked)
        {
            if (_imageWidth > 0 && _imageHeight > 0)
                FitToImage();
            else
                RefreshDisplay();
        }

        // ── Selection management ──────────────────────────────────────────────────

        /// <summary>Deselects the currently selected item, if any.</summary>
        public void ClearSelection()
        {
            if (_selectedItem == null) return;
            _selectedItem.OnDeselected();
            _selectedItem = null;
            SelectionChanged?.Invoke(null);
            RefreshDisplay();
        }

        /// <summary>Enables mouse interaction (selection and drag) for all overlay items.</summary>
        public void ActivateAllItems()
        {
            foreach (var item in _overlayItems) item.IsActiveObj = true;
        }

        /// <summary>Disables mouse interaction for all overlay items.</summary>
        public void DeactivateAllItems()
        {
            foreach (var item in _overlayItems) item.IsActiveObj = false;
        }

        // ── Internal helpers ──────────────────────────────────────────────────────

        private T AppendOverlay<T>(T item) where T : Abstract2DRenderItem
        {
            item.AddToPlot(_formsPlot.Plot);
            _overlayItems.Add(item);
            RefreshDisplay();
            return item;
        }
    }
}
