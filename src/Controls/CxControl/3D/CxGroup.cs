using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// A node in the lightweight scene graph managed by <see cref="CxDisplay"/>.
    /// Holds a local <see cref="Transform"/> (relative to its parent), optional child groups,
    /// and leaf render items. Every leaf keeps the local pose it had when it was added; the
    /// effective world pose is <c>WorldOfGroup(this) × LocalMatrix</c>.
    /// </summary>
    /// <remarks>
    /// Only <see cref="ICxPoseable"/> (advanced) render items can be added as leaves.
    /// Group membership is owned by <see cref="CxDisplay"/> — create groups via
    /// <see cref="CxDisplay.CreateGroup(string, CxGroup, CxMatrix4X4)"/> and attach items via
    /// <see cref="CxDisplay.AddToGroup(CxGroup, ICxObjRenderItem)"/>.
    /// </remarks>
    public class CxGroup
    {
        private readonly List<CxGroupLeaf> _leaves = new List<CxGroupLeaf>();
        private readonly List<CxGroup> _childGroups = new List<CxGroup>();

        internal CxGroup(string name)
        {
            Name = string.IsNullOrEmpty(name) ? "Group" : name;
        }

        /// <summary>Gets the group name.</summary>
        public string Name { get; }

        /// <summary>Gets the parent group, or <c>null</c> if this is a root group.</summary>
        public CxGroup Parent { get; internal set; }

        private CxMatrix4X4 _transform = CxMatrix4X4.Identity();

        /// <summary>
        /// Gets or sets the group's local transform, relative to its parent (or world space
        /// when root). Animate this value (via <see cref="CxDisplay.SetGroupTransform"/>) to
        /// move the group and everything beneath it.
        /// </summary>
        public CxMatrix4X4 Transform
        {
            get => _transform;
            set => _transform = value ?? CxMatrix4X4.Identity();
        }

        /// <summary>Gets the leaf render items directly owned by this group.</summary>
        public IReadOnlyList<CxGroupLeaf> Leaves => _leaves;

        /// <summary>Gets the nested child groups directly owned by this group.</summary>
        public IReadOnlyList<CxGroup> ChildGroups => _childGroups;

        /// <summary>Gets whether this group owns no leaves and no child groups.</summary>
        public bool IsEmpty => _leaves.Count == 0 && _childGroups.Count == 0;

        /// <summary>
        /// Computes this group's world matrix by walking up the parent chain:
        /// <c>WorldOfGroup(this) = WorldOfGroup(Parent) × Transform</c>.
        /// </summary>
        public CxMatrix4X4 GetWorldMatrix()
        {
            var parentWorld = Parent?.GetWorldMatrix() ?? CxMatrix4X4.Identity();
            return parentWorld * _transform;
        }

        /// <summary>
        /// Recomputes and writes the world pose of every leaf in this group's subtree:
        /// <c>item.ModelMatrix = WorldOfGroup(owner) × LocalMatrix</c>.
        /// Disposed leaves are skipped.
        /// </summary>
        internal void ApplyWorldRecursive()
        {
            var world = GetWorldMatrix();
            foreach (var leaf in _leaves)
            {
                if (leaf.Item == null || leaf.Item.IsDisposed) continue;
                if (leaf.Item is ICxPoseable poseable)
                    poseable.ModelMatrix = world * leaf.LocalMatrix;
            }
            foreach (var child in _childGroups)
                child.ApplyWorldRecursive();
        }

        /// <summary>
        /// Restores every leaf in the subtree to the local pose it had before entering the
        /// group, and moves nested groups to root.
        /// </summary>
        internal void RestoreLocalPosesRecursive()
        {
            foreach (var leaf in _leaves)
            {
                if (leaf.Item == null || leaf.Item.IsDisposed) continue;
                if (leaf.Item is ICxPoseable poseable)
                    poseable.ModelMatrix = leaf.LocalMatrix;
            }
            foreach (var child in _childGroups)
                child.RestoreLocalPosesRecursive();
        }

        internal bool Contains(ICxObjRenderItem item)
            => FindLeaf(item) != null;

        internal CxGroupLeaf FindLeaf(ICxObjRenderItem item)
        {
            foreach (var leaf in _leaves)
                if (ReferenceEquals(leaf.Item, item)) return leaf;
            foreach (var child in _childGroups)
            {
                var found = child.FindLeaf(item);
                if (found != null) return found;
            }
            return null;
        }

        internal void AddLeaf(ICxObjRenderItem item, CxMatrix4X4 localMatrix)
        {
            _leaves.Add(new CxGroupLeaf(item, localMatrix ?? CxMatrix4X4.Identity()));
        }

        internal bool RemoveLeaf(ICxObjRenderItem item)
        {
            for (int i = 0; i < _leaves.Count; i++)
            {
                if (ReferenceEquals(_leaves[i].Item, item))
                {
                    _leaves.RemoveAt(i);
                    return true;
                }
            }
            foreach (var child in _childGroups)
                if (child.RemoveLeaf(item)) return true;
            return false;
        }

        /// <summary>Removes leaves whose item has been disposed.</summary>
        internal void RemoveDisposedLeaves()
        {
            for (int i = _leaves.Count - 1; i >= 0; i--)
                if (_leaves[i].Item == null || _leaves[i].Item.IsDisposed)
                    _leaves.RemoveAt(i);
            foreach (var child in _childGroups)
                child.RemoveDisposedLeaves();
        }

        internal void AddChildGroup(CxGroup child)
        {
            if (child == null || ReferenceEquals(child, this)) return;
            if (IsAncestorOf(child))
                throw new InvalidOperationException("Cannot nest a group inside its own descendant.");

            child.Parent?.RemoveChildGroup(child);
            child.Parent = this;
            if (!_childGroups.Contains(child)) _childGroups.Add(child);
        }

        internal bool RemoveChildGroup(CxGroup child)
        {
            if (_childGroups.Remove(child))
            {
                child.Parent = null;
                return true;
            }
            return false;
        }

        internal void CollectAllGroups(List<CxGroup> acc)
        {
            acc.Add(this);
            foreach (var child in _childGroups)
                child.CollectAllGroups(acc);
        }

        internal void CollectAllLeaves(List<CxGroupLeaf> acc)
        {
            acc.AddRange(_leaves);
            foreach (var child in _childGroups)
                child.CollectAllLeaves(acc);
        }

        private bool IsAncestorOf(CxGroup node)
        {
            for (var p = node; p != null; p = p.Parent)
                if (ReferenceEquals(p, this)) return true;
            return false;
        }
    }

    /// <summary>
    /// A single render item attached to a <see cref="CxGroup"/>, together with the local pose
    /// captured when it entered the group (<c>LocalMatrix</c>). The effective world pose is
    /// <c>WorldOfGroup(owner) × LocalMatrix</c>.
    /// </summary>
    public sealed class CxGroupLeaf
    {
        internal CxGroupLeaf(ICxObjRenderItem item, CxMatrix4X4 localMatrix)
        {
            Item = item;
            LocalMatrix = localMatrix;
        }

        /// <summary>Gets the attached render item.</summary>
        public ICxObjRenderItem Item { get; }

        /// <summary>
        /// Gets or sets the item's pose relative to its owning group. Captured at attach time
        /// and restored on detach.
        /// </summary>
        public CxMatrix4X4 LocalMatrix { get; set; }
    }
}
