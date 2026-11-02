using System;

namespace VisionNet.Controls
{
    /// <summary>
    /// Group / scene-graph entry point. Relationship and pose management lives entirely in
    /// <see cref="CxGroupManager"/> (exposed as <see cref="Groups"/>). Resource lifetime
    /// (render queues, keys, disposal) stays with <see cref="CxDisplay"/>.
    /// </summary>
    public partial class CxDisplay : IDisplayHost
    {
        private readonly CxGroupManager _groupManager;

        #region Group manager (组管理入口)

        /// <summary>
        /// Gets the manager that owns this display's groups and their member relationships.
        /// All group operations (create / add / remove / pose) go through it.
        /// </summary>
        public CxGroupManager Groups => _groupManager;

        #endregion Group manager (组管理入口)

        #region IDisplayHost (供 CxGroupManager 回调)

        // InvokeRequired is the inherited public Control property, so it implements the
        // contract implicitly. The other two are implemented explicitly to avoid widening the
        // public surface (SafeInvalidate stays internal) and to adapt Control's Delegate-based
        // BeginInvoke to the Action shape the contract asks for.
        void IDisplayHost.BeginInvoke(Action action)
        {
            if (action == null) return;
            BeginInvoke((Delegate)action);
        }

        void IDisplayHost.SafeInvalidate() => SafeInvalidate();

        #endregion IDisplayHost (供 CxGroupManager 回调)
    }
}
