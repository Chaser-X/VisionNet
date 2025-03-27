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
    public class CxCamera
    {
        #region 私有字段
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
        public void FitView(Box3D? viewBox)
        {
            if (!viewBox.HasValue)
            {
                return;
            }
            // 计算点云的宽度和高度
            double pointCloudWidth = viewBox.Value.Size.X;
            double pointCloudHeight = viewBox.Value.Size.Y;

            //计算平移缩放的速度系数
            translateSpeed = zoomSpeed = (float)Math.Min(pointCloudWidth, pointCloudHeight) / 400.0f;
            // 根据窗体大小和点云的大小自适应设置 zoom
            double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
            double zoomFactor = Math.Max(pointCloudWidth / aspectRatio, pointCloudHeight);

            translateX = (float)-viewBox.Value.Center.X;
            translateY = (float)-viewBox.Value.Center.Y;
            zoom = (float)-viewBox.Value.Center.Z - (float)zoomFactor * 1.5f; //  适当调整视距
            rotateX = 0.0f;
            rotateY = 0.0f;
        }
        /// <summary>
        /// 视图空间设置
        /// </summary>
        public void LookAtMatrix(OpenGL gl)
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
        #endregion
    }
}