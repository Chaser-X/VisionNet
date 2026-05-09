using System.Collections.Generic;
using SharpGL;
using SharpGL.SceneGraph;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
        protected override void DoOpenGLInitialized()
        {
            base.DoOpenGLInitialized();
            OpenGL.ClearColor(0, 0, 0, 0);
            OpenGL.PointSize(2.0f);
        }

        protected override void DoOpenGLDraw(RenderEventArgs e)
        {
            if (DesignMode) return;
            base.DoOpenGLDraw(e);

            var gl = OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            ProcessPendingRelease(gl);

            ProcessResourcePool(gl);

            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LESS);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LoadIdentity();

            camera.LookAtMatrix(gl);
            Render(gl);

            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Disable(OpenGL.GL_BLEND);
        }

        private void ProcessPendingRelease(OpenGL gl)
        {
            while (_pendingRelease.TryDequeue(out var handle))
                ReleaseGLResources(gl, handle);
        }

        private void ProcessResourcePool(OpenGL gl)
        {
            lock (_resourceLock)
            {
                foreach (var kv in _resourcePool)
                {
                    var item   = kv.Key;
                    var handle = kv.Value;

                    if (item.IsDisposed) continue;

                    if (!handle.IsValid || handle.NeedsUpdate)
                    {
                        if (handle.IsValid)
                            ReleaseGLResources(gl, handle);

                        CreateGLResources(gl, item, handle);
                    }
                }
            }
        }

        private void Render(OpenGL gl)
        {
            if (!camera.Enable2DView && ShowCoordinateSystem)
                coordinationItem.Draw(gl);

            List<ICxObjRenderItem> snapshot;
            lock (_resourceLock)
                snapshot = new List<ICxObjRenderItem>(_surfaceItems);

            float globalZMin = float.MaxValue, globalZMax = float.MinValue;
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;
                if (cur.SurfaceColorMode != SurfaceColorMode.Intensity)
                {
                    if (cur.ZMin < globalZMin) globalZMin = cur.ZMin;
                    if (cur.ZMax > globalZMax) globalZMax = cur.ZMax;
                }
            }

            if (globalZMin < globalZMax)
            {
                foreach (var cur in snapshot)
                {
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalZRange(globalZMin, globalZMax);
                }
            }

            bool anyDrawn = false;
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                GLResourceHandle handle;
                lock (_resourceLock)
                    _resourcePool.TryGetValue(cur, out handle);

                if (handle?.IsValid != true) continue;

                cur.Draw(gl, handle);
                anyDrawn = true;
            }

            if (anyDrawn && globalZMin < globalZMax)
            {
                colorBarItem.SetRange(globalZMin, globalZMax);
                colorBarItem.Draw(gl);
            }

            if (anyDrawn)
                coorTagItem.Draw(gl);

            var items = renderItem.ToArray();
            foreach (var item in items)
                item.Draw(gl);

            if (!camera.Enable2DView)
                coordinationItem.DrawScreenPositionedAxes(gl);
        }
    }
}
