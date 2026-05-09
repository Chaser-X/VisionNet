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

            // Switch to 2D orthographic projection for HUD rendering.
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height, -1, 1);
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            // Draw colour gradient quads.
            gl.Begin(OpenGL.GL_QUADS);
            for (int i = 0; i < barHeight; i++)
            {
                float normalised = (float)i / barHeight;
                double z = _zMin + normalised * (_zMax - _zMin);
                var (r, g, b) = CxExtension.GetColorByHeight(z, _zMin, _zMax);

                gl.Color(r, g, b);
                gl.Vertex(startX,            startY + i,     0);
                gl.Vertex(startX + barWidth, startY + i,     0);
                gl.Vertex(startX + barWidth, startY + i + 1, 0);
                gl.Vertex(startX,            startY + i + 1, 0);
            }
            gl.End();

            // Draw tick marks and Z-value labels.
            const int divisions = 7;
            gl.Color(1.0f, 1.0f, 1.0f);
            gl.LineWidth(1.0f);
            for (int i = 0; i <= divisions; i++)
            {
                int    tickY  = startY + (int)(i * (barHeight / (float)divisions));
                double zValue = _zMin + i * (_zMax - _zMin) / divisions;

                gl.Begin(OpenGL.GL_LINES);
                gl.Vertex(startX - 5, tickY, 0);
                gl.Vertex(startX,     tickY, 0);
                gl.End();

                gl.DrawText(startX - 45, tickY - 5, 1, 1, 1, "", 10, $"{zValue:F2}");
            }

            // Restore matrices and depth test.
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }
    }
}
