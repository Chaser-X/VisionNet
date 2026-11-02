using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    internal sealed partial class ModernGLRenderer
    {

        /// <summary>
        /// Draws world-space positions with the shared geometry shader. The current
        /// model matrix is combined with the camera matrices before the draw.
        /// </summary>
        internal void Draw(OpenGL gl, uint primitive, float[] positions, Color color,
            float size = 1f, bool depthTest = true)
        {
            if (positions == null || positions.Length < 3) return;
            EnsureSceneMatrices(gl);
            RenderStateSnapshot previous;
            if (!BeginGeometryDraw(gl, out previous)) return;
            SetRenderState(gl, depthTest, color.A < 255);

            gl.BindVertexArray(_geometryVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _geometryVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, positions, OpenGL.GL_STREAM_DRAW);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
            gl.EnableVertexAttribArray(0);

            gl.UseProgram(_geometryProgram);
            gl.UniformMatrix4(_geometryProjection, 1, true, _projection);
            gl.UniformMatrix4(_geometryView, 1, true, _view);
            gl.UniformMatrix4(_geometryModel, 1, true, CurrentModel(gl).Data);
            gl.Uniform1(_geometryUseVertexColor, 0);
            gl.Uniform4(_geometryColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            if (primitive == OpenGL.GL_LINES || primitive == OpenGL.GL_LINE_STRIP ||
                primitive == OpenGL.GL_LINE_LOOP) gl.LineWidth(Math.Max(1f, size));
            if (primitive == OpenGL.GL_POINTS) gl.PointSize(Math.Max(1f, size));
            gl.DrawArrays(primitive, 0, positions.Length / 3);

            EndGeometryDraw(gl, previous, vertexColorAttribute: false);
        }

        /// <summary>
        /// Common prologue for a shared-geometry draw: compiles/acquires the geometry program,
        /// allocates its VAO/VBO on first use, and captures the GL state the draw may change.
        /// Returns <c>false</c> when the program is unavailable, in which case nothing was bound
        /// and nothing needs restoring.
        /// </summary>
        private bool BeginGeometryDraw(OpenGL gl, out RenderStateSnapshot previous)
        {
            previous = default(RenderStateSnapshot);
            if (!EnsureGeometryProgram(gl)) return false;
            EnsureGeometryBuffers(gl);
            previous = CaptureRenderState(gl);
            return true;
        }

        /// <summary>
        /// Common epilogue for a shared-geometry draw: releases the geometry VAO/VBO bindings and
        /// the program, then restores the state captured by <see cref="BeginGeometryDraw"/>.
        /// </summary>
        /// <param name="vertexColorAttribute">
        /// <c>true</c> when the draw enabled attribute 1 for per-vertex colours.
        /// </param>
        private void EndGeometryDraw(OpenGL gl, RenderStateSnapshot previous, bool vertexColorAttribute)
        {
            if (vertexColorAttribute) gl.DisableVertexAttribArray(1);
            gl.DisableVertexAttribArray(0);
            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.UseProgram(0);
            RestoreRenderState(gl, previous);
        }

        /// <summary>
        /// Draws 2D pixel coordinates using an orthographic projection. Depth testing
        /// and depth writes are disabled for the call, then restored to their prior state.
        /// </summary>
        internal void DrawScreenGeometry(OpenGL gl, uint primitive, float[] positions, Color color,
            float width = 1f)
        {
            if (positions == null || positions.Length < 2) return;
            RenderStateSnapshot previous;
            if (!BeginGeometryDraw(gl, out previous)) return;
            SetRenderState(gl, false, color.A < 255);
            gl.DepthMask(0);

            gl.BindVertexArray(_geometryVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _geometryVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, To3D(positions), OpenGL.GL_STREAM_DRAW);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
            gl.EnableVertexAttribArray(0);
            gl.UseProgram(_geometryProgram);
            float[] projection = Ortho(gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
            gl.UniformMatrix4(_geometryProjection, 1, true, projection);
            gl.UniformMatrix4(_geometryView, 1, true, _identity);
            gl.UniformMatrix4(_geometryModel, 1, true, _identity);
            gl.Uniform1(_geometryUseVertexColor, 0);
            gl.Uniform4(_geometryColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            if (primitive == OpenGL.GL_LINES || primitive == OpenGL.GL_LINE_STRIP ||
                primitive == OpenGL.GL_LINE_LOOP) gl.LineWidth(Math.Max(1f, width));
            gl.DrawArrays(primitive, 0, positions.Length / 2);

            EndGeometryDraw(gl, previous, vertexColorAttribute: false);
        }

        /// <summary>
        /// Draws screen-space geometry with a per-vertex RGBA colour. The interleaved
        /// position/colour data is uploaded to the shared geometry VBO for one draw.
        /// </summary>
        internal void DrawScreenGeometryColored(OpenGL gl, uint primitive, float[] positions,
            float[] colors, float width = 1f)
        {
            if (positions == null || positions.Length < 2 || colors == null
                || colors.Length < (positions.Length / 2) * 4) return;

            RenderStateSnapshot previous;
            if (!BeginGeometryDraw(gl, out previous)) return;
            SetRenderState(gl, false, true);
            gl.DepthMask(0);

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

            gl.BindVertexArray(_geometryVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _geometryVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, interleaved, OpenGL.GL_STREAM_DRAW);
            int stride = 7 * sizeof(float);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, stride, IntPtr.Zero);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 4, OpenGL.GL_FLOAT, false, stride, new IntPtr(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            gl.UseProgram(_geometryProgram);
            float[] projection = Ortho(gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
            gl.UniformMatrix4(_geometryProjection, 1, true, projection);
            gl.UniformMatrix4(_geometryView, 1, true, _identity);
            gl.UniformMatrix4(_geometryModel, 1, true, _identity);
            gl.Uniform4(_geometryColor, 1f, 1f, 1f, 1f);
            gl.Uniform1(_geometryUseVertexColor, 1);
            if (primitive == OpenGL.GL_LINES || primitive == OpenGL.GL_LINE_STRIP
                || primitive == OpenGL.GL_LINE_LOOP)
                gl.LineWidth(Math.Max(1f, width));
            gl.DrawArrays(primitive, 0, vertexCount);
            gl.Uniform1(_geometryUseVertexColor, 0);

            EndGeometryDraw(gl, previous, vertexColorAttribute: true);
        }
    }
}
