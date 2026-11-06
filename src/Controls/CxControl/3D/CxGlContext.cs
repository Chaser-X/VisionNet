using System;
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
    /// OpenTK exposes OpenGL as a set of static entry points (<c>OpenTK.Graphics.OpenGL.GL</c>)
    /// that operate on the context current on the calling thread. This wrapper therefore carries
    /// no per-call state beyond the owning control: it exists so the render items and helper
    /// services can be handed a single object that answers "how big is the drawable?" and "make
    /// my context current" without referencing a concrete binding.
    /// </remarks>
    public sealed class CxGlContext
    {
        private readonly GLControl _control;

        /// <summary>Creates a context handle for <paramref name="control"/>.</summary>
        internal CxGlContext(GLControl control)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
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

        /// <summary>Makes this context current on the calling thread.</summary>
        internal void MakeCurrent()
        {
            if (_control.IsDisposed) return;
            _control.MakeCurrent();
        }

        /// <summary>Presents the rendered back buffer to the screen (double-buffered swap).</summary>
        internal void Present()
        {
            if (_control.IsDisposed) return;
            _control.SwapBuffers();
        }

        /// <summary>
        /// The WinForms control that hosts the drawable. Internal so the public surface of this
        /// facade stays free of binding types; the camera uses it to subscribe to input events
        /// and request repaints.
        /// </summary>
        internal System.Windows.Forms.Control Host => _control;
    }
}
