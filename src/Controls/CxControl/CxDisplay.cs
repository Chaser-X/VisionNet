using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
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
        // 渲染数据
        private CxSurfaceItem surfaceItem = null;
        private CxCoordinateSystemItem coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem colorBarItem = new CxColorBarItem();
        private CxCoordinationTagItem coorTagItem = new CxCoordinationTagItem();
        private List<IRenderItem> renderItem = new List<IRenderItem>();

        //相机属性
        public CxTrackBallCamera Camera => camera;

        //private ViewMode pViewMode = ViewMode.Top;
        //public ViewMode ViewMode
        //{
        //    get { return pViewMode; }
        //    set
        //    {
        //        pViewMode = value;
        //        if (camera != null)
        //            camera.ViewMode = value;
        //    }
        //}

        private SurfaceMode pSufaceMode = VisionNet.Controls.SurfaceMode.PointCloud;
        public SurfaceMode SurfaceMode
        {
            get { return pSufaceMode; }
            set
            {
                pSufaceMode = value;
                if (surfaceItem != null)
                    surfaceItem.SurfaceMode = value;
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
            }
        }

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

                foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                {
                    var tripItem = (ToolStripMenuItem)item;
                    tripItem.Checked = tripItem.Text == camera.ViewMode.ToString();
                }
                SurfaceMode = surfaceMode;
                foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                {
                    var tripItem = (ToolStripMenuItem)item;
                    tripItem.Checked = tripItem.Text == SurfaceMode.ToString();
                }
                SurfaceColorMode = surfaceColorMode;
                foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                {
                    var tripItem = (ToolStripMenuItem)item;
                    tripItem.Checked = tripItem.Text == SurfaceColorMode.ToString();
                }
            }
        }
        #region 操作方法
        /// <summary>
        /// 设置点云数据并自适应Z显示范围
        /// </summary>
        public void SetPointCloud(CxSurface inpointCloud)
        {
            var tempSuface = new CxSurface();
            var size = inpointCloud.Width * inpointCloud.Length;
            if (size > 10000000)
            {
                var points = inpointCloud.ToPoints();
                var ratio = points.Length / 6000000.0f;
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
            //camera.ViewMode = ViewMode;
            camera.FitView(surfaceItem.BoundingBox); // 调整视图以适应点云数据
        }
        /// <summary>
        /// 添加线段
        /// </summary>
        public void SetSegment(Segment3D segment, Color color)
        {
            var segmentItem = renderItem.Find(x => x.GetType() == typeof(CxSegment3DItem));
            if (segmentItem == null)
            {
                segmentItem = new CxSegment3DItem(new Dictionary<Segment3D, Color> { { segment, color } });
                renderItem.Add(segmentItem);
            }
            else
            {
                var cxsegmentItem = segmentItem as CxSegment3DItem;
                if (cxsegmentItem.SegmentColors.ContainsKey(segment))
                {
                    cxsegmentItem.SegmentColors[segment] = color;
                }
                else
                {
                    cxsegmentItem.SegmentColors.Add(segment, color);
                }
            }
        }
        //添加点
        public void SetPoint(CxPoint3D point, Color color)
        {
            var pointItem = renderItem.Find(x => x.GetType() == typeof(CxPoint3DItem));
            if (pointItem == null)
            {
                pointItem = new CxPoint3DItem(new Dictionary<CxPoint3D, Color> { { point, color } });
                renderItem.Add(pointItem);
            }
            else
            {
                var cxpointItem = pointItem as CxPoint3DItem;
                if (cxpointItem.PointColors.ContainsKey(point))
                {
                    cxpointItem.PointColors[point] = color;
                }
                else
                {
                    cxpointItem.PointColors.Add(point, color);
                }
            }
        }
        //添加多边形
        public void SetPolygon(Polygon3D polygon, Color color)
        {
            var polygonItem = renderItem.Find(x => x.GetType() == typeof(CxPolygon3DItem));
            if (polygonItem == null)
            {
                polygonItem = new CxPolygon3DItem(new Dictionary<Polygon3D, Color> { { polygon, color } });
                renderItem.Add(polygonItem);
            }
            else
            {
                var cxpolygonItem = polygonItem as CxPolygon3DItem;
                if (cxpolygonItem.PolygonColors.ContainsKey(polygon))
                {
                    cxpolygonItem.PolygonColors[polygon] = color;
                }
                else
                {
                    cxpolygonItem.PolygonColors.Add(polygon, color);
                }
            }
        }
        //添加平面
        public void SetPlane(Plane3D plane, Color color)
        {
            var planeItem = renderItem.Find(x => x.GetType() == typeof(CxPlane3DItem));
            if (planeItem == null)
            {
                planeItem = new CxPlane3DItem(new Dictionary<Plane3D, Color> { { plane, color } });
                renderItem.Add(planeItem);
            }
            else
            {
                var cxplaneItem = planeItem as CxPlane3DItem;
                if (cxplaneItem.PlaneColors.ContainsKey(plane))
                {
                    cxplaneItem.PlaneColors[plane] = color;
                }
                else
                {
                    cxplaneItem.PlaneColors.Add(plane, color);
                }
            }
        }
        //添加Box3D
        public void SetBox(Box3D box, Color color)
        {
            var boxItem = renderItem.Find(x => x.GetType() == typeof(CxBox3DItem));
            if (boxItem == null)
            {
                boxItem = new CxBox3DItem(new Dictionary<Box3D, Color> { { box, color } });
                renderItem.Add(boxItem);
            }
            else
            {
                var cxboxItem = boxItem as CxBox3DItem;
                if (cxboxItem.BoxColors.ContainsKey(box))
                {
                    cxboxItem.BoxColors[box] = color;
                }
                else
                {
                    cxboxItem.BoxColors.Add(box, color);
                }
            }
        }

        //添加Textinfo
        public void SetTextInfo(TextInfo textInfo, Color color)
        {
            var textItem = renderItem.Find(x => x.GetType() == typeof(CxTextInfoItem));
            if (textItem == null)
            {
                textItem = new CxTextInfoItem(new Dictionary<TextInfo, Color> { { textInfo, color } });
                renderItem.Add(textItem);
            }
            else
            {
                var cxtextItem = textItem as CxTextInfoItem;
                if (cxtextItem.TextInfoColors.ContainsKey(textInfo))
                {
                    cxtextItem.TextInfoColors[textInfo] = color;
                }
                else
                {
                    cxtextItem.TextInfoColors.Add(textInfo, color);
                }
            }
        }

        #endregion
        #region 渲染方法
        /// <summary>
        /// 渲染图元
        /// </summary>
        private void Render(OpenGL gl)
        {
            coordinationItem.DrawScreenPositionedAxes(gl);
            coordinationItem.Draw(gl);

            surfaceItem?.Draw(gl);
            if (surfaceItem != null &&
                surfaceItem.SurfaceColorMode != SurfaceColorMode.Intensity)
            {
                colorBarItem.SetRange(surfaceItem.ZMin, surfaceItem.ZMax);
                colorBarItem.Draw(gl);
            }
            foreach (var item in renderItem)
            {
                item.Draw(gl);
            }

            if (surfaceItem != null)
                coorTagItem.Draw(gl);
        }


        /// <summary>
        /// 清空图元
        /// </summary>
        public void ResetView()
        {
            renderItem.Clear();
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
            // 启用深度测试
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LESS);
            // 启用混合
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LoadIdentity();
            // 设置投影矩阵
            camera.LookAtMatrix(gl);
            // 应用视图变换并绘制所有元素
            Render(gl);
            // 禁用深度测试
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            // 禁用混合
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
        #endregion 渲染方法

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
        private void lineWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var state = float.TryParse(lineWidthToolStripTextBox.Text, out float lineWidth);
            if (!state)
            {
                lineWidth = 1;
                lineWidthToolStripTextBox.Text = "1";
            }
            foreach (var item in renderItem)
                item.LineWidth = lineWidth;
        }
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
            var pos = GetPointCloudCoordinate(e.X, e.Y);
            if (pos.HasValue && !isMouseDown)
            {
                coorTagItem.Visible = true;
                coorTagItem.SetCoordinates(pos.Value);
            }
            else
                coorTagItem.Visible = false;
            base.OnMouseMove(e);
        }
        private CxPoint3D? GetPointCloudCoordinate(int mouseX, int mouseY)
        {
            OpenGL gl = OpenGL;
            // 获取当前视口
            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            // 调整Y坐标（OpenGL坐标系原点在左下角）
            int adjustedY = viewport[3] - mouseY;
            // 为深度值创建byte数组
            byte[] depthBuffer = new byte[4]; // 单个float值需要4个字节
            // 读取深度值到byte数组
            gl.ReadPixels(mouseX, adjustedY, 1, 1, OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, depthBuffer);
            // 将byte数组转换为float
            float depth = BitConverter.ToSingle(depthBuffer, 0);
            // 在转换坐标之前检查深度值
            if (Math.Abs(depth - 1.0f) < 0.00001f)
            {
                // 深度值为1.0，表示点击了背景
                return null; // 或者返回一个特殊值表示无效点击
            }
            // 将屏幕坐标转换为世界坐标
            var obj = gl.UnProject((double)mouseX, (double)adjustedY, (double)depth);
            return new CxPoint3D((float)obj[0], (float)obj[1], (float)obj[2]);
        }

    }
}


