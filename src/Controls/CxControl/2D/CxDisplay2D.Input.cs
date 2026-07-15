using System;
using System.Drawing;
using System.Windows.Forms;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Mouse and keyboard event handling for <see cref="CxDisplay2D"/>.</summary>
    public partial class CxDisplay2D
    {
        private void WireMouseEvents()
        {
            _formsPlot.MouseMove += OnPlotMouseMove;
            _formsPlot.MouseDown += OnPlotMouseDown;
            _formsPlot.MouseUp   += OnPlotMouseUp;
        }

        private void OnPlotMouseMove(object sender, MouseEventArgs e)
        {
            var plotCoord = GetPlotCoordinate(e.X, e.Y);

            // Broadcast world coordinates (scale/offset applied; default = pixel coords)
            var (wx, wy, _) = PlotToWorld(plotCoord);
            CoordinatesChanged?.Invoke(new CxPoint2D(wx, wy));

            if (_isDragging && _selectedItem != null)
            {
                _selectedItem.OnMouseMove(plotCoord, _lastDragPos);
                _lastDragPos = plotCoord;
                RefreshDisplay();
            }
        }

        private void OnPlotMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var plotCoord = GetPlotCoordinate(e.X, e.Y);

            if (_selectedItem != null)
            {
                _selectedItem.OnDeselected();
                _selectedItem = null;
                SelectionChanged?.Invoke(null);
            }

            // Hit-test active items in reverse order (topmost first)
            for (int i = _overlayItems.Count - 1; i >= 0; i--)
            {
                var item = _overlayItems[i];
                if (!item.IsActiveObj || !item.HitTest(plotCoord)) continue;

                _selectedItem = item;
                item.OnMouseDown(plotCoord);
                _isDragging  = true;
                _lastDragPos = plotCoord;
                _formsPlot.UserInputProcessor.Disable();
                SelectionChanged?.Invoke(item);
                RefreshDisplay();
                return;
            }

            // No active item hit: show world-coordinate annotation at click position
            var (wx, wy, wz) = PlotToWorld(plotCoord);
            ShowCoordAnnotation(plotCoord, wx, wy, wz);
            RefreshDisplay();
        }

        private void OnPlotMouseUp(object sender, MouseEventArgs e)
        {
            HideCoordAnnotation();
            if (!_isDragging) return;
            _isDragging = false;
            _formsPlot.UserInputProcessor.Enable();
            _selectedItem?.OnMouseUp();
        }

        private CxPoint2D GetPlotCoordinate(int screenX, int screenY)
        {
            var px    = new ScottPlot.Pixel(screenX, screenY);
            var coord = _formsPlot.Plot.GetCoordinates(px);
            return new CxPoint2D((float)coord.X, (float)coord.Y);
        }
    }
}
