using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using System.Windows.Input;
using SharpGL;
using SharpGL.SceneGraph.Cameras;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// 现代化3D相机类，实现ICamera接口
    /// </summary>
    public class CxAdvancedTrackBallCamera : ICamera
    {
        #region 私有字段
        private readonly OpenGLControl _glControl;
        private Matrix4x4 _viewMatrix = Matrix4x4.Identity;
        private Matrix4x4 _projectionMatrix = Matrix4x4.Identity;

        private Vector3 _position = new Vector3(0, 0, -10);
        private Vector3 _target = Vector3.Zero;
        private Vector3 _up = Vector3.UnitY;

        private Vector3 _sceneCenter = Vector3.Zero;
        private float _sceneRadius = 10.0f;

        private Point _lastMousePosition;
        private bool _isRotating = false;
        private bool _isPanning = false;

        private float _fieldOfView = 60.0f;
        private float _nearPlane = 0.01f;
        private float _farPlane = 1000.0f;

        private float _panSpeed = 0.01f;
        private float _rotateSpeed = 0.5f;
        private float _zoomSpeed = 0.2f;

        private bool _firstFitView = true;
        private bool _disposedValue = false;
        #endregion

        #region 公共属性
        /// <summary>
        /// 获取或设置视图模式
        /// </summary>
        public ViewMode ViewMode { get; set; } = ViewMode.Front;

        /// <summary>
        /// 获取或设置是否启用2D视图
        /// </summary>
        public bool Enable2DView { get; set; } = false;

        /// <summary>
        /// 获取或设置旋转点
        /// </summary>
        public CxPoint3D? RotationPoint { get; set; } = null;
        #endregion

        #region 构造函数
        /// <summary>
        /// 创建一个新的Modern3DCamera实例
        /// </summary>
        /// <param name="glControl">OpenGL控件</param>
        public CxAdvancedTrackBallCamera(OpenGLControl glControl)
        {
            _glControl = glControl ?? throw new ArgumentNullException(nameof(glControl));
            // 注册事件
            RegisterEvents();
        }

        /// <summary>
        /// 注册控件事件
        /// </summary>
        private void RegisterEvents()
        {
            _glControl.MouseDown += GlControl_MouseDown;
            _glControl.MouseMove += GlControl_MouseMove;
            _glControl.MouseUp += GlControl_MouseUp;
            _glControl.MouseWheel += GlControl_MouseWheel;
            _glControl.MouseDoubleClick += GlControl_MouseDoubleClick;
            _glControl.Resize += GlControl_Resize;
        }

        /// <summary>
        /// 注销控件事件
        /// </summary>
        private void UnregisterEvents()
        {
            _glControl.MouseDown -= GlControl_MouseDown;
            _glControl.MouseMove -= GlControl_MouseMove;
            _glControl.MouseUp -= GlControl_MouseUp;
            _glControl.MouseWheel -= GlControl_MouseWheel;
            _glControl.MouseDoubleClick -= GlControl_MouseDoubleClick;
            _glControl.Resize -= GlControl_Resize;
        }
        #endregion

        #region 事件处理
        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePosition = e.Location;

            if (e.Button == MouseButtons.Left)
            {
                _isRotating = true;
                _glControl.Cursor = Cursors.Hand;
            }
            else if (e.Button == MouseButtons.Middle ||
                    (e.Button == MouseButtons.Left && Control.ModifierKeys.HasFlag(Keys.Shift)))
            {
                _isPanning = true;
                _glControl.Cursor = Cursors.SizeAll;
            }
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isRotating && !Enable2DView)
            {
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;

                RotateCamera(deltaX, deltaY);
            }
            else if (_isPanning)
            {
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;

                PanCamera(deltaX, deltaY);
            }
            _lastMousePosition = e.Location;
            _glControl.Invalidate();
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            _isRotating = false;
            _isPanning = false;
            _glControl.Cursor = Cursors.Default;
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            // 计算缩放因子，根据距离自适应速度
            //float distance = Vector3.Distance(_position, _target);
            float zoomFactor = e.Delta > 0 ? 1.05f : 0.95f;

            ////自适应缩放速度
            //float adaptiveSpeed = Math.Max(distance * 0.001f, 0.01f);
            //zoomFactor = e.Delta > 0 ? 1.0f + adaptiveSpeed : 1.0f - adaptiveSpeed;

            ZoomCamera(zoomFactor);
            // 触发重绘
            _glControl.Invalidate();
        }

        private void GlControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 获取鼠标点击位置的世界坐标
                Vector3? worldPos = ScreenToWorld(e.X, e.Y);
                if (worldPos.HasValue)
                {
                    // 聚焦到点击位置
                    FocusOnPoint(worldPos.Value);
                    _glControl.Invalidate();
                }
            }
        }

        private void GlControl_Resize(object sender, EventArgs e)
        {
        }
        #endregion

        #region 相机操作方法
        /// <summary>
        /// 旋转相机
        /// </summary>
        internal void RotateCamera(int deltaX, int deltaY)
        {
            // 获取旋转中心
            Vector3 rotationCenter;
            if (RotationPoint.HasValue)
            {
                rotationCenter = new Vector3(
                    RotationPoint.Value.X,
                    RotationPoint.Value.Y,
                    RotationPoint.Value.Z);
            }
            else
            {
                rotationCenter = _sceneCenter;
            }

            // 保存原始距离
            float originalDistance = Vector3.Distance(_position, rotationCenter);

            // 计算相机到旋转中心的向量
            Vector3 viewDir = _position - rotationCenter;

            // 计算屏幕空间的旋转轴
            // 屏幕空间的水平轴（对应于世界空间中垂直于视线和上向量的轴）
            Vector3 screenRight = Vector3.Normalize(Vector3.Cross(_up, viewDir));

            // 屏幕空间的垂直轴（对应于世界空间中垂直于视线和右向量的轴）
            Vector3 screenUp = Vector3.Normalize(Vector3.Cross(viewDir, screenRight));

            // 计算旋转角度
            float angleX = -deltaY * _rotateSpeed; // 垂直移动 -> 绕屏幕水平轴旋转
            float angleY = -deltaX * _rotateSpeed; // 水平移动 -> 绕屏幕垂直轴旋转

            // 创建旋转矩阵
            Matrix4x4 rotationX = Matrix4x4.CreateFromAxisAngle(screenRight, angleX * (float)Math.PI / 180.0f);
            Matrix4x4 rotationY = Matrix4x4.CreateFromAxisAngle(screenUp, angleY * (float)Math.PI / 180.0f);
            Matrix4x4 combinedRotation = rotationX * rotationY;

            // 应用旋转到相机位置
            viewDir = Vector3.TransformNormal(viewDir, combinedRotation);
            _position = rotationCenter + viewDir;

            // 更新上向量
            _up = Vector3.TransformNormal(_up, combinedRotation);
            _up = Vector3.Normalize(_up);

            // 更新目标点
            //_target = rotationCenter;
        }
        /// <summary>
        /// 平移相机
        /// </summary>
        internal void PanCamera(int deltaX, int deltaY)
        {
            // 计算相机坐标系
            Vector3 viewDir = Vector3.Normalize(_target - _position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(viewDir, _up));
            Vector3 up = Vector3.Cross(right, viewDir);

            // 自适应平移速度
            float distance = Vector3.Distance(_position, _target);
            float adaptivePanSpeed = _panSpeed * Math.Max(distance * 0.05f, 0.5f);

            // 计算平移距离
            float panX = -deltaX * adaptivePanSpeed;
            float panY = deltaY * adaptivePanSpeed;

            // 应用平移
            Vector3 offset = right * panX + up * panY;
            _position += offset;
            _target += offset;
        }

        /// <summary>
        /// 缩放相机
        /// </summary>
        internal void ZoomCamera(float zoomFactor)
        {
            // 计算相机到目标的向量
            Vector3 viewDir = _position - _target;
            float currentDistance = viewDir.Length();

            // 计算新距离
            float newDistance = currentDistance * zoomFactor;

            // 应用距离限制
            newDistance = Math.Max(_nearPlane, Math.Min(newDistance, _farPlane));

            // 重新计算缩放因子
            float adjustedZoomFactor = newDistance / currentDistance;

            // 应用缩放
            _position = _target + Vector3.Normalize(viewDir) * newDistance;
        }
        // <summary>
        /// 向指定点缩放
        /// </summary>
        /// <param name="zoomPoint">缩放中心点</param>
        /// <param name="zoomFactor">缩放因子（小于1为放大，大于1为缩小）</param>
        internal void ZoomTowardPoint(Vector3 zoomPoint, float zoomFactor)
        {
            // 计算相机到缩放点的向量
            Vector3 cameraToPoint = _position - zoomPoint;

            // 计算当前距离
            float currentDistance = cameraToPoint.Length();

            // 计算新距离，应用限制
            float newDistance = currentDistance * zoomFactor;
            newDistance = Math.Max(_nearPlane, Math.Min(newDistance, _farPlane));

            // 计算实际缩放因子
            float actualZoomFactor = newDistance / currentDistance;

            // 应用缩放
            _position = zoomPoint + cameraToPoint * actualZoomFactor;
        }
        /// <summary>
        /// 聚焦到指定点
        /// </summary>
        internal void FocusOnPoint(Vector3 point)
        {
            // 保持相机到目标的方向和距离
            Vector3 viewDir = _position - _target;

            // 更新目标为新的焦点
            _target = point;

            // 更新相机位置
            _position = _target + viewDir;
        }

        /// <summary>
        /// 设置视图模式
        /// </summary>
        private void SetViewMode(ViewMode viewMode)
        {
            ViewMode = viewMode;

            // 保存当前距离
            float distance = Vector3.Distance(_position, _target);

            // 根据视图模式设置相机位置
            switch (viewMode)
            {
                case ViewMode.Front:
                    _position = _target + new Vector3(0, 0, distance);
                    _up = Vector3.UnitY;
                    break;

                case ViewMode.Top:
                    _position = _target + new Vector3(0, distance, 0);
                    _up = new Vector3(0, 0, -1);
                    break;

                case ViewMode.Left:
                    _position = _target + new Vector3(-distance, 0, 0);
                    _up = Vector3.UnitY;
                    break;

                case ViewMode.Right:
                    _position = _target + new Vector3(distance, 0, 0);
                    _up = Vector3.UnitY;
                    break;

                default:
                    break;
            }
        }
        #endregion

        #region ICamera接口实现
        /// <summary>
        /// 适应视图大小
        /// </summary>
        /// <param name="viewBox">视图边界框</param>
        public void FitView(Box3D? viewBox)
        {
            if (ViewMode == ViewMode.None && !_firstFitView)
            {
                return;
            }
            _firstFitView = false;

            // 重置相机
            _position = new Vector3(0, 0, -10);
            _target = Vector3.Zero;
            _up = Vector3.UnitY;

            if (viewBox.HasValue)
            {
                // 计算场景中心和半径
                _sceneCenter = new Vector3(
                    (float)viewBox.Value.Center.X,
                    (float)viewBox.Value.Center.Y,
                    (float)viewBox.Value.Center.Z
                );

                // 计算场景尺寸
                double sceneWidth = viewBox.Value.Size.Width;
                double sceneHeight = viewBox.Value.Size.Height;
                double sceneDepth = viewBox.Value.Size.Depth;

                // 计算场景半径
                _sceneRadius = (float)Math.Sqrt(
                    sceneWidth * sceneWidth +
                    sceneHeight * sceneHeight +
                    sceneDepth * sceneDepth) * 0.5f;

                // 自适应平移速度
                _panSpeed = _sceneRadius / 500.0f;

                // 设置目标为场景中心
                _target = _sceneCenter;
                RotationPoint = new CxPoint3D(_sceneCenter.X, _sceneCenter.Y, _sceneCenter.Z);

                // 计算合适的相机距离
                float distance = _sceneRadius / (float)Math.Tan(_fieldOfView * 0.5f * Math.PI / 180.0f);

                // 根据当前视图模式设置相机位置
                switch (ViewMode)
                {
                    case ViewMode.Front:
                        _position = _target + new Vector3(0, -distance * 1.1f, 0);
                        _up = new Vector3(0, 0, 1);
                        break;

                    case ViewMode.Top:
                        _position = _target + new Vector3(0, 0, distance * 1.1f);
                        _up = Vector3.UnitY;
                        break;

                    case ViewMode.Left:
                        _position = _target + new Vector3(-distance * 1.1f, 0, 0);
                        _up = new Vector3(0, 0, 1);
                        break;

                    case ViewMode.Right:
                        _position = _target + new Vector3(distance * 1.1f, 0, 0);
                        _up = new Vector3(0, 0, 1);
                        break;

                    default:
                        _position = _target + new Vector3(0, 0, distance * 1.1f);
                        _up = Vector3.UnitY;
                        break;
                }
            }
        }

        /// <summary>
        /// 设置视图矩阵
        /// </summary>
        /// <param name="gl">OpenGL上下文</param>
        public void LookAtMatrix(OpenGL gl)
        {
            // 设置视口
            gl.Viewport(0, 0, _glControl.Width, _glControl.Height);

            // 设置投影矩阵
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();

            if (!Enable2DView)
            {
                // 透视投影
                double aspectRatio = (double)_glControl.Width / _glControl.Height;
                gl.Perspective(_fieldOfView, aspectRatio, _nearPlane, _farPlane);
            }
            else
            {
                // 正交投影
                double aspectRatio = (double)_glControl.Width / _glControl.Height;
                double height = 2.0 * Math.Abs(_position.Z - _target.Z);
                double width = height * aspectRatio;
                gl.Ortho(-width / 2, width / 2, -height / 2, height / 2, _nearPlane, _farPlane);
            }

            // 设置模型视图矩阵
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();

            // 直接使用LookAt函数应用视图变换，避免额外的变换
            gl.LookAt(
                _position.X, _position.Y, _position.Z,
                _target.X, _target.Y, _target.Z,
                _up.X, _up.Y, _up.Z);
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 屏幕坐标转换为世界坐标
        /// </summary>
        private Vector3? ScreenToWorld(int screenX, int screenY)
        {
            OpenGL gl = _glControl.OpenGL;

            // 获取视口
            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);

            // 调整Y坐标（OpenGL坐标系原点在左下角）
            int adjustedY = viewport[3] - screenY;

            // 读取深度值
            byte[] depthBuffer = new byte[4];
            gl.ReadPixels(screenX, adjustedY, 1, 1, OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, depthBuffer);
            float depth = BitConverter.ToSingle(depthBuffer, 0);

            // 检查深度值是否有效
            if (Math.Abs(depth - 1.0f) < 0.00001f)
                return null;

            // 将屏幕坐标转换为世界坐标
            var obj = gl.UnProject((double)screenX, (double)adjustedY, (double)depth);
            return new Vector3((float)obj[0], (float)obj[1], (float)obj[2]);
        }

        /// <summary>
        /// 将Vector3转换为CxPoint3D
        /// </summary>
        private CxPoint3D ToCxPoint3D(Vector3 vector)
        {
            return new CxPoint3D(vector.X, vector.Y, vector.Z);
        }

        /// <summary>
        /// 将CxPoint3D转换为Vector3
        /// </summary>
        private Vector3 ToVector3(CxPoint3D point)
        {
            return new Vector3(point.X, point.Y, point.Z);
        }
        #endregion

        #region IDisposable实现
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // 释放托管资源
                    UnregisterEvents();
                }

                _disposedValue = true;
            }
        }

        ~CxAdvancedTrackBallCamera()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}