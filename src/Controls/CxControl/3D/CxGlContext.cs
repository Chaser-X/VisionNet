using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTK;

namespace VisionNet.Controls
{
    /// <summary>
    /// Binding-agnostic handle to the OpenGL context that owns a <see cref="CxDisplay"/>'s GPU
    /// resources. This is the only GL-related type that appears in the public surface of
    /// <c>CxControl</c>: it deliberately hides the underlying OpenTK types so the binding can be
    /// swapped without changing callers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenTK exposes OpenGL as static entry points (<c>OpenTK.Graphics.OpenGL.GL</c>) that operate
    /// on the context <b>current on the calling thread</b> — unlike the previous SharpGL wrapper,
    /// which made its context current implicitly before every call. Every GL operation therefore
    /// has to run with this context bound, and this type is the single place that knows how to bind
    /// it.
    /// </para>
    /// <para><b>Invariant:</b> GL work happens in exactly two shapes —</para>
    /// <list type="bullet">
    ///   <item><description>inside a frame: <c>CxDisplay.OnPaint</c> binds once via
    ///   <see cref="EnsureCurrent"/> and then runs the whole frame; or</description></item>
    ///   <item><description>outside a frame: wrapped in <see cref="RunOnContext(Action)"/> /
    ///   <see cref="RunOnContext{T}(Func{T})"/>, which binds and (in debug builds) asserts the bind
    ///   took effect.</description></item>
    /// </list>
    /// <para>
    /// A new GL call must be placed in one of those two shapes; a bare <c>GL.*</c> call on a thread
    /// that has not bound the context is a bug (it would act on whatever context happens to be
    /// current, or on none).
    /// </para>
    /// </remarks>
    public sealed class CxGlContext
    {
        // One facade per control. GL object names are owned by the control's context, and
        // ModernGLRenderer keys its per-context state by this instance, so a second facade for the
        // same control would silently split that state (duplicated programs/VAOs, leaked names).
        // Creation is therefore guarded; the table is weak, so the entry disappears with the control.
        private static readonly ConditionalWeakTable<GLControl, CxGlContext> Facades
            = new ConditionalWeakTable<GLControl, CxGlContext>();

        private readonly GLControl _control;

        /// <summary>Creates the context handle for <paramref name="control"/>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a facade already exists for <paramref name="control"/>.
        /// </exception>
        internal CxGlContext(GLControl control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (Facades.TryGetValue(control, out _))
                throw new InvalidOperationException(
                    "A CxGlContext already exists for this GLControl. GL object names are per-context, " +
                    "so the facade must be created exactly once per control.");
            Facades.Add(control, this);
            _control = control;
        }

        /// <summary>Width in pixels of the drawable surface.</summary>
        public int Width => _control.ClientSize.Width;

        /// <summary>Height in pixels of the drawable surface.</summary>
        public int Height => _control.ClientSize.Height;

        /// <summary>
        /// <c>true</c> when a real (hardware) GL context was created for the control. <c>false</c>
        /// in the WinForms designer or when context creation failed and OpenTK fell back to its
        /// dummy context, in which case no GL call is safe.
        /// </summary>
        public bool IsValid => _control.HasValidContext;

        /// <summary>
        /// Binds this context on the calling thread. Called once at the top of a frame; use
        /// <see cref="RunOnContext(Action)"/> for GL work that happens outside a frame.
        /// </summary>
        internal void EnsureCurrent()
        {
            if (_control.IsDisposed) return;
            _control.MakeCurrent();
            Debug.Assert(IsContextCurrent(),
                "[CxGlContext] EnsureCurrent did not leave the context current.");
        }

        /// <summary>Presents the rendered back buffer to the screen (double-buffered swap).</summary>
        internal void Present()
        {
            if (_control.IsDisposed) return;
            _control.SwapBuffers();
        }

        /// <summary>
        /// Runs GL work that happens outside a render frame (picking, camera unprojection, resource
        /// disposal, standalone text helpers) with this context bound.
        /// </summary>
        internal void RunOnContext(Action action)
        {
            if (action == null) return;
            EnsureCurrent();
            action();
        }

        /// <summary>Result-returning form of <see cref="RunOnContext(Action)"/>.</summary>
        internal T RunOnContext<T>(Func<T> func)
        {
            if (func == null) return default(T);
            EnsureCurrent();
            return func();
        }

        /// <summary>
        /// The WinForms control that hosts the drawable. Internal so the public surface of this
        /// facade stays free of binding types; the camera uses it to subscribe to input events
        /// and request repaints.
        /// </summary>
        internal System.Windows.Forms.Control Host => _control;

        /// <summary>
        /// Off-screen depth source the owning <see cref="CxDisplay"/> renders into, or <c>null</c>
        /// when it draws straight to the window. Depth-based helpers (picking, camera focus) read
        /// the resolved single-sample depth from here so display MSAA does not corrupt it.
        /// </summary>
        internal CxMsaaRenderTarget DepthTarget { get; set; }

        private bool IsContextCurrent()
        {
            if (_control.IsDisposed || !_control.HasValidContext) return false;
            try { return _control.Context.IsCurrent; }
            catch { return false; }
        }
    }
}
