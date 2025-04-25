using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Cameras;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public partial class CxDisplay : OpenGLControl, IDisposable
    {
        private CxTrackBallCamera camera;
        bool isMouseDown = false;
        // ��Ⱦ����
        private ICxObjRenderItem surfaceItem = null;
        private CxCoordinateSystemItem coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem colorBarItem = new CxColorBarItem();
        private CxCoordinationTagItem coorTagItem = new CxCoordinationTagItem();
        private ConcurrentBag<IRenderItem> renderItem = new ConcurrentBag<IRenderItem>();
        //�������
        public CxTrackBallCamera Camera => camera;

        private SurfaceMode pSufaceMode = VisionNet.Controls.SurfaceMode.PointCloud;
        public SurfaceMode SurfaceMode
        {
            get { return pSufaceMode; }
            set
            {
                pSufaceMode = value;
                if (surfaceItem != null)
                    surfaceItem.SurfaceMode = value;
                updataMenuItem();
            }
        }
        private SurfaceColorMode pSurfaceColorMode = VisionNet.Controls.SurfaceColorMode.ColorWithIntensity;
        public SurfaceColorMode SurfaceColorMode
        {
            get { return pSurfaceColorMode; }
            set
            {
                pSurfaceColorMode = value;
                if (surfaceItem != null)
                    surfaceItem.SurfaceColorMode = value;
                updataMenuItem();
            }
        }
        //�Ƿ���ʾ����ϵ
        public bool ShowCoordinateSystem { get; set; } = false;

        public CxDisplay() : this(ViewMode.Top, SurfaceMode.PointCloud, SurfaceColorMode.ColorWithIntensity)
        {

        }
        public CxDisplay(ViewMode viewMode = ViewMode.Top,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.ColorWithIntensity)
        {
            if (!DesignMode)
            {
                camera = new CxTrackBallCamera(this);
                camera.ViewMode = viewMode;
                InitializeComponent();
                SurfaceMode = surfaceMode;
                SurfaceColorMode = surfaceColorMode; 
                updataMenuItem();
            }
        }
        //ˢ��menu
        private void updataMenuItem()
        {
            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
            {
                var tripItem = (ToolStripMenuItem)item;
                tripItem.Checked = tripItem.Text == camera.ViewMode.ToString();
            }
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
            {
                var tripItem = (ToolStripMenuItem)item;
                tripItem.Checked = tripItem.Text == pSufaceMode.ToString();
            }
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
            {
                var tripItem = (ToolStripMenuItem)item;
                tripItem.Checked = tripItem.Text == pSurfaceColorMode.ToString();
            }
        }
        #region ��������
        /// <summary>
        /// ���õ������ݲ�����ӦZ��ʾ��Χ
        /// </summary>
        public void SetPointCloud(CxSurface inpointCloud)
        {
            var tempSuface = new CxSurface();
            var size = inpointCloud.Width * inpointCloud.Length;
            if (size > 10000000)
            {
                var points = inpointCloud.ToPoints();
                var ratio = points.Length / 10000000F;
                var width = inpointCloud.Width / ratio;
                var height = inpointCloud.Length / ratio;
                var xScale = inpointCloud.XScale * ratio;
                var yScale = inpointCloud.YScale * ratio;
                tempSuface = VisionOperator.UniformSuface(points, inpointCloud.Intensity, (int)width, (int)height, xScale, yScale, inpointCloud.ZScale, inpointCloud.XOffset, inpointCloud.YOffset, inpointCloud.ZOffset);
            }
            else
            {
                tempSuface = inpointCloud;
            }

            surfaceItem = new CxSurfaceItem(tempSuface ?? new CxSurface(), SurfaceMode, SurfaceColorMode);
            camera.FitView(surfaceItem.BoundingBox); // ������ͼ����Ӧ��������
        }
        //���Mesh
        public void SetMesh(CxMesh mesh)
        {
            surfaceItem = new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode);
            camera.FitView(surfaceItem.BoundingBox);
        }

        /// <summary>
        /// ����߶�
        /// </summary>
        public void SetSegment(Segment3D[] segment, Color color, float size = 1.0f)
        {
            var segmentItem = new CxSegment3DItem(segment, color, size);
            renderItem.Add(segmentItem);
        }
        //��ӵ�
        public void SetPoint(CxPoint3D[] point, Color color, float size = 1.0f, PointShape shape = PointShape.Point)
        {
            var pointItem = new CxPoint3DItem(point, color, size, shape);
            renderItem.Add(pointItem);
        }
        //��Ӷ����
        public void SetPolygon(Polygon3D[] polygon, Color color, float size = 1.0f)
        {
            var polygonItem = new CxPolygon3DItem(polygon, color, size);
            renderItem.Add(polygonItem);
        }
        //���ƽ��
        public void SetPlane(Plane3D[] plane, Color color, float size = 100.0f)
        {
            var planeItem = new CxPlane3DItem(plane, color, size);
            renderItem.Add(planeItem);
        }
        //���Box3D
        public void SetBox(Box3D[] box, Color color, float size = 1.0f)
        {
            var boxItem = new CxBox3DItem(box, color, size);
            renderItem.Add(boxItem);
        }
        //���Textinfo
        public void SetTextInfo(TextInfo[] textInfo, Color color, float size = 1.0f)
        {
            var textItem = new CxTextInfoItem(textInfo, color, size);
            renderItem.Add(textItem);
        }
        #endregion
        #region ��Ⱦ����
        /// <summary>
        /// ��ȾͼԪ
        /// </summary>
        private void Render(OpenGL gl)
        {
            if (!camera.Enable2DView && ShowCoordinateSystem)
                coordinationItem.Draw(gl);
            surfaceItem?.Draw(gl);
            if (surfaceItem != null &&
                surfaceItem.SurfaceColorMode != SurfaceColorMode.Intensity)
            {
                colorBarItem.SetRange(surfaceItem.ZMin, surfaceItem.ZMax);
                colorBarItem.Draw(gl);
            }
            var items = renderItem.ToArray();
            foreach (var item in items)
            {
                item.Draw(gl); 
            }
            if (surfaceItem != null)
                coorTagItem.Draw(gl);
            if (!camera.Enable2DView)
                coordinationItem.DrawScreenPositionedAxes(gl);
        }
        /// <summary>
        /// ���ͼԪ
        /// </summary>
        public void ResetView()
        {
            while (!renderItem.IsEmpty)
            {
                renderItem.TryTake(out _);
            }
            Invalidate();
        }
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
            OpenGL gl = OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            // ������Ȳ���
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LESS);
            // ���û��
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LoadIdentity();
            // ����ͶӰ����
            camera.LookAtMatrix(gl);
            // Ӧ����ͼ�任����������Ԫ��
            Render(gl);
            // ������Ȳ���
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            // ���û��
            gl.Disable(OpenGL.GL_BLEND);
        }
        protected override void DoGDIDraw(RenderEventArgs e)
        {
            base.DoGDIDraw(e);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            camera.LookAtMatrix(OpenGL);
        }
        #endregion ��Ⱦ����
        private void d2DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedItem = (ToolStripMenuItem)sender;
            selectedItem.Checked = !selectedItem.Checked;
            camera.Enable2DView = selectedItem.Checked;
            camera?.FitView(surfaceItem?.BoundingBox);
        }
        private void toolStripMenuItem_ViewModeClick(object sender, EventArgs e)
        {
            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var selectedItem = (ToolStripMenuItem)sender;
            selectedItem.Checked = true;
            camera.ViewMode = (ViewMode)Enum.Parse(typeof(ViewMode), selectedItem.Text);
            camera?.FitView(surfaceItem?.BoundingBox);
        }
        private void toolStripMenuItem_SurfaceModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var selectedItem = (ToolStripMenuItem)sender;
            selectedItem.Checked = true;
            SurfaceMode = (SurfaceMode)Enum.Parse(typeof(SurfaceMode), selectedItem.Text);

        }
        private void toolStripMenuItem_SurfaceColorModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var selectedItem = (ToolStripMenuItem)sender;
            selectedItem.Checked = true;
            SurfaceColorMode = (SurfaceColorMode)Enum.Parse(typeof(SurfaceColorMode), selectedItem.Text);
        }
        //private void lineWidthToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    var state = float.TryParse(lineWidthToolStripTextBox.Text, out float lineWidth);
        //    if (!state)
        //    {
        //        lineWidth = 1;
        //        lineWidthToolStripTextBox.Text = "1";
        //    }
        //    foreach (var item in renderItem)
        //        item.LineWidth = lineWidth;
        //}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            isMouseDown = true;
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
                coorTagItem.Visible = false;
            base.OnMouseMove(e);
        }
        private CxPoint3D? ScreenToWorldCoordinate(int mouseX, int mouseY)
        {
            OpenGL gl = OpenGL;
            // ��ȡ��ǰ�ӿ�
            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            // ����Y���꣨OpenGL����ϵԭ�������½ǣ�
            int adjustedY = viewport[3] - mouseY;
            // Ϊ���ֵ����byte����
            byte[] depthBuffer = new byte[4]; // ����floatֵ��Ҫ4���ֽ�
            // ��ȡ���ֵ��byte����
            gl.ReadPixels(mouseX, adjustedY, 1, 1, OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, depthBuffer);
            // ��byte����ת��Ϊfloat
            float depth = BitConverter.ToSingle(depthBuffer, 0);
            // ��ת������֮ǰ������ֵ
            if (Math.Abs(depth - 1.0f) < 0.00001f)
            {
                // ���ֵΪ1.0����ʾ����˱���
                return null; // ���߷���һ������ֵ��ʾ��Ч���
            }
            // ����Ļ����ת��Ϊ��������
            var obj = gl.UnProject((double)mouseX, (double)adjustedY, (double)depth);
            return new CxPoint3D((float)obj[0], (float)obj[1], (float)obj[2]); 
        }
        private (CxPoint3D? Location, byte? Intensity) GetNearestSurfacePoint(int mouseX, int mouseY)
        {

            var obj = ScreenToWorldCoordinate(mouseX, mouseY);
            if (!obj.HasValue)
                return (null, null);
            var worldObj = obj.Value;
            //meshͼԪ
            var tempmeshItem = surfaceItem as CxMeshItem;
            if (tempmeshItem != null)
                return (worldObj, null);
            //surfaceͼԪ
            var tempsurfaceItem = surfaceItem as CxSurfaceItem;
            if (tempsurfaceItem == null || tempsurfaceItem.Surface == null)
                return (null, null);
            var surface = tempsurfaceItem.Surface;
            // ��ʼ����������С����
            CxPoint3D? nearestPoint = null;
            byte? nearestIntensity = null;
            float minDistanceSquared = float.MaxValue;
            if (surface.Type == SurfaceType.Surface)
            {
                // ���� worldObj �������е�����
                int xIndex = (int)((worldObj.X - surface.XOffset) / surface.XScale);
                int yIndex = (int)((worldObj.Y - surface.YOffset) / surface.YScale);
                // ��������Ƿ��ڷ�Χ��
                if (xIndex < 0 || xIndex >= surface.Width || yIndex < 0 || yIndex >= surface.Length)
                    return (null, null);
                var minDis = 3 * (surface.XScale * surface.XScale +
                             surface.YScale * surface.YScale +
                             surface.ZScale * surface.ZScale);
                // ���������ĵ㣨3x3 ����
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = xIndex + dx;
                        int ny = yIndex + dy;

                        // ���������Ƿ��ڷ�Χ��
                        if (nx < 0 || nx >= surface.Width || ny < 0 || ny >= surface.Length)
                            continue;

                        // ��ȡ�������
                        int index = ny * surface.Width + nx;
                        // ������ʵ������
                        float x = surface.XOffset + nx * surface.XScale;
                        float y = surface.YOffset + ny * surface.YScale;
                        float z = surface.Data[index] == -32768
                           ? float.NegativeInfinity
                           : surface.ZOffset + surface.Data[index] * surface.ZScale;

                        if (float.IsInfinity(z)) // ������Ч��
                            continue;

                        // ����ŷ����þ����ƽ��
                        float distanceSquared = (x - worldObj.X) * (x - worldObj.X) +
                                                (y - worldObj.Y) * (y - worldObj.Y) +
                                                (z - worldObj.Z) * (z - worldObj.Z);

                        // ���������
                        if (distanceSquared < minDistanceSquared && distanceSquared < minDis)
                        {
                            minDistanceSquared = distanceSquared;
                            nearestPoint = new CxPoint3D(x, y, z);
                            if (surface.Intensity != null && surface.Intensity.Length != 0)
                                nearestIntensity = surface.Intensity[index];
                        }
                    }
                }
            }
            else
            {
                //PointCloud����
                nearestPoint = worldObj;
                nearestIntensity = null;
            }
            return (nearestPoint, nearestIntensity);
        }
    }
}


