using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Owns every group of a <see cref="CxDisplay"/> and the relationships between groups and
    /// render items. Only relationship composition and poses are managed here — the manager never
    /// disposes resources, never moves keys and never touches the render queues; resource
    /// lifetime (queues, keys, GL release) stays with <see cref="CxDisplay"/>.
    /// </summary>
    /// <remarks>
    /// Surface-class members (mesh / point cloud / structured surface) are bound by <b>key</b>: a
    /// leaf stores the key plus its local pose, so replacing the resource with
    /// <c>Set*(..., key: sameKey)</c> automatically re-binds the group slot to the new object.
    /// While a key has no live resource the leaf is skipped (kept, so a later resource under the
    /// same key is picked up again). Overlay members are bound by direct reference and cannot be
    /// re-bound.
    /// </remarks>
    public sealed class CxGroupManager
    {
        // Flat registry of every group owned by the display (nested groups included).
        private readonly List<CxGroup> _groups = new List<CxGroup>();
        private readonly CxDisplay _display;

        internal CxGroupManager(CxDisplay display)
        {
            _display = display ?? throw new ArgumentNullException(nameof(display));
        }

        #region Group creation (创建分组)

        /// <summary>
        /// Creates a new group and registers it with the owning display. Pass a non-null
        /// <paramref name="parent"/> to nest the group; its <paramref name="transform"/> is then
        /// interpreted relative to the parent, otherwise relative to world space.
        /// </summary>
        /// <param name="name">Group name (for identification).</param>
        /// <param name="parent">Optional parent group.</param>
        /// <param name="transform">Optional initial local transform; identity when <c>null</c>.</param>
        public CxGroup Create(string name, CxGroup parent = null, CxMatrix4X4 transform = null)
        {
            if (TryMarshal(() => Create(name, parent, transform))) return null;

            var group = new CxGroup(name) { Transform = transform ?? CxMatrix4X4.Identity() };
            _groups.Add(group);
            if (parent != null)
            {
                parent.AddChildGroup(group);
                ApplyWorld(parent);
            }
            return group;
        }

        #endregion Group creation (创建分组)

        #region Attach / detach items (挂载/卸载 Item)

        /// <summary>
        /// Attaches a surface-class render item (structured surface / point cloud / mesh) to
        /// <paramref name="group"/>. The member is bound by the item's current key, so a later
        /// <c>Set*(..., key: sameKey)</c> re-binds the group slot to the new resource. The item's
        /// current world pose is captured as its local matrix.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="group"/> or <paramref name="item"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">When the item is disposed or has no key in this display.</exception>
        /// <exception cref="NotSupportedException">When the item has no transform support.</exception>
        public void Add(CxGroup group, ICxObjRenderItem item)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!(item is ICxGroupMember member) || !member.SupportsGroupTransform)
                throw new NotSupportedException(
                    "Only ICxGroupMember render items with transform support can participate in group motion.");

            if (TryMarshal(() => Add(group, item))) return;

            if (!_groups.Contains(group))
                throw new ArgumentException("The group is not registered with this display.", nameof(group));
            if (item.IsDisposed)
                throw new ArgumentException("The item has been disposed.", nameof(item));

            string key = _display.GetSurfaceKeyOf(item);
            if (key == null)
                throw new ArgumentException(
                    "The surface item is not registered in this display; create it with a Set* call first.", nameof(item));

            RemoveLeafFromAllGroups(key, member);
            AttachLeaf(group, new CxGroupLeaf(key, ToLocalMatrix(group, member)));
        }

        /// <summary>
        /// Attaches a world-space geometry overlay item (segment / point / polygon / plane / box /
        /// world text / world axes) to <paramref name="group"/>. Overlay members are bound by
        /// direct reference and cannot be re-bound later.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="group"/> or <paramref name="item"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">When the item is disposed or not part of this display.</exception>
        /// <exception cref="NotSupportedException">When <paramref name="item"/> is a screen-space HUD item.</exception>
        public void Add(CxGroup group, AbstractRenderItem item)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!item.SupportsGroupTransform)
                throw new NotSupportedException("Screen-space overlay items cannot participate in group motion.");

            if (TryMarshal(() => Add(group, item))) return;

            if (!_groups.Contains(group))
                throw new ArgumentException("The group is not registered with this display.", nameof(group));
            if (item.IsDisposed)
                throw new ArgumentException("The item has been disposed.", nameof(item));
            if (!_display.ContainsOverlayItem(item))
                throw new ArgumentException(
                    "The overlay item is not registered in this display; create it with a Set* call first.", nameof(item));

            RemoveLeafFromAllGroups(null, item);
            AttachLeaf(group, new CxGroupLeaf(item, ToLocalMatrix(group, item)));
        }

        /// <summary>
        /// Nests <paramref name="child"/> under <paramref name="parent"/>. The child's local
        /// transform is reinterpreted as relative to the parent.
        /// </summary>
        public void Add(CxGroup parent, CxGroup child)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (child == null) throw new ArgumentNullException(nameof(child));

            if (TryMarshal(() => Add(parent, child))) return;

            if (!_groups.Contains(parent) || !_groups.Contains(child))
                throw new ArgumentException("Both groups must be registered with this display.");

            parent.AddChildGroup(child);
            ApplyWorld(parent);
            _display.SafeInvalidate();
        }

        /// <summary>
        /// Detaches a surface-class member from <paramref name="group"/> (searching nested groups
        /// too). Only the relationship changes — the resource is left untouched.
        /// </summary>
        /// <returns><c>true</c> when the member was attached to the group and removed.</returns>
        public bool Remove(CxGroup group, ICxObjRenderItem item)
        {
            if (group == null || item == null) return false;
            if (TryMarshal(() => Remove(group, item))) return false;

            string key = _display.GetSurfaceKeyOf(item);
            var leaf = FindLeaf(group, key != null ? ByKey(key) : ByReference(item), out var owner);
            return DetachLeaf(owner, leaf);
        }

        /// <summary>
        /// Detaches a world-space overlay member from <paramref name="group"/> (searching nested
        /// groups too). Only the relationship changes — the resource is left untouched.
        /// </summary>
        /// <returns><c>true</c> when the member was attached to the group and removed.</returns>
        public bool Remove(CxGroup group, AbstractRenderItem item)
        {
            if (group == null || item == null) return false;
            if (TryMarshal(() => Remove(group, item))) return false;

            var leaf = FindLeaf(group, ByReference(item), out var owner);
            return DetachLeaf(owner, leaf);
        }

        /// <summary>
        /// Detaches the member bound to <paramref name="key"/>. Also works while the key has no
        /// live resource, which is the way to drop a dangling leaf.
        /// </summary>
        /// <returns><c>true</c> when a leaf with that key was found and removed.</returns>
        public bool Remove(CxGroup group, string key)
        {
            if (group == null || string.IsNullOrEmpty(key)) return false;
            if (TryMarshal(() => Remove(group, key))) return false;

            var leaf = FindLeaf(group, ByKey(key), out var owner);
            return DetachLeaf(owner, leaf);
        }

        /// <summary>
        /// Removes a group (and its nested groups) from the registry. By default every leaf in the
        /// subtree is restored to its pre-group pose; pass <c>false</c> to keep the current world
        /// pose. Render items themselves are not disposed.
        /// </summary>
        public void RemoveGroup(CxGroup group, bool restoreChildPose = true)
        {
            if (group == null) return;
            if (TryMarshal(() => RemoveGroup(group, restoreChildPose))) return;

            group.Parent?.RemoveChildGroup(group);
            if (restoreChildPose)
                RestorePoses(group);

            var all = new List<CxGroup>();
            group.CollectAllGroups(all);
            foreach (var g in all)
            {
                _groups.Remove(g);
                if (!ReferenceEquals(g, group)) g.Parent = null;
            }
            _display.SafeInvalidate();
        }

        #endregion Attach / detach items (挂载/卸载 Item)

        #region Animation (分组动画)

        /// <summary>
        /// Sets a group's local transform and recomputes the world poses of its whole subtree.
        /// Safe to call from a background simulation thread — the update is marshalled to the UI
        /// thread and the view is invalidated. Members whose key has no live resource are skipped.
        /// </summary>
        public void SetTransform(CxGroup group, CxMatrix4X4 transform)
        {
            if (group == null) return;
            if (TryMarshal(() => SetTransform(group, transform))) return;

            group.Transform = transform ?? CxMatrix4X4.Identity();
            ApplyWorld(group);
            _display.SafeInvalidate();
        }

        /// <summary>
        /// Updates a single member's pose relative to its owning group. The local matrix is also
        /// the pose restored when the member leaves the group.
        /// </summary>
        /// <returns><c>true</c> when the member was found in the group's subtree.</returns>
        public bool SetMemberLocalTransform(CxGroup group, ICxGroupMember member, CxMatrix4X4 localTransform)
        {
            if (group == null || member == null) return false;
            if (TryMarshal(() => SetMemberLocalTransform(group, member, localTransform))) return false;

            string key = member is ICxObjRenderItem obj ? _display.GetSurfaceKeyOf(obj) : null;
            var leaf = FindLeaf(group, key != null ? ByKey(key) : ByReference(member), out _);
            if (leaf == null) return false;

            leaf.LocalMatrix = localTransform ?? CxMatrix4X4.Identity();
            ApplyWorld(group);
            _display.SafeInvalidate();
            return true;
        }

        #endregion Animation (分组动画)

        #region Introspection (查询)

        /// <summary>Gets the group that directly owns <paramref name="item"/>, or <c>null</c>.</summary>
        public CxGroup FindGroupOf(ICxObjRenderItem item)
            => item == null ? null : FindGroupOfMember(item);

        /// <summary>Gets the group that directly owns <paramref name="item"/>, or <c>null</c>.</summary>
        public CxGroup FindGroupOf(AbstractRenderItem item)
            => item == null ? null : FindGroupOfMember(item);

        private CxGroup FindGroupOfMember(object member)
        {
            string key = member is ICxObjRenderItem obj ? _display.GetSurfaceKeyOf(obj) : null;
            var match = key != null ? ByKey(key) : ByReference(member);

            foreach (var group in _groups)
                foreach (var leaf in group.Leaves)
                    if (match(leaf)) return group;

            return null;
        }

        #endregion Introspection (查询)

        #region Internal lifecycle hooks (内部生命周期)

        /// <summary>
        /// Re-applies the stored local pose to whichever resource is currently registered under
        /// <paramref name="key"/>. Called by <see cref="CxDisplay"/> after a keyed item was
        /// replaced by <c>Set*</c>. Leaves whose key has no live resource are skipped.
        /// </summary>
        internal void OnSurfaceKeyChanged(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            bool touched = false;
            foreach (var group in _groups)
            {
                var world = group.GetWorldMatrix();
                foreach (var leaf in group.Leaves)
                {
                    if (leaf.Key != key) continue;
                    if (TryApplyWorldPose(leaf, world)) touched = true;
                }
            }

            if (touched) _display.SafeInvalidate();
        }

        /// <summary>
        /// Detaches direct-reference members (overlay items) that are about to be disposed.
        /// Keyed (surface) leaves are deliberately left untouched so they can re-bind to a later
        /// resource under the same key.
        /// </summary>
        internal void ForgetOverlayMembers(IEnumerable<object> items)
        {
            if (items == null) return;
            foreach (var obj in items)
            {
                if (!(obj is ICxGroupMember member)) continue;
                foreach (var group in _groups)
                {
                    var leaf = FindDirectLeaf(group, member);
                    if (leaf == null) continue;
                    group.RemoveLeaf(leaf);
                    break;
                }
            }
        }

        /// <summary>Removes every group from the registry (used on reset / disposal).</summary>
        internal void Clear() => _groups.Clear();

        #endregion Internal lifecycle hooks (内部生命周期)

        #region Internal helpers (内部辅助)

        /// <summary>
        /// Marshals <paramref name="action"/> to the UI thread. Returns <c>true</c> when the call
        /// was queued instead of executed, in which case the public entry points return
        /// <c>false</c> / <c>null</c> — the behaviour this API had before it moved into the manager.
        /// </summary>
        private bool TryMarshal(Action action)
        {
            if (!_display.InvokeRequired) return false;
            _display.BeginInvoke(action);
            return true;
        }

        /// <summary>Converts a member's current world pose into the local frame of <paramref name="group"/>.</summary>
        private static CxMatrix4X4 ToLocalMatrix(CxGroup group, ICxPoseable member)
        {
            var world = member.ModelMatrix ?? CxMatrix4X4.Identity();
            return group.GetWorldMatrix().Inverse() * world;
        }

        /// <summary>Adds the leaf, applies the subtree's world transform and refreshes the view.</summary>
        private void AttachLeaf(CxGroup group, CxGroupLeaf leaf)
        {
            group.AddLeaf(leaf);
            ApplyWorld(group);
            _display.SafeInvalidate();
        }

        /// <summary>
        /// Resolves the member a leaf currently points at: keyed (surface) leaves look the key up
        /// in the display, direct (overlay) leaves return their stored reference.
        /// </summary>
        private ICxGroupMember Resolve(CxGroupLeaf leaf)
        {
            if (leaf.Key == null) return leaf.Item;
            return _display.ResolveSurfaceItem(leaf.Key) as ICxGroupMember;
        }

        /// <summary>Matches keyed leaves by key.</summary>
        private static Func<CxGroupLeaf, bool> ByKey(string key) => leaf => leaf.Key == key;

        /// <summary>Matches direct-reference leaves by identity; keyed leaves never match.</summary>
        private static Func<CxGroupLeaf, bool> ByReference(object member)
            => leaf => member != null && ReferenceEquals(leaf.Item, member);

        /// <summary>Finds a leaf in the group's subtree; <paramref name="owner"/> receives the direct parent.</summary>
        private static CxGroupLeaf FindLeaf(CxGroup group, Func<CxGroupLeaf, bool> match, out CxGroup owner)
        {
            foreach (var leaf in group.Leaves)
            {
                if (match(leaf)) { owner = group; return leaf; }
            }
            foreach (var child in group.ChildGroups)
            {
                var found = FindLeaf(child, match, out var childOwner);
                if (found != null) { owner = childOwner; return found; }
            }
            owner = null;
            return null;
        }

        /// <summary>Finds a direct-reference leaf of this group only (no subtree walk, no key lookup).</summary>
        private static CxGroupLeaf FindDirectLeaf(CxGroup group, ICxGroupMember member)
        {
            foreach (var leaf in group.Leaves)
                if (ReferenceEquals(leaf.Item, member)) return leaf;
            return null;
        }

        /// <summary>
        /// True for a direct-reference (overlay) leaf whose item has been disposed. Keyed (surface)
        /// leaves are never stale: they wait for a later resource under the same key.
        /// </summary>
        private static bool IsStaleDirectLeaf(CxGroupLeaf leaf)
            => leaf.Key == null && (leaf.Item == null || leaf.Item.IsDisposed);

        /// <summary>Detaches the member from whichever group currently owns it, without restoring its pose.</summary>
        private void RemoveLeafFromAllGroups(string key, ICxGroupMember member)
        {
            var match = key != null ? ByKey(key) : ByReference(member);
            foreach (var group in _groups)
            {
                var leaf = FindLeaf(group, match, out var owner);
                if (leaf == null) continue;

                owner.RemoveLeaf(leaf);
                return;
            }
        }

        private bool DetachLeaf(CxGroup owner, CxGroupLeaf leaf)
        {
            if (owner == null || leaf == null) return false;

            owner.RemoveLeaf(leaf);
            TryRestorePose(leaf);
            _display.SafeInvalidate();
            return true;
        }

        /// <summary>
        /// Writes <c>WorldOfGroup(owner) × LocalMatrix</c> to the leaf's member; returns <c>false</c>
        /// when the member cannot be resolved (key without live resource, or disposed item).
        /// </summary>
        private bool TryApplyWorldPose(CxGroupLeaf leaf, CxMatrix4X4 world)
        {
            var member = Resolve(leaf);
            if (member == null || member.IsDisposed) return false;

            member.ModelMatrix = world * leaf.LocalMatrix;
            return true;
        }

        /// <summary>
        /// Restores the leaf's member to its stored local pose; returns <c>false</c> when the member
        /// cannot be resolved.
        /// </summary>
        private bool TryRestorePose(CxGroupLeaf leaf)
        {
            var member = Resolve(leaf);
            if (member == null || member.IsDisposed) return false;

            member.ModelMatrix = leaf.LocalMatrix;
            return true;
        }

        /// <summary>
        /// Applies the subtree's world transform to every resolvable member. Direct (overlay) leaves
        /// whose item has been disposed are dropped — they cannot re-bind, unlike keyed (surface)
        /// leaves which stay so a later resource under the same key picks them up.
        /// </summary>
        private void ApplyWorld(CxGroup group)
        {
            if (group == null) return;

            group.RemoveLeaves(IsStaleDirectLeaf);

            var world = group.GetWorldMatrix();
            foreach (var leaf in group.Leaves)
                TryApplyWorldPose(leaf, world);

            foreach (var child in group.ChildGroups)
                ApplyWorld(child);
        }

        /// <summary>Restores every resolvable member of the subtree to its stored local pose.</summary>
        private void RestorePoses(CxGroup group)
        {
            if (group == null) return;

            foreach (var leaf in group.Leaves)
                TryRestorePose(leaf);

            foreach (var child in group.ChildGroups)
                RestorePoses(child);
        }

        #endregion Internal helpers (内部辅助)
    }
}
