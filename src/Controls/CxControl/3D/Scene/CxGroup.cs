using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// A node in the lightweight scene graph owned by <see cref="CxGroupManager"/>.
    /// Holds a local <see cref="Transform"/> (relative to its parent), optional child groups,
    /// and leaf members. Every leaf keeps the local pose it had when it was added; the effective
    /// world pose is <c>WorldOfGroup(this) × LocalMatrix</c>.
    /// </summary>
    /// <remarks>
    /// Relationships and poses are managed by <see cref="CxGroupManager"/> (available as
    /// <see cref="CxDisplay.Groups"/>); this class only exposes the tree structure. Surface-class
    /// members are bound by key (see <see cref="CxGroupLeaf.Key"/>) so that replacing the resource
    /// with <c>Set*(..., key: sameKey)</c> automatically re-binds the member to the new object.
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
        /// Gets the group's local transform, relative to its parent (or world space when root).
        /// Animate this value through <see cref="CxGroupManager.SetTransform(CxGroup, CxMatrix4X4)"/>
        /// to move the group and everything beneath it.
        /// </summary>
        public CxMatrix4X4 Transform
        {
            get => _transform;
            internal set => _transform = value ?? CxMatrix4X4.Identity();
        }

        /// <summary>Gets the leaf members directly owned by this group.</summary>
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

        internal void AddLeaf(CxGroupLeaf leaf)
        {
            if (leaf != null) _leaves.Add(leaf);
        }

        /// <summary>Removes a directly owned leaf; nested groups are not searched.</summary>
        internal bool RemoveLeaf(CxGroupLeaf leaf) => _leaves.Remove(leaf);

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

        /// <summary>
        /// Removes every directly owned leaf matching <paramref name="match"/> (nested groups are not
        /// searched) and returns the number of leaves removed.
        /// </summary>
        internal int RemoveLeaves(Predicate<CxGroupLeaf> match)
        {
            if (match == null) return 0;

            int removed = 0;
            for (int i = _leaves.Count - 1; i >= 0; i--)
            {
                if (!match(_leaves[i])) continue;
                _leaves.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        private bool IsAncestorOf(CxGroup node)
        {
            for (var p = node; p != null; p = p.Parent)
                if (ReferenceEquals(p, this)) return true;
            return false;
        }
    }

    /// <summary>
    /// A single member attached to a <see cref="CxGroup"/>, together with the local pose captured
    /// when it entered the group (<c>LocalMatrix</c>). The effective world pose is
    /// <c>WorldOfGroup(owner) × LocalMatrix</c>.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="Key"/> / <see cref="Item"/> is set: surface-class members are
    /// bound by key — the resource is resolved through the display's key registry on every group
    /// operation, so a replacement under the same key is picked up automatically — while overlay
    /// members are bound by direct reference.
    /// </remarks>
    public sealed class CxGroupLeaf
    {
        internal CxGroupLeaf(ICxGroupMember item, CxMatrix4X4 localMatrix)
        {
            Item = item;
            Key = null;
            LocalMatrix = localMatrix ?? CxMatrix4X4.Identity();
        }

        internal CxGroupLeaf(string key, CxMatrix4X4 localMatrix)
        {
            Key = key;
            Item = null;
            LocalMatrix = localMatrix ?? CxMatrix4X4.Identity();
        }

        /// <summary>
        /// Gets the directly referenced member (overlay items), or <c>null</c> for keyed
        /// (surface) leaves.
        /// </summary>
        public ICxGroupMember Item { get; internal set; }

        /// <summary>
        /// Gets the key this leaf is bound to (surface items), or <c>null</c> for direct-reference
        /// leaves. The current resource is resolved through the display's key registry; while the
        /// key has no live resource the leaf is skipped.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets or sets the member's pose relative to its owning group. Captured at attach time
        /// and restored on detach.
        /// </summary>
        public CxMatrix4X4 LocalMatrix { get; internal set; }
    }
}
