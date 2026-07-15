using System;
using System.Drawing;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>Public API surface: image data, geometric overlays, and view management.</summary>
    public partial class CxDisplay2D
    {
        // ── Image management ──────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the displayed image with a new <see cref="CxImage{T}"/>.
        /// The view is automatically fitted to the image after loading.
        /// </summary>
        /// <typeparam name="T">
        /// Pixel element type. Supported types: <see cref="byte"/> (direct),
        /// <see cref="float"/>/<see cref="double"/> ([0,1]→[0,255]),
        /// <see cref="short"/>/<see cref="ushort"/> (full-range normalisation).
        /// </typeparam>
        /// <param name="image">Source image. Pass <c>null</c> to clear.</param>
        /// <param name="colorMap">
        /// Optional per-pixel colour mapping function. When <c>null</c>, renders as grayscale.
        /// </param>
        public void SetImage<T>(CxImage<T> image, Func<T, Color> colorMap = null)
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

            _imageItem.SetImage(image, colorMap);
            _imageWidth  = image.Width;
            _imageHeight = image.Height;

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
