using System;
using System.Drawing;
using OpenTK.Graphics.OpenGL;

namespace VisionNet.Controls
{
    internal sealed partial class ModernGLRenderer
    {

        /// <summary>
        /// Draws world-space positions with the shared geometry shader. The current
        /// model matrix is combined with the camera matrices before the draw.
        /// </summary>
        internal void Draw(PrimitiveType primitive, float[] positions, Color color,
            float size = 1f, bool depthTest = true)
        {
            if (positions == null || positions.Length < 3) return;
            EnsureSceneMatrices();
            RenderStateSnapshot previous;
            if (!BeginGeometryDraw(out previous)) return;
            SetRenderState(depthTest, color.A < 255);

            GL.BindVertexArray(_geometryVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _geometryVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, positions.Length * sizeof(float),
                positions, BufferUsageHint.StreamDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), IntPtr.Zero);
            GL.EnableVertexAttribArray(0);

            GL.UseProgram(_geometryProgram);
            GL.UniformMatrix4(_geometryProjection, 1, true, _projection);
            GL.UniformMatrix4(_geometryView, 1, true, _view);
            GL.UniformMatrix4(_geometryModel, 1, true, CurrentModel().Data);
            GL.Uniform1(_geometryUseVertexColor, 0);
            GL.Uniform4(_geometryColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            if (primitive == PrimitiveType.Lines || primitive == PrimitiveType.LineStrip ||
                primitive == PrimitiveType.LineLoop) GL.LineWidth(Math.Max(1f, size));
            if (primitive == PrimitiveType.Points) GL.PointSize(Math.Max(1f, size));
            GL.DrawArrays(primitive, 0, positions.Length / 3);

            EndGeometryDraw(previous, vertexColorAttribute: false);
        }

        /// <summary>
        /// Common prologue for a shared-geometry draw: compiles/acquires the geometry program,
        /// allocates its VAO/VBO on first use, and captures the GL state the draw may change.
        /// Returns <c>false</c> when the program is unavailable, in which case nothing was bound
        /// and nothing needs restoring.
        /// </summary>
        private bool BeginGeometryDraw(out RenderStateSnapshot previous)
        {
            previous = default(RenderStateSnapshot);
            if (!EnsureGeometryProgram()) return false;
            EnsureGeometryBuffers();
            previous = CaptureRenderState();
            return true;
        }

        /// <summary>
        /// Common epilogue for a shared-geometry draw: releases the geometry VAO/VBO bindings and
        /// the program, then restores the state captured by <see cref="BeginGeometryDraw"/>.
        /// </summary>
        /// <param name="vertexColorAttribute">
        /// <c>true</c> when the draw enabled attribute 1 for per-vertex colours.
        /// </param>
        private void EndGeometryDraw(RenderStateSnapshot previous, bool vertexColorAttribute)
        {
            if (vertexColorAttribute) GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(0);
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.UseProgram(0);
            RestoreRenderState(previous);
        }

        /// <summary>
        /// Draws 2D pixel coordinates using an orthographic projection. Depth testing
        /// and depth writes are disabled for the call, then restored to their prior state.
        /// </summary>
        internal void DrawScreenGeometry(PrimitiveType primitive, float[] positions,
            Color color, float width = 1f)
        {
            if (positions == null || positions.Length < 2) return;
            RenderStateSnapshot previous;
            if (!BeginGeometryDraw(out previous)) return;
            SetRenderState(false, color.A < 255);
            GL.DepthMask(false);

            float[] expanded = To3D(positions);
            GL.BindVertexArray(_geometryVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _geometryVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, expanded.Length * sizeof(float),
                expanded, BufferUsageHint.StreamDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), IntPtr.Zero);
            GL.EnableVertexAttribArray(0);
            GL.UseProgram(_geometryProgram);
            float[] projection = Ortho(_gl.Width, _gl.Height);
            GL.UniformMatrix4(_geometryProjection, 1, true, projection);
            GL.UniformMatrix4(_geometryView, 1, true, _identity);
            GL.UniformMatrix4(_geometryModel, 1, true, _identity);
            GL.Uniform1(_geometryUseVertexColor, 0);
            GL.Uniform4(_geometryColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            if (primitive == PrimitiveType.Lines || primitive == PrimitiveType.LineStrip ||
                primitive == PrimitiveType.LineLoop) GL.LineWidth(Math.Max(1f, width));
            GL.DrawArrays(primitive, 0, positions.Length / 2);

            EndGeometryDraw(previous, vertexColorAttribute: false);
        }

        /// <summary>
        /// Draws screen-space geometry with a per-vertex RGBA colour. The interleaved
        /// position/colour data is uploaded to the shared geometry VBO for one draw.
        /// </summary>
        internal void DrawScreenGeometryColored(PrimitiveType primitive, float[] positions,
            float[] colors, float width = 1f)
        {
            if (positions == null || positions.Length < 2 || colors == null
                || colors.Length < (positions.Length / 2) * 4) return;

            RenderStateSnapshot previous;
            if (!BeginGeometryDraw(out previous)) return;
            SetRenderState(false, true);
            GL.DepthMask(false);

            int vertexCount = positions.Length / 2;
            EnsureCapacity(ref _coloredInterleaved, vertexCount * 7);
            var interleaved = _coloredInterleaved;
            for (int i = 0; i < vertexCount; i++)
            {
                interleaved[i * 7] = positions[i * 2];
                interleaved[i * 7 + 1] = positions[i * 2 + 1];
                interleaved[i * 7 + 2] = 0f;
                interleaved[i * 7 + 3] = colors[i * 4];
                interleaved[i * 7 + 4] = colors[i * 4 + 1];
                interleaved[i * 7 + 5] = colors[i * 4 + 2];
                interleaved[i * 7 + 6] = colors[i * 4 + 3];
            }

            GL.BindVertexArray(_geometryVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _geometryVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float),
                interleaved, BufferUsageHint.StreamDraw);
            int stride = 7 * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, IntPtr.Zero);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, new IntPtr(3 * sizeof(float)));
            GL.EnableVertexAttribArray(1);

            GL.UseProgram(_geometryProgram);
            float[] projection = Ortho(_gl.Width, _gl.Height);
            GL.UniformMatrix4(_geometryProjection, 1, true, projection);
            GL.UniformMatrix4(_geometryView, 1, true, _identity);
            GL.UniformMatrix4(_geometryModel, 1, true, _identity);
            GL.Uniform4(_geometryColor, 1f, 1f, 1f, 1f);
            GL.Uniform1(_geometryUseVertexColor, 1);
            if (primitive == PrimitiveType.Lines || primitive == PrimitiveType.LineStrip
                || primitive == PrimitiveType.LineLoop)
                GL.LineWidth(Math.Max(1f, width));
            GL.DrawArrays(primitive, 0, vertexCount);
            GL.Uniform1(_geometryUseVertexColor, 0);

            EndGeometryDraw(previous, vertexColorAttribute: true);
        }
    }
}
