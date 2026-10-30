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
    internal sealed partial class ModernOverlayRenderer
    {

        /// <summary>Reads mutable GL state that overlay draws temporarily change.</summary>
        private RenderStateSnapshot CaptureRenderState(OpenGL gl)
        {
            return _frameStateCaptured ? _frameState : CaptureRenderStateUncached(gl);
        }

        /// <summary>
        /// Captures the GL state a draw may change, so the caller can restore it afterwards.
        /// During a <see cref="BeginFrame"/> window this returns the frame-start snapshot, which
        /// is the state every item in the frame should observe; outside a frame it queries GL.
        /// </summary>
        internal RenderStateSnapshot CaptureState(OpenGL gl) => CaptureRenderState(gl);

        /// <summary>Restores state captured by <see cref="CaptureState"/>.</summary>
        internal void RestoreState(OpenGL gl, RenderStateSnapshot state)
            => RestoreRenderState(gl, state);

        private RenderStateSnapshot CaptureRenderStateUncached(OpenGL gl)
        {
            gl.GetFloat(OpenGL.GL_POINT_SIZE, _pointSizeQuery);
            gl.GetFloat(OpenGL.GL_LINE_WIDTH, _lineWidthQuery);
            return new RenderStateSnapshot
            {
                DepthTest = gl.IsEnabled(OpenGL.GL_DEPTH_TEST),
                DepthWrite = GetDepthWrite(gl),
                Blend = gl.IsEnabled(OpenGL.GL_BLEND),
                PointSize = _pointSizeQuery[0],
                LineWidth = _lineWidthQuery[0]
            };
        }

        /// <summary>
        /// Applies the depth/blend state required by one overlay draw. Translucent draws
        /// (<paramref name="blend"/>) also stop writing depth, otherwise the translucent
        /// surface would occlude geometry drawn later and could not blend with what is
        /// already in the framebuffer. <see cref="RestoreRenderState"/> puts it back.
        /// </summary>
        private static void SetRenderState(OpenGL gl, bool depth, bool blend)
        {
            if (depth) gl.Enable(OpenGL.GL_DEPTH_TEST); else gl.Disable(OpenGL.GL_DEPTH_TEST);
            if (blend)
            {
                gl.Enable(OpenGL.GL_BLEND);
                gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
                gl.DepthMask(0);
            }
            else
            {
                gl.Disable(OpenGL.GL_BLEND);
            }
        }

        /// <summary>Restores state captured before an overlay draw.</summary>
        private static void RestoreRenderState(OpenGL gl, RenderStateSnapshot state)
        {
            if (state.DepthTest) gl.Enable(OpenGL.GL_DEPTH_TEST); else gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.DepthMask((byte)(state.DepthWrite ? 1 : 0));
            if (state.Blend) gl.Enable(OpenGL.GL_BLEND); else gl.Disable(OpenGL.GL_BLEND);
            gl.PointSize(state.PointSize);
            gl.LineWidth(state.LineWidth);
        }

        /// <summary>Reads the depth write mask through SharpGL's boolean query API.</summary>
        private bool GetDepthWrite(OpenGL gl)
        {
            gl.GetBooleanv(OpenGL.GL_DEPTH_WRITEMASK, _depthWriteQuery);
            return _depthWriteQuery[0] != 0;
        }
    }
}
