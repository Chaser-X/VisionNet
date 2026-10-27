using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Group / scene-graph API. Lets callers assemble composite models from multiple
    /// Modern shader-backed render items and world-space geometry overlays, then drive them with a
    /// shared, animatable transform.
    /// </summary>
    public partial class CxDisplay
    {
        // Flat registry of every group created through this display (nested groups included).
        private readonly List<CxGroup> _groups = new List<CxGroup>();

        #region Group creation (创建分组)

        /// <summary>
        /// Creates a new group and registers it with this display. Pass a non-null
        /// <paramref name="parent"/> to nest the group; its <paramref name="transform"/> is then
        /// interpreted relative to the parent, otherwise relative to world space.
        /// </summary>
        /// <param name="name">Group name (for identification).</param>
        /// <param name="parent">Optional parent group.</param>
        /// <param name="transform">Optional initial local transform; identity when <c>null</c>.</param>
        public CxGroup CreateGroup(string name, CxGroup parent = null, CxMatrix4X4 transform = null)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => CreateGroup(name, parent, transform))); return null; }

            var group = new CxGroup(name) { Transform = transform ?? CxMatrix4X4.Identity() };
            _groups.Add(group);
            if (parent != null)
            {
                parent.AddChildGroup(group);
                parent.ApplyWorldRecursive();
            }
            return group;
        }
        #endregion Group creation (创建分组)

        #region Attach / detach items (挂载/卸载 Item)

        /// <summary>
        /// Attaches a surface / point-cloud / mesh render item to
        /// <paramref name="group"/>. The item's current pose is captured as its local matrix,
        /// so attaching without changing the group's transform does not move it.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="item"/> is <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">When the item has no transform support.</exception>
        public void AddToGroup(CxGroup group, ICxObjRenderItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item is ICxGroupMember member && member.SupportsGroupTransform)
            {
                AddMemberToGroup(group, member);
                return;
            }
            throw new NotSupportedException(
                "Only ICxGroupMember render items with transform support can participate in group motion.");
        }

        /// <summary>
        /// Attaches a world-space geometry overlay item (segment / point / polygon / plane / box /
        /// world text / world axes) to <paramref name="group"/>. Screen-space HUD items are rejected.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="item"/> is <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">When <paramref name="item"/> is a screen-space HUD item.</exception>
        public void AddToGroup(CxGroup group, AbstractRenderItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!item.SupportsGroupTransform)
                throw new NotSupportedException("Screen-space overlay items cannot participate in group motion.");
            AddMemberToGroup(group, item);
        }

        /// <summary>
        /// Nests <paramref name="child"/> under <paramref name="parent"/>. The child's
        /// <see cref="CxGroup.Transform"/> is reinterpreted as relative to the parent.
        /// </summary>
        public void AddToGroup(CxGroup parent, CxGroup child)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (InvokeRequired) { BeginInvoke(new Action(() => AddToGroup(parent, child))); return; }

            parent.AddChildGroup(child);
            parent.ApplyWorldRecursive();
            SafeInvalidate();
        }

        private void AddMemberToGroup(CxGroup group, ICxGroupMember member)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (InvokeRequired) { BeginInvoke(new Action(() => AddMemberToGroup(group, member))); return; }

            // Move the item out of any previous group first, keeping its current world pose.
            RemoveItemFromAllGroups(member, restorePose: false);

            // Convert the current world pose into the target group's local frame so attaching
            // (or re-parenting) never makes the item jump.
            var world = member.ModelMatrix ?? CxMatrix4X4.Identity();
            var groupWorld = group.GetWorldMatrix();
            var local = groupWorld.Inverse() * world;
            group.AddLeaf(member, local);
            group.ApplyWorldRecursive();
            SafeInvalidate();
        }

        /// <summary>
        /// Detaches a render item from <paramref name="group"/>, restoring the pose
        /// it had before it entered the group.
        /// </summary>
        /// <returns><c>true</c> when the item was attached to the group and removed.</returns>
        public bool RemoveFromGroup(CxGroup group, ICxObjRenderItem item)
            => item is ICxGroupMember member && RemoveMemberFromGroup(group, member);

        /// <summary>
        /// Detaches a world-space geometry overlay item from <paramref name="group"/>, restoring
        /// the pose it had before it entered the group.
        /// </summary>
        /// <returns><c>true</c> when the item was attached to the group and removed.</returns>
        public bool RemoveFromGroup(CxGroup group, AbstractRenderItem item)
            => RemoveMemberFromGroup(group, item);

        private bool RemoveMemberFromGroup(CxGroup group, ICxGroupMember member)
        {
            if (group == null || member == null) return false;
            if (InvokeRequired) { BeginInvoke(new Action(() => RemoveMemberFromGroup(group, member))); return false; }

            var leaf = group.FindLeaf(member);
            if (leaf == null) return false;

            group.RemoveLeaf(member);
            member.ModelMatrix = leaf.LocalMatrix;
            SafeInvalidate();
            return true;
        }

        /// <summary>
        /// Removes a group (and its nested groups) from the display. By default every leaf in
        /// the subtree is restored to its pre-group pose; pass <c>false</c> to keep the
        /// current world pose.
        /// </summary>
        public void RemoveGroup(CxGroup group, bool restoreChildPose = true)
        {
            if (group == null) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => RemoveGroup(group, restoreChildPose))); return; }

            group.Parent?.RemoveChildGroup(group);
            if (restoreChildPose)
                group.RestoreLocalPosesRecursive();

            var all = new List<CxGroup>();
            group.CollectAllGroups(all);
            foreach (var g in all)
            {
                _groups.Remove(g);
                if (!ReferenceEquals(g, group)) g.Parent = null;
            }
            SafeInvalidate();
        }
        #endregion Attach / detach items (挂载/卸载 Item)

        #region Animation (分组动画)

        /// <summary>
        /// Sets a group's local transform and recomputes the world poses of its whole subtree.
        /// Safe to call from a background simulation thread — the update is marshalled to the
        /// UI thread and the view is invalidated.
        /// </summary>
        public void SetGroupTransform(CxGroup group, CxMatrix4X4 transform)
        {
            if (group == null) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => SetGroupTransform(group, transform))); return; }

            group.RemoveDisposedLeaves();
            group.Transform = transform ?? CxMatrix4X4.Identity();
            group.ApplyWorldRecursive();
            SafeInvalidate();
        }
        #endregion Animation (分组动画)

        #region Introspection (查询)

        /// <summary>Gets the group that directly owns <paramref name="item"/>, or <c>null</c>.</summary>
        public CxGroup FindGroupOf(ICxObjRenderItem item)
            => item is ICxGroupMember member ? FindGroupOfMember(member) : null;

        /// <summary>Gets the group that directly owns <paramref name="item"/>, or <c>null</c>.</summary>
        public CxGroup FindGroupOf(AbstractRenderItem item)
            => FindGroupOfMember(item);

        private CxGroup FindGroupOfMember(ICxGroupMember member)
        {
            if (member == null) return null;
            foreach (var group in _groups)
                if (group.Contains(member)) return group;
            return null;
        }
        #endregion Introspection (查询)

        #region Internal lifecycle hooks (内部生命周期)

        /// <summary>
        /// Detaches the given items from every group without restoring their pose. Called when
        /// the display disposes or replaces surface / overlay items so groups never retain
        /// stale entries.
        /// </summary>
        internal void ForgetGroups(IEnumerable<object> items)
        {
            if (items == null) return;
            foreach (var obj in items)
                if (obj is ICxGroupMember member)
                    RemoveItemFromAllGroups(member, restorePose: false);
        }

        /// <summary>Removes a single item from any group it belongs to.</summary>
        private void RemoveItemFromAllGroups(ICxGroupMember member, bool restorePose)
        {
            foreach (var group in _groups)
            {
                var leaf = group.FindLeaf(member);
                if (leaf == null) continue;

                group.RemoveLeaf(member);
                if (restorePose) member.ModelMatrix = leaf.LocalMatrix;
                break;
            }
        }
        #endregion Internal lifecycle hooks (内部生命周期)
    }
}
