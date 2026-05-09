using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
        public void ResetView(bool resetAll = true)
        {
            renderItem.ForEach(item => item.Dispose());
            renderItem.Clear();

            coordinationItem = new CxCoordinateSystemItem();
            coorTagItem = new CxCoordinationTagItem();
            colorBarItem = new CxColorBarItem();

            if (resetAll)
                ClearSurfaceItems();

            Invalidate();
        }

        public void SetViewCenter(CxPoint3D center)
        {
            camera.FocusOnPoint(new Vector3(center.X, center.Y, center.Z));
            Invalidate();
        }

        public void SetViewUpDirection(CxVector3D upDirection)
        {
            camera.SetDefaultUpView(new Vector3(upDirection.X, upDirection.Y, upDirection.Z));
        }

        protected override void DoGDIDraw(RenderEventArgs e) => base.DoGDIDraw(e);

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            camera?.LookAtMatrix(OpenGL);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            isMouseDown = true;
            camera.RotationPoint = GetNearestSurfacePoint(e.X, e.Y).Location;
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            isMouseDown = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var pos = GetNearestSurfacePoint(e.X, e.Y);
            if (pos.Location.HasValue && !isMouseDown)
            {
                coorTagItem.Visible = true;
                coorTagItem.SetCoordinates(pos.Location.Value, pos.Intensity);
            }
            else
            {
                coorTagItem.Visible = false;
            }
            base.OnMouseMove(e);
        }

        private CxPoint3D? ScreenToWorldCoordinate(int mouseX, int mouseY)
        {
            var gl = OpenGL;
            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            int adjustedY = viewport[3] - mouseY;

            byte[] depthBuffer = new byte[4];
            gl.ReadPixels(mouseX, adjustedY, 1, 1, OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, depthBuffer);
            float depth = BitConverter.ToSingle(depthBuffer, 0);

            if (Math.Abs(depth - 1.0f) < 0.00001f) return null;

            var obj = gl.UnProject(mouseX, adjustedY, depth);
            return new CxPoint3D((float)obj[0], (float)obj[1], (float)obj[2]);
        }

        private (CxPoint3D? Location, byte? Intensity) GetNearestSurfacePoint(int mouseX, int mouseY)
        {
            var pos = ScreenToWorldCoordinate(mouseX, mouseY);
            if (!pos.HasValue) return (null, null);
            var world = pos.Value;

            List<ICxObjRenderItem> snapshot;
            lock (_resourceLock)
                snapshot = new List<ICxObjRenderItem>(_surfaceItems);

            if (snapshot.Count == 0) return (null, null);

            CxPoint3D? bestPoint = null;
            byte? bestIntensity = null;
            float bestDist = float.MaxValue;

            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                if (cur is CxMeshItem || cur is CxMeshAdvancedItem)
                {
                    if (0f < bestDist) { bestDist = 0f; bestPoint = world; bestIntensity = null; }
                    continue;
                }

                CxSurface surface = null;
                if      (cur is CxSurfaceItem        si) surface = si.Surface;
                else if (cur is CxSurfaceAdvancedItem ai) surface = ai.Surface;
                if (surface == null) continue;

                if (surface.Type != SurfaceType.Surface)
                {
                    if (0f < bestDist) { bestDist = 0f; bestPoint = world; bestIntensity = null; }
                    continue;
                }

                int xi = (int)((world.X - surface.XOffset) / surface.XScale);
                int yi = (int)((world.Y - surface.YOffset) / surface.YScale);
                if (xi < 0 || xi >= surface.Width || yi < 0 || yi >= surface.Length) continue;

                float threshold = 5 * (surface.XScale * surface.XScale
                                     + surface.YScale * surface.YScale
                                     + surface.ZScale * surface.ZScale);

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = xi + dx, ny = yi + dy;
                        if (nx < 0 || nx >= surface.Width || ny < 0 || ny >= surface.Length) continue;

                        int idx = ny * surface.Width + nx;
                        float z = surface.Data[idx] == -32768
                            ? float.NegativeInfinity
                            : surface.ZOffset + surface.Data[idx] * surface.ZScale;
                        if (float.IsInfinity(z)) continue;

                        float x = surface.XOffset + nx * surface.XScale;
                        float y = surface.YOffset + ny * surface.YScale;
                        float d = (x - world.X) * (x - world.X)
                                + (y - world.Y) * (y - world.Y)
                                + (z - world.Z) * (z - world.Z);

                        if (d < bestDist && d < threshold)
                        {
                            bestDist = d;
                            bestPoint = new CxPoint3D(x, y, z);
                            bestIntensity = (surface.Intensity != null && surface.Intensity.Length > idx)
                                ? surface.Intensity[idx] : (byte?)null;
                        }
                    }
                }
            }

            return (bestPoint, bestIntensity);
        }

        private void d2DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = !mi.Checked;
            camera.Enable2DView = mi.Checked;
            Box3D? combined;
            lock (_resourceLock) combined = GetCombinedBoundingBox();
            camera?.FitView(combined);
        }

        private void toolStripMenuItem_ViewModeClick(object sender, EventArgs e)
        {
            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            camera.ViewMode = (ViewMode)Enum.Parse(typeof(ViewMode), mi.Text);
            Box3D? combined;
            lock (_resourceLock) combined = GetCombinedBoundingBox();
            camera?.FitView(combined);
        }

        private void toolStripMenuItem_SurfaceModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceMode = (SurfaceMode)Enum.Parse(typeof(SurfaceMode), mi.Text);
        }

        private void toolStripMenuItem_SurfaceColorModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceColorMode = (SurfaceColorMode)Enum.Parse(typeof(SurfaceColorMode), mi.Text);
        }
    }
}
