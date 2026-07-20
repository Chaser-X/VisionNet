using System;
using System.Drawing;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    /// <summary>Public API surface for <see cref="CxDisplay2D"/>.</summary>
    public partial class CxDisplay2D
    {
        #region Coordinate system

        /// <summary>
        /// Sets the scale and offset for converting image pixel coordinates to world coordinates.
        /// Default scale: (1, 1, 1) — no scaling. Default offset: (0, 0, 0) — no offset.
        /// </summary>
        public void SetScaleAndOffset(CxPoint3D scale, CxPoint3D offset)
        {
            Scale = scale;
            Offset = offset;
            if (_imageItem != null) _imageItem.UpdateWorldRect(GetImageWorldRect());
            FitImage1to1();
        }

        /// <summary>Returns the image bounding rectangle expressed in world coordinates.</summary>
        public CxBox2D GetImageWorldRect()
        {
            float w = _imageWidth * Scale.X;
            float h = _imageHeight * Scale.Y;
            return new CxBox2D(
                new CxPoint2D(Offset.X + w / 2f, Offset.Y + h / 2f),
                new CxSize2D(w, h));
        }

        #endregion

        #region Display settings

        /// <summary>Shows or hides the X/Y coordinate axes (tick labels and axis lines). Default: <c>true</c>.</summary>
        public void ShowAxes(bool visible)
        {
            _formsPlot.Plot.Axes.Bottom.IsVisible = visible;
            _formsPlot.Plot.Axes.Left.IsVisible = visible;
            _formsPlot.Plot.Axes.Right.IsVisible = visible;
            _formsPlot.Plot.Axes.Top.IsVisible = visible;
            RefreshDisplay();
        }

        /// <summary>
        /// Enforces or releases a 1:1 X/Y aspect ratio (equal world units per pixel on both axes).
        /// When <paramref name="locked"/> is <c>true</c>, the image is also fitted to the current view.
        /// </summary>
        /// <summary>
        /// Enables or disables the 1:1 aspect ratio constraint.
        /// When enabled, every pan/zoom operation maintains equal X/Y scale.
        /// </summary>
        public void SetAspectLock(bool locked)
        {
            if (locked)
            {
                if (!_formsPlot.Plot.Axes.Rules.Contains(_squareRule))
                    _formsPlot.Plot.Axes.Rules.Add(_squareRule);
                FitImage1to1();
            }
            else
            {
                _formsPlot.Plot.Axes.Rules.Remove(_squareRule);
                RefreshDisplay();
            }
        }

        public void SetBackgroundColor(Color color)
        {
            _formsPlot.BackColor = color;
            _formsPlot.Plot.DataBackground.Color = ScottPlot.Color.FromColor(color);
            RefreshDisplay();
        }
        #endregion

        #region Image management

        /// <summary>
        /// Replaces the displayed image with a new <see cref="CxImage"/>.
        /// Rendering adapts to <see cref="CxImage.Type"/> and <see cref="CxImage.Channel"/>:
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
            _imageWidth = image.Width;
            _imageHeight = image.Height;

            _imageItem.UpdateWorldRect(GetImageWorldRect());
            FitImage1to1();
        }

        /// <summary>Removes the currently displayed image.</summary>
        public void ClearImage()
        {
            if (_imageItem != null)
            {
                _imageItem.RemoveFromPlot(_formsPlot.Plot);
                _imageItem.Dispose();
                _imageItem = null;
                _imageWidth = 0;
                _imageHeight = 0;
            }
            HideCoordAnnotation();
            RefreshDisplay();
        }

        #endregion

        #region Overlay management

        /// <summary>Adds a set of 2D point markers. Returns the created item for later manipulation.</summary>
        public CxPoint2DItem SetPoint(CxPoint2D[] points, Color color, float size = 5f)
            => AppendOverlay(new CxPoint2DItem(points, color, size));

        /// <summary>Adds a set of 2D line segments. Returns the created item for later manipulation.</summary>
        public CxSegment2DItem SetSegment(CxSegment2D[] segments, Color color, float size = 1f)
            => AppendOverlay(new CxSegment2DItem(segments, color, size));

        /// <summary>Adds a set of 2D polygons (open or closed). Returns the created item for later manipulation.</summary>
        public CxPolygon2DItem SetPolygon(CxPolygon2D[] polygons, Color color, float size = 1f, bool filled = false)
            => AppendOverlay(new CxPolygon2DItem(polygons, color, size, filled));

        /// <summary>Adds a set of 2D circles. Returns the created item for later manipulation.</summary>
        public CxCircle2DItem SetCircle(CxCircle2D[] circles, Color color, float size = 1f, bool filled = false)
            => AppendOverlay(new CxCircle2DItem(circles, color, size, filled));

        /// <summary>Adds a set of 2D infinite lines. Returns the created item for later manipulation.</summary>
        public CxLine2DItem SetLine(CxLine2D[] lines, Color color, float size = 1f)
            => AppendOverlay(new CxLine2DItem(lines, color, size));

        /// <summary>Adds a set of 2D axis-aligned boxes (rectangles). Returns the created item for later manipulation.</summary>
        public CxBox2DItem SetBox(CxBox2D[] boxes, Color color, float size = 1f, bool filled = false)
            => AppendOverlay(new CxBox2DItem(boxes, color, size, filled));

        /// <summary>Adds a set of 2D oriented rectangles (rotatable). Returns the created item for later manipulation.</summary>
        public CxRectangle2DItem SetRectangle(CxRectangle2D[] rects, Color color, float size = 1f, bool filled = false)
            => AppendOverlay(new CxRectangle2DItem(rects, color, size, filled));

        /// <summary>Adds a set of 2D arcs. Returns the created item for later manipulation.</summary>
        public CxArc2DItem SetArc(CxArc2D[] arcs, Color color, float size = 1f)
            => AppendOverlay(new CxArc2DItem(arcs, color, size));

        /// <summary>Adds a set of 2D text labels. Returns the created item for later manipulation.</summary>
        public CxText2DPlotItem SetText2D(CxText2D[] texts, Color color)
            => AppendOverlay(new CxText2DPlotItem(texts, color));

        /// <summary>Adds a set of 2D edge-finding ROI fields. Returns the created item for later manipulation.</summary>
        public CxSegment2DFittingFieldItem SetSegmentFittingField(CxSegment2DFittingField[] fields, Color color, float size = 1f)
            => AppendOverlay(new CxSegment2DFittingFieldItem(fields, color, size));

        /// <summary>Adds a set of 2D arc edge-finding ROI fields. Returns the created item for later manipulation.</summary>
        public CxArc2DFittingFieldItem SetArcFittingField(CxArc2DFittingField[] fields, Color color, float size = 1f)
            => AppendOverlay(new CxArc2DFittingFieldItem(fields, color, size));

        /// <summary>Adds a set of 2D polygon edge-finding ROI fields. Returns the created item for later manipulation.</summary>
        public CxPolygon2DFittingFieldItem SetPolygonFittingField(CxPolygon2DFittingField[] fields, Color color, float size = 1f)
            => AppendOverlay(new CxPolygon2DFittingFieldItem(fields, color, size));

        /// <summary>Adds a set of 2D circle edge-finding ROI fields. Returns the created item for later manipulation.</summary>
        public CxCircle2DFittingFieldItem SetCircleFittingField(CxCircle2DFittingField[] fields, Color color, float size = 1f)
            => AppendOverlay(new CxCircle2DFittingFieldItem(fields, color, size));

        /// <summary>Adds a set of 2D coordinate frames. Returns the created item.</summary>
        public CxCoordination2DItem SetCoordination(CxCoordination2D[] frames, Color color, float size = 1f)
            => AppendOverlay(new CxCoordination2DItem(frames, color, size));

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

        #endregion

        #region View management

        /// <summary>Clears all overlays and the displayed image, returning the control to an empty state.</summary>
        public void ResetView()
        {
            ClearOverlays();
            ClearImage();
        }

        #endregion

        #region Selection management

        /// <summary>Deselects the currently selected overlay item, if any.</summary>
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

        #endregion

        #region Internal helpers

        private T AppendOverlay<T>(T item) where T : Abstract2DRenderItem
        {
            item.AddToPlot(_formsPlot.Plot);
            _overlayItems.Add(item);
            RefreshDisplay();
            return item;
        }

        /// <summary>Converts a plot-space (world-unit) coordinate to (X, Y, Z). Z comes from the image pixel value.</summary>
        internal (float X, float Y, float? Z) PlotToWorld(CxPoint2D plotCoord)
        {
            float wx = plotCoord.X;
            float wy = plotCoord.Y;
            int px = Scale.X != 0f ? (int)Math.Round((plotCoord.X - Offset.X) / Scale.X) : 0;
            int py = Scale.Y != 0f ? (int)Math.Round((plotCoord.Y - Offset.Y) / Scale.Y) : 0;
            float? rawZ = _imageItem?.GetPixelFloat(px, py);
            float? wz = rawZ.HasValue ? rawZ.Value * Scale.Z + Offset.Z : (float?)null;
            return (wx, wy, wz);
        }

        #endregion
    }
}
