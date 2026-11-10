using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// A trackball-style 3D camera that supports perspective and orthographic projection,
    /// mouse-driven rotation / panning / zoom, and scene-fit.
    /// <para>
    /// Mouse bindings:
    /// <list type="bullet">
    ///   <item><description>Left drag — rotate around <see cref="RotationPoint"/> (disabled in 2D mode).</description></item>
    ///   <item><description>Middle drag — pan.</description></item>
    ///   <item><description>Scroll wheel — zoom in / out.</description></item>
    ///   <item><description>Left double-click — focus (re-centre) on the clicked surface point.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class CxTrackBallCamera : ICamera
    {
        #region Private fields

        private readonly CxGlContext _gl;

        // View state.
        private Vector3 _position    = new Vector3(0, 0, -10);
        private Vector3 _target      = Vector3.Zero;
        private Vector3 _up          = Vector3.UnitY;
        private Vector3 _sceneCenter = Vector3.Zero;
        private float   _sceneRadius = 10.0f;

        // Mouse state.
        private Point _lastMousePosition;
        private bool  _isRotating = false;
        private bool  _isPanning  = false;

        // Projection parameters.
        private readonly float _fieldOfView = 60.0f;
        private readonly float _nearPlane   = 0.01f;
        private readonly float _farPlane    = 1000.0f;

        // Speed settings.
        private float _panSpeed    = 0.01f;
        private float _rotateSpeed = 0.5f;

        private bool _firstFitView = true;
        private bool _disposed     = false;

        // View-transform overrides.
        private bool     _isLeftHanded = false;
        private float    _zScale       = 1.0f;
        private ViewMode _viewMode     = ViewMode.Front;

        // Reused by GetMatrices so camera updates do not allocate two 4x4 arrays
        // on every paint while the user is rotating or zooming the view.
        private readonly float[] _projectionMatrix = new float[16];
        private readonly float[] _viewMatrix = new float[16];

        #endregion

        #region Public properties

        /// <inheritdoc/>
        public ViewMode ViewMode
        {
            get => _viewMode;
            set { _viewMode = value; _zScale = 1.0f; }
        }

        /// <inheritdoc/>
        public bool Enable2DView { get; set; } = false;

        /// <inheritdoc/>
        public CxPoint3D? RotationPoint { get; set; } = null;

        /// <summary>
        /// Gets or sets whether the coordinate system is left-handed.
        /// When <c>true</c>, a Y-axis flip is appended to the Modelview matrix after
        /// <c>gl.LookAt</c>, inverting the Y direction so that left-handed data renders correctly.
        /// </summary>
        public bool IsLeftHanded
        {
            get => _isLeftHanded;
            set { _isLeftHanded = value; _gl.Host.Invalidate(); }
        }

        /// <summary>
        /// Gets or sets the Z-axis display scale factor appended to the Modelview matrix.
        /// Values greater than 1 stretch the Z axis; values in (0, 1) compress it.
        /// Clamped to [0.01, ∞). Default is <c>1.0</c>.
        /// </summary>
        public float ZScale
        {
            get => _zScale;
            set { _zScale = Math.Max(0.01f, value); _gl.Host.Invalidate(); }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the camera and subscribes to the mouse / resize events of
        /// <paramref name="glControl"/>.
        /// </summary>
        /// <param name="gl">The GL context of the host control. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="gl"/> is <c>null</c>.</exception>
        public CxTrackBallCamera(CxGlContext gl)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            RegisterEvents();
        }

        #endregion

        #region Event subscription

        private void RegisterEvents()
        {
            _gl.Host.MouseDown        += GlControl_MouseDown;
            _gl.Host.MouseMove        += GlControl_MouseMove;
            _gl.Host.MouseUp          += GlControl_MouseUp;
            _gl.Host.MouseWheel       += GlControl_MouseWheel;
            _gl.Host.MouseDoubleClick += GlControl_MouseDoubleClick;
            _gl.Host.Resize           += GlControl_Resize;
        }

        private void UnregisterEvents()
        {
            _gl.Host.MouseDown        -= GlControl_MouseDown;
            _gl.Host.MouseMove        -= GlControl_MouseMove;
            _gl.Host.MouseUp          -= GlControl_MouseUp;
            _gl.Host.MouseWheel       -= GlControl_MouseWheel;
            _gl.Host.MouseDoubleClick -= GlControl_MouseDoubleClick;
            _gl.Host.Resize           -= GlControl_Resize;
        }

        #endregion

        #region Mouse event handlers

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePosition = e.Location;

            if (e.Button == MouseButtons.Left)
            {
                _isRotating = true;
                _gl.Host.Cursor = Cursors.Hand;
            }
            else if (e.Button == MouseButtons.Middle ||
                     (e.Button == MouseButtons.Left && Control.ModifierKeys.HasFlag(Keys.Shift)))
            {
                _isPanning = true;
                _gl.Host.Cursor = Cursors.SizeAll;
            }
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            int deltaX = e.X - _lastMousePosition.X;
            int deltaY = e.Y - _lastMousePosition.Y;

            bool moved = false;
            if (_isRotating && !Enable2DView)
            {
                RotateCamera(deltaX, deltaY);
                moved = true;
            }
            else if (_isPanning)
            {
                PanCamera(deltaX, deltaY);
                moved = true;
            }

            _lastMousePosition = e.Location;

            // Only a drag changes the view, so only a drag needs a repaint from here. A plain
            // hover already repaints through CxDisplay's tooltip path; invalidating on every
            // hover as well would keep a WM_PAINT permanently pending while the mouse moves,
            // which starves low-priority WM_TIMER work in the host application.
            if (moved) _gl.Host.Invalidate();
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            _isRotating = false;
            _isPanning  = false;
            _gl.Host.Cursor = Cursors.Default;
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys.HasFlag(Keys.Shift))
            {
                // Shift + scroll → Z-scale; camera distance unchanged.
                // ZScale setter calls Invalidate() internally.
                ZScale *= e.Delta > 0 ? 1.05f : 0.95f;
            }
            else
            {
                // 5 % zoom step per wheel notch.
                ZoomCamera(e.Delta > 0 ? 1.05f : 0.95f);
                _gl.Host.Invalidate();
            }
        }

        private void GlControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Vector3? worldPos = ScreenToWorld(e.X, e.Y);
                if (worldPos.HasValue)
                {
                    FocusOnPoint(worldPos.Value);
                    _gl.Host.Invalidate();
                }
            }
        }

        private void GlControl_Resize(object sender, EventArgs e) { }

        #endregion

        #region Camera manipulation

        /// <summary>
        /// Rotates the camera around <see cref="RotationPoint"/> (or the scene centre if not set)
        /// by the given screen-space mouse deltas.
        /// </summary>
        /// <param name="deltaX">Horizontal mouse movement in pixels.</param>
        /// <param name="deltaY">Vertical mouse movement in pixels.</param>
        internal void RotateCamera(int deltaX, int deltaY)
        {
            // RotationPoint / _sceneCenter are in the caller's (logical) world; the camera orbits in
            // its own (mirrored) world, so the pivot is mapped across the same way as the target.
            Vector3 pivot = Mirror(RotationPoint.HasValue
                ? new Vector3(RotationPoint.Value.X, RotationPoint.Value.Y, RotationPoint.Value.Z)
                : _sceneCenter);

            Vector3 viewDir     = _position - pivot;
            Vector3 screenRight = Vector3.Normalize(Vector3.Cross(_up, viewDir));
            Vector3 screenUp    = Vector3.Normalize(Vector3.Cross(viewDir, screenRight));

            // _rotateSpeed is in degrees; convert to radians for CreateFromAxisAngle.
            float angleX = -deltaY * _rotateSpeed * (float)Math.PI / 180f;
            float angleY = -deltaX * _rotateSpeed * (float)Math.PI / 180f;

            Matrix4x4 rot = Matrix4x4.CreateFromAxisAngle(screenRight, angleX)
                          * Matrix4x4.CreateFromAxisAngle(screenUp,    angleY);

            _position = pivot + Vector3.TransformNormal(viewDir, rot);
            _up       = Vector3.Normalize(Vector3.TransformNormal(_up, rot));
        }

        /// <summary>
        /// Pans the camera (moves both position and target) by the given screen-space mouse deltas.
        /// Pan speed scales with the distance to the target.
        /// </summary>
        /// <param name="deltaX">Horizontal mouse movement in pixels.</param>
        /// <param name="deltaY">Vertical mouse movement in pixels.</param>
        internal void PanCamera(int deltaX, int deltaY)
        {
            Vector3 viewDir = Vector3.Normalize(_target - _position);
            Vector3 right   = Vector3.Normalize(Vector3.Cross(viewDir, _up));
            Vector3 up      = Vector3.Cross(right, viewDir);

            float distance         = Vector3.Distance(_position, _target);
            float adaptivePanSpeed = _panSpeed * Math.Max(distance * 0.05f, 0.5f);

            Vector3 offset = right * (-deltaX * adaptivePanSpeed)
                           + up    * ( deltaY * adaptivePanSpeed);
            _position += offset;
            _target   += offset;
        }

        /// <summary>
        /// Zooms the camera by moving it along the view vector.
        /// <paramref name="zoomFactor"/> greater than 1 zooms out; less than 1 zooms in.
        /// The resulting distance is clamped to [<c>nearPlane</c>, <c>farPlane</c>].
        /// </summary>
        /// <param name="zoomFactor">Multiplicative zoom factor.</param>
        internal void ZoomCamera(float zoomFactor)
        {
            Vector3 viewDir     = _position - _target;
            float   newDistance = Math.Max(_nearPlane,
                                  Math.Min(viewDir.Length() * zoomFactor, _farPlane));
            _position = _target + Vector3.Normalize(viewDir) * newDistance;
        }

        /// <summary>
        /// Zooms the camera toward a specific world-space point rather than the current target.
        /// </summary>
        /// <param name="zoomPoint">World-space zoom origin.</param>
        /// <param name="zoomFactor">Multiplicative zoom factor (&lt;1 = zoom in, &gt;1 = zoom out).</param>
        internal void ZoomTowardPoint(Vector3 zoomPoint, float zoomFactor)
        {
            Vector3 toCamera    = _position - zoomPoint;
            float   newDistance = Math.Max(_nearPlane,
                                  Math.Min(toCamera.Length() * zoomFactor, _farPlane));
            _position = zoomPoint + Vector3.Normalize(toCamera) * newDistance;
        }

        /// <summary>
        /// Moves the camera so that <paramref name="point"/> ends up at the viewport centre,
        /// preserving the direction and distance from the previous target.
        /// </summary>
        /// <param name="point">World-space point to bring to the viewport centre.</param>
        internal void FocusOnPoint(Vector3 point)
        {
            Vector3 offset = _position - _target;

            // The modelview is built as LookAt * Scale(1,-1,1) * Scale(1,1,_zScale), so the
            // world point actually rendered at the viewport centre is S * _target, not _target.
            // Pre-apply the inverse of those view transforms to the requested point so that
            // `point` itself is centred (mirrors the Y axis in left-handed mode, folds in ZScale).
            Vector3 target = point;
            if (_isLeftHanded) target.Y = -target.Y;
            target.Z *= _zScale;

            _target   = target;
            _position = target + offset;
        }

        /// <summary>
        /// Sets the camera up-direction vector.
        /// Passing <c>null</c> resets to <see cref="Vector3.UnitY"/>.
        /// </summary>
        /// <param name="up">Desired up direction, or <c>null</c> to use the default.</param>
        public void SetDefaultUpView(Vector3? up)
        {
            _up = up ?? Vector3.UnitY;
        }

        /// <summary>
        /// Maps a point of the caller's (logical) world into the camera's own world. When
        /// <see cref="IsLeftHanded"/> the view matrix mirrors the world in Y (see
        /// <see cref="GetMatrices"/>), so the point actually rendered at a given camera target is
        /// <c>Mirror(target)</c> — everything the camera compares against (its target and the
        /// rotation pivot) must be mirrored too, otherwise the scene is not centred on fit and the
        /// drag no longer matches the displayed world. The up-vector is deliberately <b>not</b>
        /// mirrored (that would also flip the screen X axis); this matches <see cref="FocusOnPoint"/>.
        /// </summary>
        private Vector3 Mirror(Vector3 v) => _isLeftHanded ? new Vector3(v.X, -v.Y, v.Z) : v;

        /// <summary>Repositions the camera for the given preset view direction.</summary>
        private void SetViewMode(ViewMode viewMode)
        {
            ViewMode = viewMode;
            float distance = Vector3.Distance(_position, _target);

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
            }
        }

        #endregion

        #region ICamera implementation

        /// <summary>
        /// Repositions the camera so the given bounding box fills the viewport,
        /// then applies the current <see cref="ViewMode"/> preset orientation.
        /// The first call is always honoured; subsequent calls with <see cref="ViewMode.None"/>
        /// are ignored to avoid resetting a user-adjusted view.
        /// </summary>
        /// <param name="viewBox">Bounding box to fit, or <c>null</c> to leave the view unchanged.</param>
        public void FitView(CxBox3D? viewBox)
        {
            if (ViewMode == ViewMode.None && !_firstFitView) return;
            _firstFitView = false;

            if (!viewBox.HasValue) return;

            _sceneCenter = new Vector3(
                (float)viewBox.Value.Center.X,
                (float)viewBox.Value.Center.Y,
                (float)viewBox.Value.Center.Z);

            double w = viewBox.Value.Size.Width;
            double h = viewBox.Value.Size.Height;
            double d = viewBox.Value.Size.Depth;

            _sceneRadius = (float)Math.Sqrt(w * w + h * h + d * d) * 0.5f;
            _panSpeed    = _sceneRadius / 500f;
            RotationPoint = new CxPoint3D(_sceneCenter.X, _sceneCenter.Y, _sceneCenter.Z);

            // Distance required so the scene fits inside the FOV.
            float dist = _sceneRadius / (float)Math.Tan(_fieldOfView * 0.5f * Math.PI / 180f);

            // Build the preset in the caller's (logical) world, then re-target it into the camera's
            // own world. When IsLeftHanded the view matrix mirrors the world in Y (see GetMatrices),
            // so the point actually rendered at the viewport centre is Mirror(_target); the target
            // must be mirrored too, otherwise the scene is not centred on fit. The offset and _up are
            // kept as-is (mirroring _up would flip the screen X axis as well).
            Vector3 logicalOffset;
            switch (ViewMode)
            {
                case ViewMode.Front:
                    logicalOffset = new Vector3(0, -dist * 1.1f, 0);
                    _up = new Vector3(0, 0, 1);
                    break;
                case ViewMode.Top:
                    logicalOffset = new Vector3(0, 0, dist * 1.1f);
                    _up = Vector3.UnitY;
                    break;
                case ViewMode.Left:
                    logicalOffset = new Vector3(-dist * 1.1f, 0, 0);
                    _up = new Vector3(0, 0, 1);
                    break;
                case ViewMode.Right:
                    logicalOffset = new Vector3(dist * 1.1f, 0, 0);
                    _up = new Vector3(0, 0, 1);
                    break;
                default:
                    logicalOffset = new Vector3(0, 0, dist * 1.1f);
                    break;
            }

            _target   = Mirror(_sceneCenter);
            _position = _target + logicalOffset;
        }

        /// <summary>
        /// Computes row-major projection and view matrices for the current frame.
        /// The matrices are uploaded by the modern renderer, so camera math does not
        /// depend on the legacy OpenGL matrix stack.
        /// </summary>
        public void GetMatrices(int width, int height, out float[] projection, out float[] view)
        {
            float aspect = height > 0 ? (float)width / height : 1f;
            if (!Enable2DView)
            {
                float f = 1f / (float)Math.Tan(_fieldOfView * Math.PI / 360.0);
                float nf = 1f / (_nearPlane - _farPlane);
                projection = _projectionMatrix;
                projection[0] = f / aspect; projection[1] = 0; projection[2] = 0; projection[3] = 0;
                projection[4] = 0; projection[5] = f; projection[6] = 0; projection[7] = 0;
                projection[8] = 0; projection[9] = 0;
                projection[10] = (_farPlane + _nearPlane) * nf;
                projection[11] = 2f * _farPlane * _nearPlane * nf;
                projection[12] = 0; projection[13] = 0; projection[14] = -1f; projection[15] = 0;
            }
            else
            {
                float heightWorld = 2f * Math.Abs(_position.Z - _target.Z);
                if (heightWorld < 1e-4f) heightWorld = 1f;
                float widthWorld = heightWorld * aspect;
                float left = -widthWorld / 2f, right = widthWorld / 2f;
                float bottom = -heightWorld / 2f, top = heightWorld / 2f;
                projection = _projectionMatrix;
                projection[0] = 2f / (right - left); projection[1] = 0; projection[2] = 0;
                projection[3] = -(right + left) / (right - left);
                projection[4] = 0; projection[5] = 2f / (top - bottom); projection[6] = 0;
                projection[7] = -(top + bottom) / (top - bottom);
                projection[8] = 0; projection[9] = 0;
                projection[10] = -2f / (_farPlane - _nearPlane);
                projection[11] = -(_farPlane + _nearPlane) / (_farPlane - _nearPlane);
                projection[12] = 0; projection[13] = 0; projection[14] = 0; projection[15] = 1f;
            }

            Vector3 forward = Vector3.Normalize(_target - _position);
            Vector3 side = Vector3.Normalize(Vector3.Cross(forward, _up));
            Vector3 up = Vector3.Cross(side, forward);
            view = _viewMatrix;
            view[0] = side.X; view[1] = side.Y; view[2] = side.Z; view[3] = -Vector3.Dot(side, _position);
            view[4] = up.X; view[5] = up.Y; view[6] = up.Z; view[7] = -Vector3.Dot(up, _position);
            view[8] = -forward.X; view[9] = -forward.Y; view[10] = -forward.Z;
            view[11] = Vector3.Dot(forward, _position);
            view[12] = 0; view[13] = 0; view[14] = 0; view[15] = 1f;
            if (_isLeftHanded)
            {
                view[1] = -view[1]; view[5] = -view[5]; view[9] = -view[9]; view[13] = -view[13];
            }
            if (Math.Abs(_zScale - 1f) > 1e-6f)
            {
                view[2] *= _zScale; view[6] *= _zScale; view[10] *= _zScale; view[14] *= _zScale;
            }
        }

        /// <summary>
        /// Publishes the camera to the renderer for the current frame, so world-space draws
        /// (including the standalone text helpers) use this projection/view pair.
        /// </summary>
        /// <remarks>
        /// This used to also load the legacy fixed-function matrix stack, for callers that issued
        /// fixed-function draws afterwards. That path was removed with the OpenTK migration: under a
        /// core profile it silently produced an identity projection, and CxDisplay never used it (it
        /// uploads the matrices as shader uniforms). Standalone callers must draw through
        /// <see cref="ModernGLRenderer"/>/<see cref="CxGlContext"/>.
        /// </remarks>
        /// <param name="gl">Active OpenGL context.</param>
        public void LookAtMatrix(CxGlContext gl)
        {
            gl.RunOnContext(() =>
            {
                GL.Viewport(0, 0, _gl.Width, _gl.Height);
                float[] projection, view;
                GetMatrices(_gl.Width, _gl.Height, out projection, out view);
                ModernGLRenderer.For(gl).SetSceneMatrices(projection, view);
            });
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reads the depth buffer at the given screen pixel and un-projects it to world space.
        /// Returns <c>null</c> if the pixel hits the far plane (background).
        /// </summary>
        private Vector3? ScreenToWorld(int screenX, int screenY)
        {
            var gl = _gl;
            if (gl == null || !gl.IsValid) return null;
            // Runs from a double-click handler, outside the render frame.
            return gl.RunOnContext<Vector3?>(() =>
            {
                float? depth = CxMsaaRenderTarget.ReadDepthAtPixel(gl, screenX, screenY);
                if (!depth.HasValue || Math.Abs(depth.Value - 1.0f) < 0.00001f) return null;

                float[] projection, view;
                GetMatrices(_gl.Width, _gl.Height, out projection, out view);
                var renderer = ModernGLRenderer.For(gl);
                renderer.SetSceneMatrices(projection, view);

                int glY = _gl.Height - 1 - screenY;   // Flip Y to OpenGL convention.
                CxPoint3D world;
                if (!renderer.Unproject(screenX + 0.5f, glY + 0.5f, depth.Value, out world)) return null;
                return new Vector3(world.X, world.Y, world.Z);
            });
        }

        #endregion

        #region IDisposable

        /// <summary>Releases managed resources (event subscriptions).</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    UnregisterEvents();
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
