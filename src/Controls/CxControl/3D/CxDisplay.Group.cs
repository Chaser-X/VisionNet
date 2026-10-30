namespace VisionNet.Controls
{
    /// <summary>
    /// Group / scene-graph entry point. Relationship and pose management lives entirely in
    /// <see cref="CxGroupManager"/> (exposed as <see cref="Groups"/>); this partial only provides
    /// the read-only lookups the manager needs to resolve keyed members. Resource lifetime
    /// (render queues, keys, disposal) stays with <see cref="CxDisplay"/>.
    /// </summary>
    public partial class CxDisplay
    {
        private readonly CxGroupManager _groupManager;

        #region Group manager (组管理入口)

        /// <summary>
        /// Gets the manager that owns this display's groups and their member relationships.
        /// All group operations (create / add / remove / pose) go through it.
        /// </summary>
        public CxGroupManager Groups => _groupManager;

        #endregion Group manager (组管理入口)

        #region Key lookups for the group manager (组管理只读查询)

        /// <summary>Resolves the surface item currently registered under <paramref name="key"/>, or <c>null</c>.</summary>
        internal ICxObjRenderItem ResolveSurfaceItem(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _registry.GetByKey(key);
        }

        /// <summary>Gets the key <paramref name="item"/> is currently registered under, or <c>null</c>.</summary>
        internal string GetSurfaceKeyOf(ICxObjRenderItem item)
        {
            if (item == null) return null;
            return _registry.GetKeyOf(item);
        }


        /// <summary>Gets whether <paramref name="item"/> is currently in this display's overlay queue.</summary>
        internal bool ContainsOverlayItem(AbstractRenderItem item)
        {
            if (item == null) return false;
            return _registry.ContainsOverlay(item);
        }

        #endregion Key lookups for the group manager (组管理只读查询)
    }
}
