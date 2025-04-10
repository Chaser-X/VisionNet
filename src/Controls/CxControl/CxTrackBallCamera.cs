using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxTrackBallCamera
    {
        #region 私有字段
        private OpenGLControl openGLControl;
        private float translateX = 0f;
        private float translateY = 0f;
        private float translateZ = -10;
        private bool isDragging = false;
        private bool isRotating = false;
        private int lastMouseX, lastMouseY;
        private float[] rotationMatrix = new float[16];
        private float translateSpeed = 0.5f;
        private float translateZSpeed = 0.001f;
        #endregion
        #region 属性
        public ViewMode ViewMode { get; set; } = ViewMode.Front;
        public bool Enable2DView { get; set; } = false;
        #endregion
        #region 构造函数
        public CxTrackBallCamera(OpenGLControl openGLControl)
        {
            this.openGLControl = openGLControl;
            // 初始化旋转矩阵为单位矩阵
            for (int i = 0; i < 16; i++)
                rotationMatrix[i] = (i % 5 == 0) ? 1.0f : 0.0f;

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
            if (e.Button == MouseButtons.Middle)
            {
                isDragging = true;
                lastMouseX = e.X;
                lastMouseY = e.Y;
            }
            else if (e.Button == MouseButtons.Left)
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

                // 计算旋转角度
                float angleX = deltaY * 0.5f;
                float angleY = deltaX * 0.5f;

                if(!Enable2DView)
                    // 更新旋转矩阵
                    UpdateRotationMatrix(angleX, angleY);

                lastMouseX = e.X;
                lastMouseY = e.Y;
                openGLControl.Invalidate();
            }
        }
        private void OpenGLControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                isDragging = false;
            }
            else if (e.Button == MouseButtons.Left)
            {
                isRotating = false;
            }
        }
        private void OpenGLControl_MouseWheel(object sender, MouseEventArgs e)
        {
            // translateZ += e.Delta * (translateZSpeed * (1 - (-translateZ) / 10000.0f));
            float delta = e.Delta > 0 ? 0.9f : 1.1f;
            translateZ *= delta;
            openGLControl.Invalidate();
        }
        #endregion
        #region 渲染方法
        /// <summary>
        /// 设置视图模式
        /// </summary>
        public void FitView(Box3D? viewBox)
        {
            if (!viewBox.HasValue || ViewMode == ViewMode.None)
            {
                return;
            }
            // 计算点云的宽度和高度
            double pointCloudWidth = viewBox.Value.Size.Width;
            double pointCloudHeight = viewBox.Value.Size.Height;

            //计算平移缩放的速度系数
            translateSpeed = translateZSpeed = (float)Math.Min(pointCloudWidth, pointCloudHeight) / 400.0f;
            // 根据窗体大小和点云的大小自适应设置 zoom
            double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
            double zoomFactor = Math.Max(pointCloudWidth / aspectRatio, pointCloudHeight);

            translateX = (float)-viewBox.Value.Center.X;
            translateY = (float)-viewBox.Value.Center.Y;
            if(!Enable2DView)
                translateZ = (float)-viewBox.Value.Center.Z - (float)zoomFactor * 1.2f; //  适当调整视距
            else
            {
                var scaleWdith = pointCloudWidth / aspectRatio;
                if(scaleWdith > pointCloudHeight)
                    translateZ = (float)(openGLControl.Width / scaleWdith); //  适当调整视距
                else
                    translateZ = (float)(openGLControl.Height / pointCloudHeight); //  适当调整视距
            }

            // 重置旋转矩阵
            for (int i = 0; i < 16; i++)
                rotationMatrix[i] = (i % 5 == 0) ? 1.0f : 0.0f;

            // 根据视图模式设置初始旋转矩阵
            switch (ViewMode)
            {
                case ViewMode.Top:
                    SetRotationMatrix(0, 1, 0, 0);
                    break;
                case ViewMode.Front:
                    SetRotationMatrix(90, 1, 0, 0);
                    break;
                case ViewMode.Left:
                    SetRotationMatrix(90, 0, 1, 0);
                    break;
                case ViewMode.Right:
                    SetRotationMatrix(-90, 0, 1, 0);
                    break;
            }
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

            if (!Enable2DView)
            {
                // 设置透视投影
                double nearPlane = 0.01; // 近裁剪面
                double farPlane = 1000.0; // 远裁剪面
                double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
                double fov = 60.0; // 视场角
                gl.Perspective(fov, aspectRatio, nearPlane, farPlane); // 设置透视投影
            }
            else
            {
                // 设置正交投影
                double left = -openGLControl.Width / 2;
                double right = openGLControl.Width / 2;
                double bottom = -openGLControl.Height / 2;
                double top = openGLControl.Height / 2;
                gl.Ortho(left, right, bottom, top, -1000.0, 1000.0); // 设置正交投影
            }

            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            if (!Enable2DView)
            {
                gl.Translate(translateX, translateY, translateZ);
            }
            else
            {
                gl.Translate(translateX, translateY, 0);
                gl.Scale(translateZ, -translateZ, 1);
            }
            gl.MultMatrix(rotationMatrix);
        }
        private void UpdateRotationMatrix(float angleX, float angleY)
        {
            // 计算旋转矩阵
            float[] rotationX = new float[16];
            float[] rotationY = new float[16];

            for (int i = 0; i < 16; i++)
            {
                rotationX[i] = (i % 5 == 0) ? 1.0f : 0.0f;
                rotationY[i] = (i % 5 == 0) ? 1.0f : 0.0f;
            }

            float cosX = (float)Math.Cos(angleX * Math.PI / 180.0);
            float sinX = (float)Math.Sin(angleX * Math.PI / 180.0);
            float cosY = (float)Math.Cos(angleY * Math.PI / 180.0);
            float sinY = (float)Math.Sin(angleY * Math.PI / 180.0);

            rotationX[5] = cosX;
            rotationX[6] = -sinX;
            rotationX[9] = sinX;
            rotationX[10] = cosX;

            rotationY[0] = cosY;
            rotationY[2] = sinY;
            rotationY[8] = -sinY;
            rotationY[10] = cosY;
            // 更新旋转矩阵
            MultiplyMatrix(rotationMatrix, rotationX);
            MultiplyMatrix(rotationMatrix, rotationY);
        }
        private void SetRotationMatrix(float angle, float x, float y, float z)
        {
            float radians = angle * (float)Math.PI / 180.0f;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            float oneMinusCos = 1.0f - cos;
           
            rotationMatrix[0] = cos + x * x * oneMinusCos;
            rotationMatrix[1] = x * y * oneMinusCos - z * sin;
            rotationMatrix[2] = x * z * oneMinusCos + y * sin;
            rotationMatrix[3] = 0.0f;

            rotationMatrix[4] = y * x * oneMinusCos + z * sin;
            rotationMatrix[5] = cos + y * y * oneMinusCos;
            rotationMatrix[6] = y * z * oneMinusCos - x * sin;
            rotationMatrix[7] = 0.0f;

            rotationMatrix[8] = z * x * oneMinusCos - y * sin;
            rotationMatrix[9] = z * y * oneMinusCos + x * sin;
            rotationMatrix[10] = cos + z * z * oneMinusCos;
            rotationMatrix[11] = 0.0f;

            rotationMatrix[12] = 0.0f;
            rotationMatrix[13] = 0.0f;
            rotationMatrix[14] = 0.0f;
            rotationMatrix[15] = 1.0f;
        }
        private void MultiplyMatrix(float[] result, float[] matrix)
        {
            float[] temp = new float[16];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    temp[i * 4 + j] = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        temp[i * 4 + j] += result[i * 4 + k] * matrix[k * 4 + j];
                    }
                }
            }
            Array.Copy(temp, result, 16);
        }
        #endregion
    }
}
