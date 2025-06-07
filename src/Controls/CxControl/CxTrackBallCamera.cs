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
        private CxPoint3D pointCloudCenter = new CxPoint3D();
        #endregion
        #region 属性
        public ViewMode ViewMode { get; set; } = ViewMode.Front;
        public bool Enable2DView { get; set; } = false;
        public CxPoint3D? RotationPoint { get; set; } = null; //旋转点
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
            this.openGLControl.MouseDoubleClick += OpenGLControl_MouseDoubleClick; // 添加双击事件

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
                float angleX = -deltaY * 0.5f;
                float angleY = -deltaX * 0.5f;

                if (!Enable2DView)
                {
                    // 更新旋转矩阵
                    UpdateRotationMatrix(angleX, angleY, RotationPoint);
                }

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
            /*  // translateZ += e.Delta * (translateZSpeed * (1 - (-translateZ) / 10000.0f));
              float delta = e.Delta > 0 ? 0.9f : 1.1f;
              //translateZ *= delta;
              scale *= delta;*/
            // 计算缩放因子
            float delta = e.Delta > 0 ? 0.9f : 1.1f;
            translateZ *= delta;
            openGLControl.Invalidate();
        }
        private void OpenGLControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 获取鼠标点击位置的世界坐标
                var worldPosition = GetMouseWorldPosition(e.X, e.Y);
                if (worldPosition != null)
                {
                    // 将世界坐标转换到旋转后的坐标系
                    var adjustedPosition = ApplyInverseRotation(worldPosition.Value);

                    // 根据调整后的坐标计算平移量
                    translateX = -adjustedPosition.X;
                    translateY = -adjustedPosition.Y;

                    // 触发重绘
                    openGLControl.Invalidate();
                }
            }
        }

        #endregion
        #region 渲染方法
        /// <summary>
        public void FitView(Box3D? viewBox)
        {
            if (ViewMode == ViewMode.None)
                return;
            translateX = 0f;
            translateY = 0f;
            translateZ = -10;
            translateSpeed = 0.5f;
            pointCloudCenter = new CxPoint3D();
            //重置视图旋转矩阵
            for (int i = 0; i < 16; i++)
                rotationMatrix[i] = (i % 5 == 0) ? 1.0f : 0.0f;
            //重置视图中心位置
            if (viewBox.HasValue)
            {
                // 更新点云中心
                pointCloudCenter = new CxPoint3D(
                    (float)viewBox.Value.Center.X,
                    (float)viewBox.Value.Center.Y,
                    (float)viewBox.Value.Center.Z
                );

                // 计算点云的宽度、高度和深度
                double pointCloudWidth = viewBox.Value.Size.Width;
                double pointCloudHeight = viewBox.Value.Size.Height;
                double pointCloudDepth = viewBox.Value.Size.Depth;

                // 计算平移缩放的速度系数
                var min1 = Math.Min(pointCloudWidth, pointCloudHeight);
                var min2 = Math.Min(pointCloudHeight, pointCloudDepth);
                var min3 = Math.Min(pointCloudDepth, pointCloudWidth);
                if (min1 == min2) // height min
                    translateSpeed = (float)min3 / 400.0f;
                if (min2 == min3) // depth min
                    translateSpeed = (float)min1 / 400.0f;
                if (min1 == min3) // width min
                    translateSpeed = (float)min2 / 400.0f;

                // 根据窗体大小和点云的大小自适应设置 zoom
                double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
                double zoomFactor = Math.Max(pointCloudWidth / aspectRatio, pointCloudHeight);

                translateX = (float)-viewBox.Value.Center.X;
                translateY = (float)-viewBox.Value.Center.Y;
                if (!Enable2DView)
                {
                    translateZ = (float)-viewBox.Value.Center.Z - (float)zoomFactor * 1.2f; // 适当调整视距
                }
                else
                {
                    var scaleWdith = pointCloudWidth / aspectRatio;
                    if (scaleWdith > pointCloudHeight)
                        translateZ = (float)(openGLControl.Width / scaleWdith); // 适当调整视距
                    else
                        translateZ = (float)(openGLControl.Height / pointCloudHeight); // 适当调整视距
                }
            }
            switch (ViewMode)
            {
                case ViewMode.Top:
                    break;
                case ViewMode.Front:
                    UpdateRotationMatrix(90, 0);
                    break;
                case ViewMode.Left:
                    UpdateRotationMatrix(0, -90);
                    break;
                case ViewMode.Right:
                    UpdateRotationMatrix(0, 90);
                    break;
                default: break;
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
        private void UpdateRotationMatrix(float angleX, float angleY, CxPoint3D? rotationPt = null)
        {
            if (!rotationPt.HasValue)
            {
                rotationPt = pointCloudCenter; // 如果没有指定旋转点，则使用点云中心
            }
            // 平移到旋转中心
            float[] translateToCenter = new float[16];
            for (int i = 0; i < 16; i++)
                translateToCenter[i] = (i % 5 == 0) ? 1.0f : 0.0f;
            translateToCenter[12] = -rotationPt.Value.X;
            translateToCenter[13] = -rotationPt.Value.Y;
            translateToCenter[14] = -rotationPt.Value.Z;

            // 平移回原点
            float[] translateBack = new float[16];
            for (int i = 0; i < 16; i++)
                translateBack[i] = (i % 5 == 0) ? 1.0f : 0.0f;
            translateBack[12] = rotationPt.Value.X;
            translateBack[13] = rotationPt.Value.Y;
            translateBack[14] = rotationPt.Value.Z;

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
            MultiplyMatrix(rotationMatrix, translateToCenter);
            MultiplyMatrix(rotationMatrix, rotationX);
            MultiplyMatrix(rotationMatrix, rotationY);
            MultiplyMatrix(rotationMatrix, translateBack);
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
        private CxPoint3D? GetMouseWorldPosition(int mouseX, int mouseY)
        {
            OpenGL gl = openGLControl.OpenGL;

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
        // 将点应用逆旋转矩阵
        private CxPoint3D ApplyInverseRotation(CxPoint3D point)
        {
            // 计算旋转矩阵的逆矩阵
            float[] inverseRotationMatrix = new float[16];
            Array.Copy(rotationMatrix, inverseRotationMatrix, 16);
            InvertMatrix(inverseRotationMatrix);

            // 将点转换为齐次坐标
            float[] pointVector = { point.X, point.Y, point.Z, 1.0f };
            float[] resultVector = new float[4];

            // 应用逆旋转矩阵
            for (int i = 0; i < 4; i++)
            {
                resultVector[i] = 0;
                for (int j = 0; j < 4; j++)
                {
                    resultVector[i] += inverseRotationMatrix[i * 4 + j] * pointVector[j];
                }
            }

            // 返回转换后的点
            return new CxPoint3D(resultVector[0], resultVector[1], resultVector[2]);
        }
        // 矩阵求逆
        private void InvertMatrix(float[] matrix)
        {
            // 为了简化，这里假设矩阵是正交矩阵，直接转置即可
            for (int i = 0; i < 3; i++)
            {
                for (int j = i + 1; j < 3; j++)
                {
                    float temp = matrix[i * 4 + j];
                    matrix[i * 4 + j] = matrix[j * 4 + i];
                    matrix[j * 4 + i] = temp;
                }
            }
        }
        #endregion
    }
}
