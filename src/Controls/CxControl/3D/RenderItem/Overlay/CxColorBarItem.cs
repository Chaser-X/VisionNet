using SharpGL;
using System;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a vertical rainbow colour bar on the right side of the viewport with
    /// evenly spaced tick marks and Z-value labels.
    /// The bar covers the current <see cref="SetRange"/> Z range.
    /// </summary>
    public class CxColorBarItem : AbstractRenderItem
    {
        private float _zMin;
        private float _zMax;
        private float[] _barPositions = new float[0];
        private float[] _barColors = new float[0];
        private float[] _tickPositions = new float[0];
        private int _cachedBarHeight = -1;

        /// <summary>Initializes the colour bar with the given Z range.</summary>
        /// <param name="zMin">Lower bound of the Z range (dark blue end).</param>
        /// <param name="zMax">Upper bound of the Z range (white end).</param>
        public CxColorBarItem(float zMin = 0, float zMax = 0)
        {
            _zMin = zMin;
            _zMax = zMax;
        }

        /// <summary>Updates the Z range displayed by the colour bar.</summary>
        /// <param name="zMin">New lower bound.</param>
        /// <param name="zMax">New upper bound.</param>
        public void SetRange(float zMin, float zMax)
        {
            _zMin = zMin;
            _zMax = zMax;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (_zMax - _zMin <= 0) return;

            int barWidth  = 20;
            int barHeight = gl.RenderContextProvider.Height / 2;
            int startX    = gl.RenderContextProvider.Width - barWidth - 10;
            int startY    = (gl.RenderContextProvider.Height - barHeight) / 2;

            var renderer = ModernOverlayRenderer.For(gl);
            if (_cachedBarHeight != barHeight)
            {
                _barPositions = new float[(barHeight + 1) * 2 * 2];
                _barColors = new float[(barHeight + 1) * 2 * 4];
                _tickPositions = new float[(7 + 1) * 4];
                _cachedBarHeight = barHeight;
            }
            var positions = _barPositions;
            var colors = _barColors;
            for (int i = 0; i <= barHeight; i++)
            {
                float normalised = barHeight == 0 ? 0f : (float)i / barHeight;
                double z = _zMin + normalised * (_zMax - _zMin);
                var (r, g, b) = CxExtension.GetColorByHeight(z, _zMin, _zMax);
                int vertex = i * 2;
                positions[vertex * 2] = startX;
                positions[vertex * 2 + 1] = startY + i;
                positions[(vertex + 1) * 2] = startX + barWidth;
                positions[(vertex + 1) * 2 + 1] = startY + i;
                for (int side = 0; side < 2; side++)
                {
                    int color = (vertex + side) * 4;
                    colors[color] = (float)r;
                    colors[color + 1] = (float)g;
                    colors[color + 2] = (float)b;
                    colors[color + 3] = 1f;
                }
            }
            renderer.DrawScreenGeometryColored(gl, OpenGL.GL_TRIANGLE_STRIP, positions, colors);

            // Draw tick marks and Z-value labels.
            const int divisions = 7;
            var tickPositions = _tickPositions;
            for (int i = 0; i <= divisions; i++)
            {
                int tickY = startY + (int)(i * (barHeight / (float)divisions));

                int offset = i * 4;
                tickPositions[offset] = startX - 5;
                tickPositions[offset + 1] = tickY;
                tickPositions[offset + 2] = startX;
                tickPositions[offset + 3] = tickY;
            }
            renderer.DrawScreenGeometry(gl, OpenGL.GL_LINES, tickPositions, System.Drawing.Color.White);

            for (int i = 0; i <= divisions; i++)
            {
                int tickY = startY + (int)(i * (barHeight / (float)divisions));
                double zValue = _zMin + i * (_zMax - _zMin) / divisions;
                renderer.DrawText(gl, startX - 45, tickY - 5, System.Drawing.Color.White, 10, $"{zValue:F2}");
            }
        }
    }
}
