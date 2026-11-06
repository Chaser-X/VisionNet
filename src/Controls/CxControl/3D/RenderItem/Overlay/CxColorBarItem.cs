using OpenTK.Graphics.OpenGL;
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
        /// <summary>
        /// Number of intervals the bar is divided into. Also the number of tick marks plus one,
        /// and the number of Z-value labels.
        /// </summary>
        private const int TickDivisions = 7;

        private float _zMin;
        private float _zMax;
        private float[] _barPositions = new float[0];
        private float[] _barColors = new float[0];
        private float[] _tickPositions = new float[0];
        private float _cachedZMin;
        private float _cachedZMax;
        private int _cachedViewportWidth = -1;
        private int _cachedViewportHeight = -1;
        private bool _hasCachedGeometry;

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

        /// <summary>
        /// Screen Y of tick <paramref name="index"/>, where index 0 is the bottom of the bar
        /// (<paramref name="startY"/>) and <see cref="TickDivisions"/> is its top.
        /// </summary>
        internal static int TickY(int index, int startY, int barHeight)
            => startY + (int)(index * (barHeight / (float)TickDivisions));

        /// <summary>
        /// Z value labelled at tick <paramref name="index"/>: the range split into
        /// <see cref="TickDivisions"/> equal steps, so index 0 is <paramref name="zMin"/> and
        /// <see cref="TickDivisions"/> is <paramref name="zMax"/>.
        /// </summary>
        internal static double TickZValue(int index, float zMin, float zMax)
            => zMin + index * (zMax - zMin) / TickDivisions;

        /// <inheritdoc/>
        public override void Draw(CxGlContext gl)
        {
            if (_zMax - _zMin <= 0) return;

            int barWidth  = 20;
            int viewportWidth = gl.Width;
            int viewportHeight = gl.Height;
            int barHeight = viewportHeight / 2;
            int startX    = viewportWidth - barWidth - 10;
            int startY    = (gl.Height - barHeight) / 2;

            var renderer = ModernGLRenderer.For(gl);
            bool geometryChanged = !_hasCachedGeometry
                || _cachedZMin != _zMin
                || _cachedZMax != _zMax
                || _cachedViewportWidth != viewportWidth
                || _cachedViewportHeight != viewportHeight;
            if (geometryChanged)
            {
                _barPositions = new float[(barHeight + 1) * 2 * 2];
                _barColors = new float[(barHeight + 1) * 2 * 4];
                _tickPositions = new float[(TickDivisions + 1) * 4];
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

                var tickPositions = _tickPositions;
                for (int i = 0; i <= TickDivisions; i++)
                {
                    int tickY = TickY(i, startY, barHeight);
                    int offset = i * 4;
                    tickPositions[offset] = startX - 5;
                    tickPositions[offset + 1] = tickY;
                    tickPositions[offset + 2] = startX;
                    tickPositions[offset + 3] = tickY;
                }

                _cachedZMin = _zMin;
                _cachedZMax = _zMax;
                _cachedViewportWidth = viewportWidth;
                _cachedViewportHeight = viewportHeight;
                _hasCachedGeometry = true;
            }
            renderer.DrawScreenGeometryColored(gl, PrimitiveType.TriangleStrip,
                _barPositions, _barColors);

            // Draw tick marks and Z-value labels.
            renderer.DrawScreenGeometry(gl, PrimitiveType.Lines,
                _tickPositions, System.Drawing.Color.White);

            for (int i = 0; i <= TickDivisions; i++)
            {
                int tickY = TickY(i, startY, barHeight);
                double zValue = TickZValue(i, _zMin, _zMax);
                renderer.DrawText(gl, startX - 45, tickY - 5, System.Drawing.Color.White, 10, $"{zValue:F2}");
            }
        }
    }
}
