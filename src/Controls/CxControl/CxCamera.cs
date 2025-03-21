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
    public enum SufaceMode
    {
        PointCloud,
        Mesh,
        HeightMap,
        Intensity,
    }
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
        private bool surfaceModeChanged = false;
        // 渲染数据
        private CxSuface pointCloud = null;
        private List<Segment> segments = new List<Segment>();
        private List<TextInfo> textInfos = new List<TextInfo>();
        private List<uint> meshIndexs = new List<uint>();
        private Dictionary<string, uint> textTextures = new Dictionary<string, uint>();
        private uint[] vboIds = new uint[2];
        private bool vboInitialized = false;

        private bool pointCloudUpdated = false;
        private double pointCloudZMin = 0.0; // 初始化Z最小值
        private double pointCloudZMax = 0.0; // 初始化Z最大值

        private SufaceMode surfaceMode = SufaceMode.PointCloud;
        #endregion
        #region 属性字段
        /// <summary>
        /// 表面显示模式
        /// </summary>
        public SufaceMode SufaceMode
        {
            get
            {
                return SufaceMode;
            }
            set
            {
                pointCloudUpdated = surfaceMode != value;
                surfaceMode = value;
            }
        }
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
        public void SetPointCloud(CxSuface pointCloud)
        {
            this.pointCloud = null;
            AdjustViewToPointCloud(pointCloud); // 调整视图以适应点云数据
            this.pointCloud = pointCloud ?? new CxSuface();
            pointCloudUpdated = true; // 标记点云数据已更新
                                      // openGLControl.Invalidate(); // 重新绘制
        }

        /// <summary>
        /// 添加线段
        /// </summary>
        public void SetSegment(Segment segment)
        {
            segments.Add(segment);
            //openGLControl.Invalidate();
        }

        /// <summary>
        /// 添加3D文本
        /// </summary>
        public void SetText(TextInfo text)
        {
            if (string.IsNullOrEmpty(text.Text)) return;

            textInfos.Add(text);
            //openGLControl.Invalidate();
        }
        #endregion
        #region 界面事件处理
        private void OpenGLControl_OpenGLInitialized(object sender, EventArgs e)
        {
            OpenGL gl = openGLControl.OpenGL;
            gl.ClearColor(0, 0, 0, 0);
            //gl.Enable(OpenGL.GL_DEPTH_TEST);
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
            SetProjectionMatrix(gl);
            // 应用视图变换并绘制所有元素
            ApplyViewTransform(gl);
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
        private void AdjustViewToPointCloud(CxSuface pointCloud)
        {
            if (pointCloud == null || pointCloud.Data.Length == 0) return;
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            // 计算点云的边界
            var data = pointCloud.ToPoints();
            foreach (var point in data)
            {
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;

                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;

                if (!double.IsInfinity(point.Z))
                {
                    if (point.Z < minZ) minZ = point.Z;
                    if (point.Z > maxZ) maxZ = point.Z;
                }
            }

            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;
            double centerZ = (minZ + maxZ) / 2.0;
            // 保存Z轴范围
            pointCloudZMin = minZ;
            pointCloudZMax = maxZ;

            // 计算点云的宽度和高度
            double pointCloudWidth = maxX - minX;
            double pointCloudHeight = maxY - minY;

            //计算平移缩放的速度系数
            translateSpeed = zoomSpeed = (float)Math.Max(pointCloudWidth, pointCloudHeight) / 400.0f;
            // 根据窗体大小和点云的大小自适应设置 zoom
            double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
            double zoomFactor = Math.Max(pointCloudWidth / aspectRatio, pointCloudHeight);

            translateX = (float)-centerX;
            translateY = (float)-centerY;
            zoom = (float)-centerZ - (float)zoomFactor * 1.5f; //  适当调整视距
            rotateX = 0.0f;
            rotateY = 0.0f;

            //openGLControl.Invalidate(); // 刷新控件以更新显示
        }
        /// <summary>
        /// 设置投影矩阵
        /// </summary>
        private void SetProjectionMatrix(OpenGL gl)
        {
            // 设置视口
            gl.Viewport(0, 0, openGLControl.Width, openGLControl.Height);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();

            double nearPlane = 0.001; // 近裁剪面
            double farPlane = 10000.0; // 远裁剪面
            double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
            double fov = 45.0; // 视场角

            // 手动设置透视投影矩阵
            double top = nearPlane * Math.Tan(fov * Math.PI / 360.0);
            double bottom = -top;
            double left = bottom * aspectRatio;
            double right = top * aspectRatio;

            gl.Frustum(left, right, bottom, top, nearPlane, farPlane); // 设置视锥体
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
        }

        /// <summary>
        /// 应用视图变换并绘制所有元素
        /// </summary>
        private void ApplyViewTransform(OpenGL gl)
        {
            gl.Translate(translateX, translateY, zoom);
            gl.Rotate(rotateX, 1.0f, 0.0f, 0.0f);
            gl.Rotate(rotateY, 0.0f, 1.0f, 0.0f);

            DrawPointCloud(gl);
            DrawCoordination(gl);
            DrawSegments(gl);
            DrawColorBar(gl);// 绘制颜色条
            DrawText(gl);
        }
        private void DrawColorBar(OpenGL gl)
        {
            // 检测点云是否有效
            if (pointCloud == null || pointCloud.Data.Length == 0 || pointCloudZMax - pointCloudZMin <= 0)
                return;

            int colorBarWidth = 20; // 颜色条的宽度
            int colorBarHeight = openGLControl.Height / 2; // 颜色条的高度
            int startX = openGLControl.Width - colorBarWidth - 10; // 颜色条的起始位置（右侧，留出一些边距）
            int startY = (openGLControl.Height - colorBarHeight) / 2; // 颜色条竖直居中

            // 设置 2D 正交投影模式
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, openGLControl.Width, 0, openGLControl.Height, -1, 1); // 2D 投影
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            // 绘制颜色条
            gl.Begin(OpenGL.GL_QUADS);

            for (int i = 0; i < colorBarHeight; i++)
            {
                // 计算归一化值和当前高度
                float normalizedValue = (float)i / colorBarHeight;
                double currentHeight = pointCloudZMin + normalizedValue * (pointCloudZMax - pointCloudZMin);

                // 获取颜色
                var (r, g, b) = GetColorByHeight(currentHeight, pointCloudZMin, pointCloudZMax);

                gl.Color(r, g, b);
                gl.Vertex(startX, startY + i, 0);                // 左下角
                gl.Vertex(startX + colorBarWidth, startY + i, 0);  // 右下角
                gl.Vertex(startX + colorBarWidth, startY + i + 1, 0); // 右上角
                gl.Vertex(startX, startY + i + 1, 0);           // 左上角
            }

            gl.End();

            // 绘制刻度和文字
            int numDivisions = 7; // 7 等分
            gl.Color(1.0f, 1.0f, 1.0f); // 刻度和文字用白色

            for (int i = 0; i <= numDivisions; i++)
            {
                // 计算刻度位置和对应高度
                int tickY = startY + (int)(i * (colorBarHeight / (float)numDivisions));
                double heightValue = pointCloudZMin + i * (pointCloudZMax - pointCloudZMin) / numDivisions;
                gl.Begin(OpenGL.GL_LINES);
                // 绘制刻度线
                gl.Vertex(startX - 5, tickY, 0); // 刻度线左端
                gl.Vertex(startX, tickY, 0);     // 刻度线右端
                gl.End();
                // 绘制高度文字
                gl.DrawText(startX - 45, tickY - 5, 1, 1, 1, "", 10, $"{heightValue:F2}");
                //   DrawTextLabel2D(gl, startX - 40, tickY - 5, 15, $"{heightValue:F2}");
                //gl.Translate(x, y, z);
                //gl.Scale(1f, 1f, 1f); // 调整文本大小

            }

            // 恢复矩阵设置
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
        }
        /// <summary>
        /// 绘制坐标轴
        /// </summary>
        /// <param name="gl">OpenGL 对象</param>
        private void DrawCoordination(OpenGL gl)
        {
            gl.Begin(OpenGL.GL_LINES);

            // 绘制X轴 - 红色
            gl.Color(1.0f, 0.0f, 0.0f);
            gl.Vertex(-1000.0f, 0.0f, 0.0f);
            gl.Vertex(1000.0f, 0.0f, 0.0f);

            // 绘制Y轴 - 绿色
            gl.Color(0.0f, 1.0f, 0.0f);
            gl.Vertex(0.0f, -1000.0f, 0.0f);
            gl.Vertex(0.0f, 1000.0f, 0.0f);

            // 绘制Z轴 - 蓝色
            gl.Color(0.0f, 0.0f, 1.0f);
            gl.Vertex(0.0f, 0.0f, -1000.0f);
            gl.Vertex(0.0f, 0.0f, 1000.0f);

            // X轴刻度
            gl.Color(1.0f, 0.0f, 0.0f);
            for (float i = -1000.0f; i <= 1000.0f; i += 10.0f)
            {
                gl.Vertex(i, -0.1f, 0.0f);
                gl.Vertex(i, 0.1f, 0.0f);
            }

            // Y轴刻度
            gl.Color(0.0f, 1.0f, 0.0f);
            for (float i = -1000.0f; i <= 1000.0f; i += 10.0f)
            {
                gl.Vertex(-0.1f, i, 0.0f);
                gl.Vertex(0.1f, i, 0.0f);
            }

            // Z轴刻度
            gl.Color(0.0f, 0.0f, 1.0f);
            for (float i = -1000.0f; i <= 1000.0f; i += 10.0f)
            {
                gl.Vertex(0.0f, -0.1f, i);
                gl.Vertex(0.0f, 0.1f, i);
            }

            gl.End();
            //绘制刻度标签
            gl.Color(1.0f, 1.0f, 1.0f);
            for (float i = -1000.0f; i <= 1000.0f; i += 100.0f)
            {
                // X轴刻度标签
                DrawTextLabel2D(gl, i, -0.2f, 0.0f, 15, i.ToString());

                // Y轴刻度标签
                DrawTextLabel2D(gl, -0.2f, i, 0.0f, 15, i.ToString());

                // Z轴刻度标签
                DrawTextLabel2D(gl, 0.0f, -0.2f, i, 15, i.ToString());
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
        /*
        private void DrawTextLabel2D(OpenGL gl, float x, float y, float z, float size, string text)
        {
            // 将3D坐标转换为屏幕坐标
            var objcoor = new Vertex(x, y, z);
            var screencoor = gl.Project(objcoor);

            // 检查屏幕坐标是否在屏幕范围内
            if (screencoor.X < 0 || screencoor.X > openGLControl.Width || screencoor.Y < 0 || screencoor.Y > openGLControl.Height)
            {
                return; // 如果超出屏幕范围，则不绘制文本
            }

            // 检查透视范围
            if (screencoor.Z < 0 || screencoor.Z > 1)
            {
                return; // 如果超出透视范围，则不绘制文本
            }


            // 确保坐标在有效范围内
            if (screencoor.X >= 0 && screencoor.X < openGLControl.Width && screencoor.Y >= 0 && screencoor.Y < openGLControl.Height)
            {

                // 保存当前矩阵状态
                gl.PushMatrix();
                // 设置正交投影
                gl.MatrixMode(OpenGL.GL_PROJECTION);
                gl.PushMatrix();
                gl.LoadIdentity();
                gl.Ortho(0, openGLControl.Width, 0, openGLControl.Height, -1000, 1000);
                gl.MatrixMode(OpenGL.GL_MODELVIEW);
                gl.PushMatrix();
                gl.LoadIdentity();

                // 设置文本位置
                gl.Translate(screencoor.X, screencoor.Y, 0);//objcoor.Z
                gl.Scale(size, size, size); // 调整文本大小

                // 启用深度测试
                //gl.Enable(OpenGL.GL_DEPTH_TEST);
                // 绘制文本
                foreach (char c in text)
                {
                    gl.DrawText3D("Arial", 1f, 0, c.ToString());
                }

                // 禁用深度测试
                //gl.Disable(OpenGL.GL_DEPTH_TEST);

                // 恢复矩阵状态
                gl.PopMatrix();
                gl.MatrixMode(OpenGL.GL_PROJECTION);
                gl.PopMatrix();
                gl.MatrixMode(OpenGL.GL_MODELVIEW);
                gl.PopMatrix();
            }

        }
        */
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
        private void DrawPointCloud(OpenGL gl)
        {
            if (pointCloud == null || pointCloud.Data.Length == 0) return;

            if (vboInitialized && pointCloudUpdated)
            {
                openGLControl.OpenGL.DeleteBuffers(2, vboIds);
                vboInitialized = false;
            }
            if (!vboInitialized)
            {
                openGLControl.OpenGL.GenBuffers(2, vboIds);
                vboInitialized = true;
                pointCloudUpdated = true; // 初次初始化时标记为已更新
            }
            if (pointCloudUpdated || surfaceModeChanged)
            {
                float[] vertices = null;// new float[pointCloud.Data.Length * 3];
                float[] colors = null;//new float[pointCloud.Intensity.Length * 3];
                CxPoint3D[] vertexs = pointCloud.ToPoints();

                if (surfaceMode == SufaceMode.Mesh)
                {
                    meshIndexs = GenerateMeshIndexFromPointCloud(pointCloud);
                }
                vertices = new float[vertexs.Length * 3];
                colors = new float[vertexs.Length * 3];
                for (int i = 0; i < vertexs.Length; i++)
                {
                    vertices[i * 3] = (float)vertexs[i].X;
                    vertices[i * 3 + 1] = (float)vertexs[i].Y;
                    vertices[i * 3 + 2] = (float)vertexs[i].Z;

                    float intensity = 1;
                    if (pointCloud.Intensity.Length == 0)
                    {
                        intensity = 1;
                    }
                    else
                    {
                        intensity = (float)pointCloud.Intensity[i] / 255.0f; // 亮度因子
                    }

                    if (surfaceMode == SufaceMode.Intensity)
                    {
                        colors[i * 3] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 1] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 2] = Math.Min(intensity, 1.0f);
                    }
                    else
                    {
                        var color = GetColorByHeight(vertexs[i].Z, pointCloudZMin, pointCloudZMax);
                        colors[i * 3] = Math.Min(color.r * intensity, 1.0f);
                        colors[i * 3 + 1] = Math.Min(color.g * intensity, 1.0f);
                        colors[i * 3 + 2] = Math.Min(color.b * intensity, 1.0f);
                    }
                }
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices, OpenGL.GL_STATIC_DRAW);

                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, colors, OpenGL.GL_STATIC_DRAW);
                pointCloudUpdated = false; // 重置标记
            }

            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.EnableClientState(OpenGL.GL_COLOR_ARRAY);

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
            gl.VertexPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
            gl.ColorPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);
            //gl.Enable(OpenGL.GL_DEPTH_TEST);
            if (surfaceMode == SufaceMode.PointCloud)
                gl.DrawArrays(OpenGL.GL_POINTS, 0, pointCloud.Data.Length);
            else if (surfaceMode == SufaceMode.Mesh)
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, meshIndexs.Count, meshIndexs.ToArray());//OpenGL.GL_UNSIGNED_INT
            }
            //gl.Disable(OpenGL.GL_DEPTH_TEST);

            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.DisableClientState(OpenGL.GL_COLOR_ARRAY);
        }
        /// <summary>
        /// 根据给定范围的Zmin,zmax,对每个点Z返回和Z相关的颜色
        /// </summary>
        private (float r, float g, float b) GetColorByHeight(double z, double zMin, double zMax)
        {
            float range = (float)(zMax - zMin);
            if (z > zMax) z = zMax;
            if (z < zMin) z = zMin;
            float normalizedZ = (float)(z - zMin) / range;

            float r, g, b;
            if (normalizedZ < 1.0f / 7.0f)
            {
                // 深蓝
                r = 0.0f;
                g = 0.0f;
                b = 0.5f + normalizedZ * 3.5f;
            }
            else if (normalizedZ < 2.0f / 7.0f)
            {
                // 天空蓝
                r = 0.0f;
                g = (normalizedZ - 1.0f / 7.0f) * 7.0f;
                b = 1.0f;
            }
            else if (normalizedZ < 3.0f / 7.0f)
            {
                // 绿
                r = 0.0f;
                g = 1.0f;
                b = 1.0f - (normalizedZ - 2.0f / 7.0f) * 7.0f;
            }
            else if (normalizedZ < 4.0f / 7.0f)
            {
                // 黄
                r = (normalizedZ - 3.0f / 7.0f) * 7.0f;
                g = 1.0f;
                b = 0.0f;
            }
            else if (normalizedZ < 5.0f / 7.0f)
            {
                // 红
                r = 1.0f;
                g = 1.0f - (normalizedZ - 4.0f / 7.0f) * 7.0f;
                b = 0.0f;
            }
            else if (normalizedZ < 6.0f / 7.0f)
            {
                // 粉
                r = 1.0f;
                g = 0.0f;
                b = (normalizedZ - 5.0f / 7.0f) * 7.0f;
            }
            else
            {
                // 白
                r = 1.0f;
                g = (normalizedZ - 6.0f / 7.0f) * 7.0f;
                b = 1.0f;
            }
            return (r, g, b);
        }
        /// <summary>
        /// 绘制线段
        /// </summary>
        private void DrawSegments(OpenGL gl)
        {
            if (segments.Count == 0) return;

            gl.Begin(OpenGL.GL_LINES);
            foreach (var segment in segments)
            {
                gl.Color(1.0f, 0.0f, 0.0f); // 红色起点
                gl.Vertex(segment.Start.X, segment.Start.Y, segment.Start.Z);

                gl.Color(0.0f, 1.0f, 0.0f); // 绿色终点
                gl.Vertex(segment.End.X, segment.End.Y, segment.End.Z);
            }
            gl.End();
        }
        /// <summary>
        /// 绘制文本
        /// </summary>
        private void DrawText(OpenGL gl)
        {
            if (textInfos.Count == 0) return;

            // 启用纹理和混合
            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            // 禁用深度测试，确保文本始终可见
            gl.Disable(OpenGL.GL_DEPTH_TEST);

            foreach (var textInfo in textInfos)
            {
                // 获取或创建文本纹理
                uint textureID = GetTextTexture(gl, textInfo.Text);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureID);

                // 保存当前矩阵状态
                gl.PushMatrix();

                // 移动到文本位置
                gl.Translate(textInfo.Location.X, textInfo.Location.Y, textInfo.Location.Z);

                // 使文本始终面向相机 - 简单方法
                // 取消当前视图的旋转，使文本始终面向前方
                gl.Rotate(-rotateY, 0.0f, 1.0f, 0.0f);
                gl.Rotate(-rotateX, 1.0f, 0.0f, 0.0f);
                // 根据距离调整文本大小
                float fixedSize = zoom / -10;
                gl.Scale(fixedSize, fixedSize, fixedSize); // 应用固定大小


                // 绘制文本平面
                float width = 2.0f;
                float height = 0.5f;

                gl.Begin(OpenGL.GL_QUADS);
                {
                    gl.Color(1.0f, 1.0f, 1.0f); // 确保颜色设置正确
                    gl.TexCoord(0.0f, 1.0f); gl.Vertex(-width / 2, -height / 2, 0);
                    gl.TexCoord(1.0f, 1.0f); gl.Vertex(width / 2, -height / 2, 0);
                    gl.TexCoord(1.0f, 0.0f); gl.Vertex(width / 2, height / 2, 0);
                    gl.TexCoord(0.0f, 0.0f); gl.Vertex(-width / 2, height / 2, 0);
                }
                gl.End();

                gl.PopMatrix();
            }

            // 恢复深度测试
            gl.Enable(OpenGL.GL_DEPTH_TEST);

            // 禁用纹理和混合
            gl.Disable(OpenGL.GL_BLEND);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
        }
        /// <summary>
        /// 获取文本纹理
        /// </summary>
        private uint GetTextTexture(OpenGL gl, string text)
        {
            // 检查缓存
            if (textTextures.ContainsKey(text))
                return textTextures[text];

            // 创建文本位图
            using (Bitmap bitmap = new Bitmap(256, 64))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString(text, new Font("Arial", 24), Brushes.White, 0, 0);

                // 创建纹理
                uint[] textures = new uint[1];
                gl.GenTextures(1, textures);
                uint textureID = textures[0];

                gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureID);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);

                // 锁定位图数据
                BitmapData data = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // 加载纹理数据
                gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA,
                              bitmap.Width, bitmap.Height, 0,
                              OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE,
                              data.Scan0);

                bitmap.UnlockBits(data);

                // 缓存纹理ID
                textTextures[text] = textureID;
                return textureID;
            }
        }
        /// <summary>
        /// 生成网格索引
        /// </summary>
        public List<uint> GenerateMeshIndexFromPointCloud(CxSuface pointCloud)
        {
            List<uint> meshIndices = new List<uint>();
            if (pointCloud == null || pointCloud.Data.Length == 0) return meshIndices;
            // 生成索引
            for (uint y = 0; y < pointCloud.Length - 1; y++)
            {
                for (uint x = 0; x < pointCloud.Width - 1; x++)
                {
                    uint topLeft = (uint)(y * pointCloud.Width + x);
                    uint topRight = (uint)(topLeft + 1);
                    uint bottomLeft = (uint)(topLeft + pointCloud.Width);
                    uint bottomRight = (uint)(bottomLeft + 1);

                    // 第一个三角形
                    meshIndices.Add(topLeft);
                    meshIndices.Add(bottomLeft);
                    meshIndices.Add(topRight);

                    // 第二个三角形
                    meshIndices.Add(topRight);
                    meshIndices.Add(bottomLeft);
                    meshIndices.Add(bottomRight);
                }
            }
            return meshIndices;
            // openGLControl.Invalidate(); // 刷新控件以更新显示
        }

        /// <summary>
        /// 清除所有渲染数据
        /// </summary>
        public void Clear()
        {
            pointCloud = null;
            segments.Clear();
            textInfos.Clear();
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