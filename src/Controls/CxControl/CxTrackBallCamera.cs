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
        #region ˽���ֶ�
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
        #region ����
        public ViewMode ViewMode { get; set; } = ViewMode.Front;
        public bool Enable2DView { get; set; } = false;
        #endregion
        #region ���캯��
        public CxTrackBallCamera(OpenGLControl openGLControl)
        {
            this.openGLControl = openGLControl;
            // ��ʼ����ת����Ϊ��λ����
            for (int i = 0; i < 16; i++)
                rotationMatrix[i] = (i % 5 == 0) ? 1.0f : 0.0f;

            // �����¼�
            this.openGLControl.MouseDown += OpenGLControl_MouseDown;
            this.openGLControl.MouseMove += OpenGLControl_MouseMove;
            this.openGLControl.MouseUp += OpenGLControl_MouseUp;
            this.openGLControl.MouseWheel += OpenGLControl_MouseWheel;
        }
        #endregion
        #region ����¼�����
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

                // ������ת�Ƕ�
                float angleX = deltaY * 0.5f;
                float angleY = deltaX * 0.5f;

                if(!Enable2DView)
                    // ������ת����
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
        #region ��Ⱦ����
        /// <summary>
        /// ������ͼģʽ
        /// </summary>
        public void FitView(Box3D? viewBox)
        {
            if (!viewBox.HasValue || ViewMode == ViewMode.None)
            {
                return;
            }
            // ������ƵĿ�Ⱥ͸߶�
            double pointCloudWidth = viewBox.Value.Size.Width;
            double pointCloudHeight = viewBox.Value.Size.Height;

            //����ƽ�����ŵ��ٶ�ϵ��
            translateSpeed = translateZSpeed = (float)Math.Min(pointCloudWidth, pointCloudHeight) / 400.0f;
            // ���ݴ����С�͵��ƵĴ�С����Ӧ���� zoom
            double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
            double zoomFactor = Math.Max(pointCloudWidth / aspectRatio, pointCloudHeight);

            translateX = (float)-viewBox.Value.Center.X;
            translateY = (float)-viewBox.Value.Center.Y;
            if(!Enable2DView)
                translateZ = (float)-viewBox.Value.Center.Z - (float)zoomFactor * 1.2f; //  �ʵ������Ӿ�
            else
            {
                var scaleWdith = pointCloudWidth / aspectRatio;
                if(scaleWdith > pointCloudHeight)
                    translateZ = (float)(openGLControl.Width / scaleWdith); //  �ʵ������Ӿ�
                else
                    translateZ = (float)(openGLControl.Height / pointCloudHeight); //  �ʵ������Ӿ�
            }

            // ������ת����
            for (int i = 0; i < 16; i++)
                rotationMatrix[i] = (i % 5 == 0) ? 1.0f : 0.0f;

            // ������ͼģʽ���ó�ʼ��ת����
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
        /// ��ͼ�ռ�����
        /// </summary>
        public void LookAtMatrix(OpenGL gl)
        {
            // �����ӿ�
            gl.Viewport(0, 0, openGLControl.Width, openGLControl.Height);

            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();

            if (!Enable2DView)
            {
                // ����͸��ͶӰ
                double nearPlane = 0.01; // ���ü���
                double farPlane = 1000.0; // Զ�ü���
                double aspectRatio = (double)openGLControl.Width / (double)openGLControl.Height;
                double fov = 60.0; // �ӳ���
                gl.Perspective(fov, aspectRatio, nearPlane, farPlane); // ����͸��ͶӰ
            }
            else
            {
                // ��������ͶӰ
                double left = -openGLControl.Width / 2;
                double right = openGLControl.Width / 2;
                double bottom = -openGLControl.Height / 2;
                double top = openGLControl.Height / 2;
                gl.Ortho(left, right, bottom, top, -1000.0, 1000.0); // ��������ͶӰ
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
            // ������ת����
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
            // ������ת����
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
