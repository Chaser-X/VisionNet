using OpenTK.Graphics.OpenGL;

namespace VisionNet.Controls
{
    internal sealed partial class ModernGLRenderer
    {

        /// <summary>Reads mutable GL state that overlay draws temporarily change.</summary>
        private RenderStateSnapshot CaptureRenderState()
        {
            return _frameStateCaptured ? _frameState : CaptureRenderStateUncached();
        }

        /// <summary>
        /// Captures the GL state a draw may change, so the caller can restore it afterwards.
        /// During a <see cref="BeginFrame"/> window this returns the frame-start snapshot, which
        /// is the state every item in the frame should observe; outside a frame it queries GL.
        /// </summary>
        internal RenderStateSnapshot CaptureState() => CaptureRenderState();

        /// <summary>Restores state captured by <see cref="CaptureState"/>.</summary>
        internal void RestoreState(RenderStateSnapshot state)
            => RestoreRenderState(state);

        private RenderStateSnapshot CaptureRenderStateUncached()
        {
            GL.GetFloat(GetPName.PointSize, _pointSizeQuery);
            GL.GetFloat(GetPName.LineWidth, _lineWidthQuery);
            return new RenderStateSnapshot
            {
                DepthTest = GL.IsEnabled(EnableCap.DepthTest),
                DepthWrite = GL.GetBoolean(GetPName.DepthWritemask),
                Blend = GL.IsEnabled(EnableCap.Blend),
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
        private static void SetRenderState(bool depth, bool blend)
        {
            if (depth) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
            if (blend)
            {
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.DepthMask(false);
            }
            else
            {
                GL.Disable(EnableCap.Blend);
            }
        }

        /// <summary>Restores state captured before an overlay draw.</summary>
        private static void RestoreRenderState(RenderStateSnapshot state)
        {
            if (state.DepthTest) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(state.DepthWrite);
            if (state.Blend) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
            GL.PointSize(state.PointSize);
            GL.LineWidth(state.LineWidth);
        }
    }
}
