using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Legacy trackball camera implemented with a raw rotation matrix.
    /// <para>
    /// This implementation is superseded by <see cref="CxAdvancedTrackBallCamera"/> and is kept
    /// for reference only. Prefer <see cref="CxAdvancedTrackBallCamera"/> for new code.
    /// </para>
    /// </summary>
    public class CxTrackBallCamera : ICamera
    {
        #region Private fields

        private OpenGLControl _glControl;
        private float _translateX    = 0f;
        private float _translateY    = 0f;
        private float _translateZ    = -10f;
        private bool  _isDragging    = false;
        private bool  _isRotating    = false;
        private int   _lastMouseX;
        private int   _lastMouseY;
        private float[] _rotationMatrix = new float[16];
        private float _translateSpeed  = 0.5f;
        private CxPoint3D _pointCloudCenter = new CxPoint3D();

        private float _originalZ      = -10f;
        private bool  _firstFitView   = true;
        private bool  _disposed       = false;

        #endregion

        #region Public properties

        /// <inheritdoc/>
        public ViewMode ViewMode { get; set; } = ViewMode.Front;

        /// <inheritdoc/>
        public bool Enable2DView { get; set; } = false;

        /// <inheritdoc/>
        public CxPoint3D? RotationPoint { get; set; } = null;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the camera, resets the rotation matrix to identity,
        /// and subscribes to control mouse events.
        /// </summary>
        /// <param name="glControl">The OpenGL host control.</param>
        public CxTrackBallCamera(OpenGLControl glControl)
        {
            _glControl = glControl;

            // Identity rotation matrix.
            for (int i = 0; i < 16; i++)
                _rotationMatrix[i] = (i % 5 == 0) ? 1.0f : 0.0f;

            _glControl.MouseDown        += GlControl_MouseDown;
            _glControl.MouseMove        += GlControl_MouseMove;
            _glControl.MouseUp          += GlControl_MouseUp;
            _glControl.MouseWheel       += GlControl_MouseWheel;
            _glControl.MouseDoubleClick += GlControl_MouseDoubleClick;
        }

        #endregion

        #region Mouse event handlers

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                _isDragging = true;
                _lastMouseX = e.X;
                _lastMouseY = e.Y;
            }
            else if (e.Button == MouseButtons.Left)
            {
                _isRotating = true;
                _lastMouseX = e.X;
                _lastMouseY = e.Y;
            }
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int dx = e.X - _lastMouseX;
                int dy = e.Y - _lastMouseY;

                _translateX += dx * _translateSpeed;
                _translateY -= dy * _translateSpeed;

                _lastMouseX = e.X;
                _lastMouseY = e.Y;
            }
            else if (_isRotating)
            {
                int dx = e.X - _lastMouseX;
                int dy = e.Y - _lastMouseY;

                float angleX = -dy * 0.15f;
                float angleY = -dx * 0.15f;

                if (!Enable2DView)
                    UpdateRotationMatrix(angleX, angleY, RotationPoint);

                _lastMouseX = e.X;
                _lastMouseY = e.Y;
            }

            _glControl.Invalidate();
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
                _isDragging = false;
            else if (e.Button == MouseButtons.Left)
                _isRotating = false;
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            // Adaptive zoom: speed scales with current distance.
            float speed = Math.Max(Math.Abs(_translateZ) * 0.02f, 0.005f);
            _translateZ += e.Delta > 0 ? speed : -speed;
            _glControl.Invalidate();
        }

        private void GlControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                CxPoint3D? worldPos = GetMouseWorldPosition(e.X, e.Y);
                if (worldPos.HasValue)
                {
                    CxPoint3D adjusted = ApplyInverseRotation(worldPos.Value);
                    _translateX = -adjusted.X;
                    _translateY = -adjusted.Y;
                    _glControl.Invalidate();
                }
            }
        }

        #endregion

        #region ICamera implementation

        /// <inheritdoc/>
        public void FitView(CxBox3D? viewBox)
        {
            if (ViewMode == ViewMode.None && !_firstFitView) return;
            _firstFitView = false;

            _translateX     = 0f;
            _translateY     = 0f;
            _translateZ     = -10f;
            _translateSpeed = 0.5f;
            _pointCloudCenter = new CxPoint3D();

            // Reset to identity rotation.
            for (int i = 0; i < 16; i++)
                _rotationMatrix[i] = (i % 5 == 0) ? 1.0f : 0.0f;

            if (viewBox.HasValue)
            {
                _pointCloudCenter = new CxPoint3D(
                    (float)viewBox.Value.Center.X,
                    (float)viewBox.Value.Center.Y,
                    (float)viewBox.Value.Center.Z);

                double w = viewBox.Value.Size.Width;
                double h = viewBox.Value.Size.Height;
                double d = viewBox.Value.Size.Depth;

                // Choose pan speed from the second-smallest dimension.
                double min1 = Math.Min(w, h), min2 = Math.Min(h, d), min3 = Math.Min(d, w);
                if (min1 == min2)      _translateSpeed = (float)min3 / 400f;
                else if (min2 == min3) _translateSpeed = (float)min1 / 400f;
                else                   _translateSpeed = (float)min2 / 400f;

                double aspect   = (double)_glControl.Width / _glControl.Height;
                double zoomDist = Math.Max(w / aspect, h);

                _translateX = -(float)viewBox.Value.Center.X;
                _translateY = -(float)viewBox.Value.Center.Y;

                if (!Enable2DView)
                {
                    _translateZ = -(float)viewBox.Value.Center.Z - (float)zoomDist * 1.2f;
                }
                else
                {
                    double scaledW = w / aspect;
                    _translateZ = scaledW > h
                        ? (float)(_glControl.Width  / scaledW)
                        : (float)(_glControl.Height / h);
                }

                _originalZ = _translateZ;
            }

            switch (ViewMode)
            {
                case ViewMode.Front: UpdateRotationMatrix(90, 0);  break;
                case ViewMode.Left:  UpdateRotationMatrix(0, -90); break;
                case ViewMode.Right: UpdateRotationMatrix(0,  90); break;
            }
        }

        /// <inheritdoc/>
        public void LookAtMatrix(OpenGL gl)
        {
            gl.Viewport(0, 0, _glControl.Width, _glControl.Height);

            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();

            if (!Enable2DView)
            {
                double aspect = (double)_glControl.Width / _glControl.Height;
                gl.Perspective(60.0, aspect, 0.01, 1000.0);
            }
            else
            {
                double left   = -_glControl.Width  / 2.0;
                double right  =  _glControl.Width  / 2.0;
                double bottom = -_glControl.Height / 2.0;
                double top    =  _glControl.Height / 2.0;
                gl.Ortho(left, right, bottom, top, -1000.0, 1000.0);
            }

            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();

            if (!Enable2DView)
            {
                gl.Translate(_translateX, _translateY, _translateZ);
            }
            else
            {
                gl.Translate(_translateX, _translateY, 0);
                gl.Scale(_translateZ, -_translateZ, 1);
            }

            gl.MultMatrix(_rotationMatrix);
        }

        #endregion

        #region Matrix helpers

        /// <summary>
        /// Incrementally applies X-axis and Y-axis rotations (in degrees) around
        /// <paramref name="rotationPt"/> to the accumulated rotation matrix.
        /// </summary>
        /// <param name="angleX">Rotation around the screen-horizontal axis (degrees).</param>
        /// <param name="angleY">Rotation around the screen-vertical axis (degrees).</param>
        /// <param name="rotationPt">World-space pivot, or <c>null</c> to use the point-cloud centre.</param>
        private void UpdateRotationMatrix(float angleX, float angleY, CxPoint3D? rotationPt = null)
        {
            CxPoint3D pivot = rotationPt ?? _pointCloudCenter;

            float[] toCenter = Identity4x4();
            toCenter[12] = -pivot.X;
            toCenter[13] = -pivot.Y;
            toCenter[14] = -pivot.Z;

            float[] toBack = Identity4x4();
            toBack[12] = pivot.X;
            toBack[13] = pivot.Y;
            toBack[14] = pivot.Z;

            float cosX = (float)Math.Cos(angleX * Math.PI / 180.0);
            float sinX = (float)Math.Sin(angleX * Math.PI / 180.0);
            float cosY = (float)Math.Cos(angleY * Math.PI / 180.0);
            float sinY = (float)Math.Sin(angleY * Math.PI / 180.0);

            float[] rotX = Identity4x4();
            rotX[5]  =  cosX; rotX[6]  = -sinX;
            rotX[9]  =  sinX; rotX[10] =  cosX;

            float[] rotY = Identity4x4();
            rotY[0]  =  cosY; rotY[2]  = sinY;
            rotY[8]  = -sinY; rotY[10] = cosY;

            MultiplyMatrix(_rotationMatrix, toCenter);
            MultiplyMatrix(_rotationMatrix, rotX);
            MultiplyMatrix(_rotationMatrix, rotY);
            MultiplyMatrix(_rotationMatrix, toBack);
        }

        /// <summary>Returns a new 4×4 column-major identity matrix.</summary>
        private static float[] Identity4x4()
        {
            float[] m = new float[16];
            for (int i = 0; i < 16; i++)
                m[i] = (i % 5 == 0) ? 1.0f : 0.0f;
            return m;
        }

        /// <summary>
        /// Right-multiplies <paramref name="result"/> by <paramref name="matrix"/> in place
        /// (<c>result = result × matrix</c>).
        /// </summary>
        private void MultiplyMatrix(float[] result, float[] matrix)
        {
            float[] temp = new float[16];
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    temp[row * 4 + col] = 0;
                    for (int k = 0; k < 4; k++)
                        temp[row * 4 + col] += result[row * 4 + k] * matrix[k * 4 + col];
                }
            Array.Copy(temp, result, 16);
        }

        /// <summary>
        /// Un-projects the depth-buffer value at the given screen pixel to a world-space point.
        /// Returns <c>null</c> if the pixel hits the far plane.
        /// </summary>
        private CxPoint3D? GetMouseWorldPosition(int mouseX, int mouseY)
        {
            var gl = _glControl.OpenGL;

            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            int adjustedY = viewport[3] - mouseY;

            byte[] depthBytes = new byte[4];
            gl.ReadPixels(mouseX, adjustedY, 1, 1,
                OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, depthBytes);
            float depth = BitConverter.ToSingle(depthBytes, 0);

            if (Math.Abs(depth - 1.0f) < 0.00001f) return null;

            var obj = gl.UnProject((double)mouseX, (double)adjustedY, (double)depth);
            return new CxPoint3D((float)obj[0], (float)obj[1], (float)obj[2]);
        }

        /// <summary>
        /// Transforms <paramref name="point"/> by the transpose (inverse for pure-rotation matrices)
        /// of the current rotation matrix.
        /// </summary>
        private CxPoint3D ApplyInverseRotation(CxPoint3D point)
        {
            float[] inv = new float[16];
            Array.Copy(_rotationMatrix, inv, 16);
            TransposeRotation(inv);

            float[] v = { point.X, point.Y, point.Z, 1.0f };
            float[] r = new float[4];
            for (int i = 0; i < 4; i++)
            {
                r[i] = 0;
                for (int j = 0; j < 4; j++)
                    r[i] += inv[i * 4 + j] * v[j];
            }

            return new CxPoint3D(r[0], r[1], r[2]);
        }

        /// <summary>
        /// Transposes the 3×3 rotation sub-matrix of a 4×4 column-major matrix in place.
        /// Equivalent to inverting a pure-rotation matrix.
        /// </summary>
        private static void TransposeRotation(float[] matrix)
        {
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                {
                    float tmp           = matrix[i * 4 + j];
                    matrix[i * 4 + j]   = matrix[j * 4 + i];
                    matrix[j * 4 + i]   = tmp;
                }
        }

        #endregion

        #region IDisposable

        /// <summary>Releases event subscriptions.</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _glControl != null)
                {
                    _glControl.MouseDown        -= GlControl_MouseDown;
                    _glControl.MouseMove        -= GlControl_MouseMove;
                    _glControl.MouseUp          -= GlControl_MouseUp;
                    _glControl.MouseWheel       -= GlControl_MouseWheel;
                    _glControl.MouseDoubleClick -= GlControl_MouseDoubleClick;
                }
                _disposed = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Finalizer — releases event subscriptions if <see cref="Dispose()"/> was not called.</summary>
        ~CxTrackBallCamera() => Dispose(false);

        #endregion
    }
}
