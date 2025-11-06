using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http.Headers;
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
        private CxAdvancedTrackBallCamera camera;
        bool isMouseDown = false;
        // 渲染数据
        private ConcurrentQueue<ICxObjRenderItem> surfaceItemBag = new ConcurrentQueue<ICxObjRenderItem>();
        private ICxObjRenderItem surfaceItem = null;
        private CxCoordinateSystemItem coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem colorBarItem = new CxColorBarItem();
        private CxCoordinationTagItem coorTagItem = new CxCoordinationTagItem();
        private List<IRenderItem> renderItem = new List<IRenderItem>();
        //相机属性
        public CxAdvancedTrackBallCamera Camera => camera;
        public ViewMode SurfaceViewMode
        {
            get { return camera.ViewMode; }
            set
            {
                if (camera.ViewMode != value)
                {
                    camera.ViewMode = value;
                    updataMenuItem();
                }
            }
        }

        private SurfaceMode pSufaceMode = VisionNet.Controls.SurfaceMode.PointCloud;
        public SurfaceMode SurfaceMode
        {
            get { return pSufaceMode; }
            set
            {
                if (pSufaceMode != value)
                {
                    pSufaceMode = value;
                    updataMenuItem();
                }
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

                if (pSurfaceColorMode != value)
                {
                    pSurfaceColorMode = value;
                    updataMenuItem();
                }
                if (surfaceItem != null)
                    surfaceItem.SurfaceColorMode = value;
            }
        }
        //是否显示坐标系
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
                InitializeComponent();
                camera = new CxAdvancedTrackBallCamera(this);
                camera.ViewMode = viewMode;
                SurfaceMode = surfaceMode;
                SurfaceColorMode = surfaceColorMode;
                updataMenuItem();
                // RenderTrigger = RenderTrigger.Manual;
            }
        }
        //刷新menu
        private void updataMenuItem()
        {
            //是否跨线程调用
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => updataMenuItem()));
                return;
            }
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
        #region 操作方法
        /// <summary>
        /// 设置点云数据并自适应Z显示范围
        /// </summary>
        public void SetPointCloud(CxSurface inpointCloud)
        {
            var tempSuface = new CxSurface();
            var size = inpointCloud.Width * inpointCloud.Length;
            if (size > 100000000)
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
            /*
            if (surfaceItem != null)
            {
                surfaceItem.OnDisposed += () =>
                {
                    surfaceItem = new CxSurfaceItem(tempSuface ?? new CxSurface(), SurfaceMode, SurfaceColorMode);
                    camera.FitView(surfaceItem.BoundingBox); // 调整视图以适应点云数据
                    Invalidate();
                };
                surfaceItem.Dispose(); // 释放旧的图元资源
                //DoOpenGLDraw(new RenderEventArgs(this.CreateGraphics()));
                Invalidate();
                //surfaceItem = new CxSurfaceItem(tempSuface, SurfaceMode, SurfaceColorMode);
                //camera.FitView(surfaceItem.BoundingBox); // 调整视图以适应点云数据
                //Invalidate();
            }
            else
            {
                surfaceItem = new CxSurfaceItem(tempSuface, SurfaceMode, SurfaceColorMode);
                camera.FitView(surfaceItem.BoundingBox); // 调整视图以适应点云数据
                Invalidate();
            }
            */

            var tempsurfaceItem = new CxSurfaceItem(tempSuface, SurfaceMode, SurfaceColorMode);
            camera.FitView(tempsurfaceItem.BoundingBox); // 调整视图以适应点云数据
            surfaceItemBag.Enqueue(tempsurfaceItem);
            Invalidate();
        }
        //添加Mesh
        public void SetMesh(CxMesh mesh)
        {
            //    if (InvokeRequired)
            //    {
            //        BeginInvoke(new Action(() => SetMesh(mesh)));
            //        return;
            //    }
            /*   if (surfaceItem != null)
               {
                   surfaceItem.OnDisposed += () =>
                   {
                       surfaceItem = new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode);
                       camera.FitView(surfaceItem.BoundingBox);
                       Invalidate();
                   };

                   surfaceItem.Dispose(); // 释放旧的图元资源
                   //DoOpenGLDraw(new RenderEventArgs(this.CreateGraphics()));
                   //Invalidate();
               }
               else
               {
                   surfaceItem = new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode);
                   camera.FitView(surfaceItem.BoundingBox);
                   Invalidate();
               }*/
            var tempsurfaceItem = new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode);
            camera.FitView(tempsurfaceItem.BoundingBox);
            surfaceItemBag.Enqueue(tempsurfaceItem);
            Invalidate();
        }
        //添加SurfaceAdvancedItem
        public void SetSurfaceAdvancedItem(CxSurface surfaceItem)
        {
            var tempsurfaceItem = new CxSurfaceAdvancedItem(surfaceItem, SurfaceMode, SurfaceColorMode,2000000);
            surfaceItemBag.Enqueue(tempsurfaceItem);
            camera.FitView(tempsurfaceItem.BoundingBox);
            Invalidate();
        }
        //添加MeshAdvancedItem
        public void SetMeshAdvancedItem(CxMesh meshItem)
        {
            var tempsurfaceItem = new CxMeshAdvancedItem(meshItem, SurfaceMode, SurfaceColorMode);
            surfaceItemBag.Enqueue(tempsurfaceItem);
            camera.FitView(tempsurfaceItem.BoundingBox);
            Invalidate();
        }
        /// <summary>
        /// 添加线段
        /// </summary>
        public void SetSegment(Segment3D[] segment, Color color, float size = 1.0f)
        {
            var segmentItem = new CxSegment3DItem(segment, color, size);
            renderItem.Add(segmentItem);
            Invalidate();
        }
        //添加点
        public void SetPoint(CxPoint3D[] point, Color color, float size = 1.0f, PointShape shape = PointShape.Point)
        {
            var pointItem = new CxPoint3DItem(point, color, size, shape);
            renderItem.Add(pointItem);
            Invalidate();
        }
        //添加多边形
        public void SetPolygon(Polygon3D[] polygon, Color color, float size = 1.0f)
        {
            var polygonItem = new CxPolygon3DItem(polygon, color, size);
            renderItem.Add(polygonItem);
            Invalidate();
        }
        //添加平面
        public void SetPlane(Plane3D[] plane, Color color, float size = 100.0f)
        {
            var planeItem = new CxPlane3DItem(plane, color, size);
            renderItem.Add(planeItem);
            Invalidate();
        }
        //添加Box3D
        public void SetBox(Box3D[] box, Color color, float size = 1.0f)
        {
            var boxItem = new CxBox3DItem(box, color, size);
            renderItem.Add(boxItem);
            Invalidate();
        }
        //添加Textinfo
        public void SetTextInfo(TextInfo[] textInfo, Color color)
        {
            var textItem = new CxTextInfoItem(textInfo, color, 1);
            renderItem.Add(textItem);
            Invalidate();
        }
        //添加2D文本
        public void SetText2D(Text2D[] text2Ds, Color color)
        {
            var textItem = new CxText2DItem(text2Ds, color, 1);
            renderItem.Add(textItem);
            Invalidate();
        }
        //添加自定义3D坐标系
        public void SetCoordinate3DSystem(CxCoordination3D? coordinationItem = null, float axisLength = 5)
        {
            if (!coordinationItem.HasValue)
                coordinationItem = new CxCoordination3D()
                {
                    Origin = new CxPoint3D(0, 0, 0),
                    XAxis = new CxVector3D(1, 0, 0),
                    YAxis = new CxVector3D(0, 1, 0),
                    ZAxis = new CxVector3D(0, 0, 1)
                };
            var coorItem = new CxCoordinateSystemItem(axisLength, axisLength / 50, axisLength / 10, axisLength / 25, coordinationItem);
            renderItem.Add(coorItem);
            Invalidate();
        }
        #endregion
        #region 渲染方法
        /// <summary>
        /// 渲染图元
        /// </summary>
        private void Render(OpenGL gl)
        {
            if (!camera.Enable2DView && ShowCoordinateSystem)
                coordinationItem.Draw(gl);
            
            if (surfaceItemBag.Count == 0)
                surfaceItem = null;
            else if (surfaceItemBag.Count == 1)
                surfaceItemBag.TryPeek(out surfaceItem);
            else if (surfaceItemBag.Count > 1)
            {
                surfaceItemBag.TryDequeue(out ICxObjRenderItem tempsurfaceItem);
                tempsurfaceItem.Dispose();
                tempsurfaceItem.Draw(gl);
                surfaceItemBag.TryPeek(out surfaceItem);
                camera.FitView(surfaceItem.BoundingBox);
            }
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
        /// 清空图元
        /// </summary>
        public void ResetView(bool resetAll = true)
        {
            //增加BeginInvorke
            //if (InvokeRequired)
            //{
            //    BeginInvoke(new Action(() => ResetView(resetAll)));
            //    return;
            //}
            renderItem.ForEach(item => item.Dispose());
            renderItem.Clear();
            coordinationItem?.Dispose();
            coorTagItem?.Dispose();
            colorBarItem?.Dispose();
            if (resetAll)
            {
                surfaceItem?.Dispose(); // 释放旧的图元资源
            }
            //DoOpenGLDraw(new RenderEventArgs(this.CreateGraphics()));

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
            camera?.LookAtMatrix(OpenGL);
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
            //Invalidate();
        }
        private void toolStripMenuItem_SurfaceModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var selectedItem = (ToolStripMenuItem)sender;
            selectedItem.Checked = true;
            SurfaceMode = (SurfaceMode)Enum.Parse(typeof(SurfaceMode), selectedItem.Text);
            //Invalidate();
        }
        private void toolStripMenuItem_SurfaceColorModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var selectedItem = (ToolStripMenuItem)sender;
            selectedItem.Checked = true;
            SurfaceColorMode = (SurfaceColorMode)Enum.Parse(typeof(SurfaceColorMode), selectedItem.Text);
            //Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            isMouseDown = true;
            var pos = GetNearestSurfacePoint(e.X, e.Y);
            camera.RotationPoint = pos.Location;
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            isMouseDown = false;
            //camera.RotationPoint = null;
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
        private (CxPoint3D? Location, byte? Intensity) GetNearestSurfacePoint(int mouseX, int mouseY)
        {

            var obj = ScreenToWorldCoordinate(mouseX, mouseY);
            if (!obj.HasValue)
                return (null, null);
            var worldObj = obj.Value;
            //mesh图元
            var tempmeshItem = surfaceItem as CxMeshItem;
            if (tempmeshItem != null)
                return (worldObj, null);
            var tempmeshAdvancedItem = surfaceItem as CxMeshAdvancedItem;
            if (tempmeshAdvancedItem != null)
                return (worldObj, null); // 如果是MeshAdvancedItem，直接返回世界坐标和无强度值
            //surface图元
            CxSurface surface = null;
            var tempsurfaceItem = surfaceItem as CxSurfaceItem;
            if (tempsurfaceItem != null)
            {
                if (tempsurfaceItem.Surface == null)
                    return (null, null);
                surface = tempsurfaceItem.Surface;
            }
            else if (surfaceItem is CxSurfaceAdvancedItem advancedItem)
            {
                if (advancedItem.Surface == null)
                    return (null, null);
                surface = advancedItem.Surface;
            }
            else
            {
                return (null, null); // 如果没有有效的surface图元，返回null
            }

            // 初始化最近点和最小距离
            CxPoint3D? nearestPoint = null;
            byte? nearestIntensity = null;
            float minDistanceSquared = float.MaxValue;
            if (surface.Type == SurfaceType.Surface)
            {
                // 计算 worldObj 在网格中的索引
                int xIndex = (int)((worldObj.X - surface.XOffset) / surface.XScale);
                int yIndex = (int)((worldObj.Y - surface.YOffset) / surface.YScale);
                // 检查索引是否在范围内
                if (xIndex < 0 || xIndex >= surface.Width || yIndex < 0 || yIndex >= surface.Length)
                    return (null, null);
                var minDis = 5 * (surface.XScale * surface.XScale +
                             surface.YScale * surface.YScale +
                             surface.ZScale * surface.ZScale);
                // 遍历附近的点（3x3 邻域）
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = xIndex + dx;
                        int ny = yIndex + dy;

                        // 检查邻域点是否在范围内
                        if (nx < 0 || nx >= surface.Width || ny < 0 || ny >= surface.Length)
                            continue;

                        // 获取点的索引
                        int index = ny * surface.Width + nx;
                        // 计算点的实际坐标
                        float x = surface.XOffset + nx * surface.XScale;
                        float y = surface.YOffset + ny * surface.YScale;
                        float z = surface.Data[index] == -32768
                           ? float.NegativeInfinity
                           : surface.ZOffset + surface.Data[index] * surface.ZScale;

                        if (float.IsInfinity(z)) // 跳过无效点
                            continue;

                        // 计算欧几里得距离的平方
                        float distanceSquared = (x - worldObj.X) * (x - worldObj.X) +
                                                (y - worldObj.Y) * (y - worldObj.Y) +
                                                (z - worldObj.Z) * (z - worldObj.Z);

                        // 更新最近点
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
                //PointCloud类型
                nearestPoint = worldObj;
                nearestIntensity = null;
            }
            return (nearestPoint, nearestIntensity);
        }
    }
}


