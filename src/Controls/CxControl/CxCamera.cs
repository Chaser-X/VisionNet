using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Assets;
using SharpGL.SceneGraph.Lighting;
using VisionNet.DataType;
using static System.Net.Mime.MediaTypeNames;

namespace VisionNet.Controls
{
    public class CxCamera : IDisposable
    {
        #region 私有字段
        private object lockObj = new object();
        private OpenGLControl openGLControl;
        private float translateX = 0f;
        private float translateY = 0f;
        private float zoom = -10;
        private bool isDragging = false;
        private bool isRotating = false;
        private int lastMouseX, lastMouseY;
        private float rotateX = 0.0f;
        private float rotateY = 0.0f;
        private float translateSpeed = 1f;
        private float zoomSpeed = 1f;
        // 渲染数据
        private CxSurfaceItem surfaceItem = null;
        private CxCoordinateSystemItem coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem colorBarItem = new CxColorBarItem();
        private List<IRenderItem> renderItem = new List<IRenderItem>();
        private Dictionary<string, uint> textTextures = new Dictionary<string, uint>();
        #endregion
        #region 构造函数
        public CxCamera(OpenGLControl openGLControl)
        {
            this.openGLControl = openGLControl;

            // 订阅事件
            this.openGLControl.MouseDown += OpenGLControl_MouseDown;
            this.openGLControl.MouseMove += OpenGLControl_MouseMove;
            this.openGLControl.MouseUp += OpenGLControl_MouseUp;
            this.openGLControl.MouseWheel += OpenGLControl_MouseWheel;
            this.openGLControl.OpenGLInitialized += OpenGLControl_OpenGLInitialized;
            this.openGLControl.OpenGLDraw += OpenGLControl_OpenGLDraw;
            this.openGLControl.Resize += OpenGLControl_Resize;
            OpenGLControl_OpenGLInitialized(null, null);

        }
        #endregion
        #region 操作方法
        /// <summary>
        /// 设置点云数据并自适应Z显示范围
        /// </summary>
        public void SetPointCloud(CxSurface inpointCloud, SurfaceMode surfaceMode)
        {
            var tempSuface = new CxSurface();
            var size = inpointCloud.Width * inpointCloud.Length;
            if (size > 6000000)
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

            surfaceItem = new CxSurfaceItem(tempSuface ?? new CxSurface(), surfaceMode);
            FitView(); // 调整视图以适应点云数据
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
        /// <summary>
        /// 添加3D文本
        /// </summary>
        public void SetText(TextInfo text)
        {
            if (string.IsNullOrEmpty(text.Text)) return;
            //textInfos.Add(text);
        }
        #endregion
        #region 界面事件处理
        private void OpenGLControl_OpenGLInitialized(object sender, EventArgs e)
        {
            OpenGL gl = openGLControl.OpenGL;
            gl.ClearColor(0, 0, 0, 0);
            gl.PointSize(2.0f);
        }

        private void OpenGLControl_OpenGLDraw(object sender, SharpGL.RenderEventArgs args)
        {
            OpenGL gl = openGLControl.OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            //// 启用深度测试
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LESS);
            gl.LoadIdentity();
            // 设置投影矩阵
            LookAtMatrix(gl);
            // 应用视图变换并绘制所有元素
            Render(gl);
            // 禁用深度测试
            gl.Disable(OpenGL.GL_DEPTH_TEST);
        }
        private void OpenGLControl_Resize(object sender, EventArgs e)
        {
        }
        #endregion
        #region 鼠标事件处理
        private void OpenGLControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                lastMouseX = e.X;
                lastMouseY = e.Y;
            }
            else if (e.Button == MouseButtons.Right)
            {
                isRotating = true;
                lastMouseX = e.X;
                lastMouseY = e.Y;
            }
        }

        private void OpenGLControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                int deltaX = e.X - lastMouseX;
                int deltaY = e.Y - lastMouseY;

                translateX += deltaX * translateSpeed;
                translateY -= deltaY * translateSpeed;

                lastMouseX = e.X;
                lastMouseY = e.Y;
                openGLControl.Invalidate();
            }
            else if (isRotating)
            {
                int deltaX = e.X - lastMouseX;
                int deltaY = e.Y - lastMouseY;

                rotateX += deltaY * 0.5f;
                rotateY += deltaX * 0.5f;

                lastMouseX = e.X;
                lastMouseY = e.Y;
                openGLControl.Invalidate();
            }
        }

        private void OpenGLControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
            }
            else if (e.Button == MouseButtons.Right)
            {
                isRotating = false;
            }
        }

        private void OpenGLControl_MouseWheel(object sender, MouseEventArgs e)
        {
            zoom += e.Delta * (zoomSpeed * (1 - (-zoom) / 10000.0f));
            openGLControl.Invalidate();
        }
        #endregion
        #region 渲染方法
        /// <summary>
        /// 自适应视图
        /// </summary>
        private void FitView()
        {
            if (surfaceItem == null || surfaceItem.Surface == null)
            {
                return;
            }
            // 计算点云的宽度和高度
            double pointCloudWidth = surfaceItem.BoundingBox.Value.Size.X;
            double pointCloudHeight = surfaceItem.BoundingBox.Value.Size.Y;

            //计算平移缩放的速度系数
            translateSpeed = zoomSpeed = (float)Math.Min(pointCloudWidth, pointCloudHeight) / 400.0f;
            // 根据窗体大小和点云的大小自适应设置 zoom
            double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
            double zoomFactor = Math.Max(pointCloudWidth / aspectRatio, pointCloudHeight);

            translateX = (float)-surfaceItem.BoundingBox.Value.Center.X;
            translateY = (float)-surfaceItem.BoundingBox.Value.Center.Y;
            zoom = (float)-surfaceItem.BoundingBox.Value.Center.Z - (float)zoomFactor * 1.5f; //  适当调整视距
            rotateX = 0.0f;
            rotateY = 0.0f;
        }
        /// <summary>
        /// 视图空间设置
        /// </summary>
        private void LookAtMatrix(OpenGL gl)
        {
            // 设置视口
            gl.Viewport(0, 0, openGLControl.Width, openGLControl.Height);

            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            double nearPlane = 0.001; // 近裁剪面
            double farPlane = 10000.0; // 远裁剪面
            double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
            double fov = 60.0; // 视场角
            // 手动设置透视投影矩阵
            //double top = nearPlane * Math.Tan(fov * Math.PI / 360.0);
            //double bottom = -top;
            //double left = bottom * aspectRatio;
            //double right = top * aspectRatio;
            //gl.Frustum(left, right, bottom, top, nearPlane, farPlane); // 设置视锥体
            gl.Perspective(fov, aspectRatio, nearPlane, farPlane); // 设置透视投影
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            gl.Translate(translateX, translateY, zoom);
            gl.Rotate(rotateX, 1.0f, 0.0f, 0.0f);
            gl.Rotate(rotateY, 0.0f, 1.0f, 0.0f);
        }
        /// <summary>
        /// 可视化元素渲染
        /// </summary>
        private void Render(OpenGL gl)
        {
            coordinationItem.DrawScreenPositionedAxes(gl);
            coordinationItem.Draw(gl);
            surfaceItem?.Draw(gl);
            if (surfaceItem != null &&
                ((int)surfaceItem.SurfaceMode & (int)SurfaceMode.HeightMap) == (int)SurfaceMode.HeightMap)
            {
                colorBarItem.SetRange(surfaceItem.ZMin, surfaceItem.ZMax);
                colorBarItem.Draw(gl);
            }
            foreach (var item in renderItem)
            {
                item.Draw(gl);
            }
        }
        private void DrawTextLabel3D(OpenGL gl, float x, float y, float z, float size, string text)
        {
            // 将3D坐标转换为屏幕坐标（包括深度信息）
            var objCoord = new Vertex(x, y, z);
            var screenCoord = gl.Project(objCoord);
            // 判断是否在屏幕范围内
            if (screenCoord.X < 0 || screenCoord.X > openGLControl.Width ||
                screenCoord.Y < 0 || screenCoord.Y > openGLControl.Height)
            {
                return;
            }

            // 如果需要考虑透视范围，可以检查 screenCoord.Z（假设其为归一化深度：0～1）
            if (screenCoord.Z < 0 || screenCoord.Z > 1)
            {
                return;
            }

            float newScale = size * (1 + screenCoord.Z);//1 / (size / (screenCoord.Z + epsilon))* 100;

            // 保存当前矩阵
            gl.PushMatrix();

            // 移动到文字位置
            gl.Translate(x, y, z);

            // 使文字始终面向相机
            gl.Rotate(-rotateY, 0.0f, 1.0f, 0.0f);
            gl.Rotate(-rotateX, 1.0f, 0.0f, 0.0f);

            // 应用缩放，使文字在屏幕上保持固定像素大小
            gl.Scale(newScale, newScale, newScale);

            // 绘制文字
            gl.Color(1.0f, 1.0f, 1.0f);
            foreach (char c in text)
            {
                gl.DrawText3D("Arial", 0.1f, 0.0f, c.ToString());
            }

            gl.PopMatrix();
        }
        private void DrawTextLabel2D(OpenGL gl, float x, float y, float z, float size, string text)
        {
            // 将3D坐标转换为屏幕坐标（包括深度信息）
            var objCoord = new Vertex(x, y, z);
            var screenCoord = gl.Project(objCoord);
            // 判断是否在屏幕范围内
            if (screenCoord.X < 0 || screenCoord.X > openGLControl.Width ||
                screenCoord.Y < 0 || screenCoord.Y > openGLControl.Height)
            {
                return;
            }
            // 如果需要考虑透视范围，可以检查 screenCoord.Z（假设其为归一化深度：0～1）
            if (screenCoord.Z < 0 || screenCoord.Z > 1)
            {
                return;
            }
            gl.DrawText((int)screenCoord.X, (int)screenCoord.Y, 1, 1, 1, "Arial", size, text);
        }
        /// <summary>
        /// 清除所有渲染数据
        /// </summary>
        public void Clear()
        {
            renderItem.Clear();
            openGLControl.Invalidate();
        }
        #endregion
        #region 资源释放
        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {

                }
                // 释放非托管资源（如果有）
                disposed = true;
            }
        }
        ~CxCamera()
        {
            Dispose(true);
        }
        #endregion
    }
}