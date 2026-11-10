using System;

namespace VisionNet.Controls
{
    /// <summary>
    /// The part of <see cref="CxDisplay"/> that <see cref="CxGroupManager"/> needs: UI-thread
    /// marshalling and the repaint request.
    /// </summary>
    /// <remarks>
    /// The manager deliberately does not depend on <see cref="CxDisplay"/> itself. Group and pose
    /// management is independent of the frame pipeline, and taking this narrow contract keeps the
    /// two types from referencing each other — the display owns the manager, the manager only
    /// knows how to get back onto the UI thread. Registry lookups are served by
    /// <see cref="CxItemRegistry"/>, which the manager also takes directly.
    /// </remarks>
    internal interface IDisplayHost
    {
        /// <summary>True when the caller is on a thread other than the control's UI thread.</summary>
        bool InvokeRequired { get; }

        /// <summary>Queues <paramref name="action"/> to run on the UI thread.</summary>
        void BeginInvoke(Action action);

        /// <summary>Requests a repaint, marshalling to the UI thread when necessary.</summary>
        void SafeInvalidate();
    }
}
