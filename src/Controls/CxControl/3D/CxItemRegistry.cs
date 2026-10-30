using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace VisionNet.Controls
{
    /// <summary>
    /// Owns every mutable item collection of a <see cref="CxDisplay"/>: the surface-item queue and
    /// its key map, the overlay queue and its key map, the per-item GL resource pool, and the
    /// deferred-release queue — together with the single lock that guards them.
    /// </summary>
    /// <remarks>
    /// <para><b>Lock ordering:</b> <c>CxDisplay._renderLock</c> → this registry's lock. Never take
    /// the render lock while holding this one.</para>
    /// <para>Every member takes the lock itself, so a caller cannot forget it or get the order
    /// wrong. Operations that must be atomic as a group (register/replace, detach, dispose) are
    /// single methods here rather than critical sections opened by the caller.</para>
    /// <para><b>Callbacks run while the lock is held.</b> The members that report affected items
    /// (<see cref="RegisterSurfaceItem"/>, <see cref="ClearSurfaceItems"/>, <see cref="DisposeAll"/>,
    /// <see cref="ClearOverlayItems"/>) invoke the caller's delegate from inside the critical
    /// section, so those delegates must not block, and any callback that re-enters the registry or
    /// the display must be on the same thread (the lock is re-entrant for it). This matches the
    /// previous behaviour, where the same delegates ran inside <c>CxDisplay._resourceLock</c>.</para>
    /// <para>The deferred-release queue is a <see cref="ConcurrentQueue{T}"/>: it is filled from any
    /// thread and drained on the render pass, so enqueue/dequeue need no lock.</para>
    /// </remarks>
    internal sealed class CxItemRegistry
    {
        private readonly object _lock = new object();

        // ── Surface items ────────────────────────────────────────────────────────

        // In draw order. Kept in sync with _keys under _lock.
        private readonly List<ICxObjRenderItem> _items = new List<ICxObjRenderItem>();

        // Maps each caller-supplied key to the item stored under it. A key is unique across all
        // three item types (Surface / PointCloud / Mesh), so setting an existing key replaces
        // whatever item occupies it.
        private readonly Dictionary<string, ICxObjRenderItem> _keys
            = new Dictionary<string, ICxObjRenderItem>(StringComparer.Ordinal);

        // ── Overlay items ────────────────────────────────────────────────────────

        private readonly List<IRenderItem> _renderItems = new List<IRenderItem>();

        // Overlay keys are a namespace of their own: they never collide with surface keys, and
        // only items registered with a key appear here.
        private readonly Dictionary<string, AbstractRenderItem> _overlayKeys
            = new Dictionary<string, AbstractRenderItem>(StringComparer.Ordinal);

        // ── GL resources ─────────────────────────────────────────────────────────

        // One handle per registered surface item. Items never touch these; CxDisplay owns the
        // lifecycle and frees them through the deferred-release queue.
        private readonly Dictionary<ICxObjRenderItem, GLResourceHandle> _pool
            = new Dictionary<ICxObjRenderItem, GLResourceHandle>();

        private readonly ConcurrentQueue<GLResourceHandle> _pendingRelease
            = new ConcurrentQueue<GLResourceHandle>();

        // ── Surface queries ──────────────────────────────────────────────────────

        /// <summary>Number of registered surface items.</summary>
        internal int Count
        {
            get { lock (_lock) return _items.Count; }
        }

        /// <summary>Returns the surface item stored under <paramref name="key"/>, or <c>null</c>.</summary>
        internal ICxObjRenderItem GetByKey(string key)
        {
            if (key == null) return null;
            lock (_lock) return _keys.TryGetValue(key, out var item) ? item : null;
        }

        /// <summary>Returns a new array with the current surface items, in draw order.</summary>
        internal ICxObjRenderItem[] SnapshotItems()
        {
            lock (_lock) return _items.ToArray();
        }

        /// <summary>Returns a new list with the current surface items, in draw order.</summary>
        internal List<ICxObjRenderItem> CopyItems()
        {
            lock (_lock) return new List<ICxObjRenderItem>(_items);
        }

        /// <summary>Copies the current surface items into <paramref name="destination"/> (cleared first).</summary>
        internal void CopyItemsInto(List<ICxObjRenderItem> destination)
        {
            lock (_lock)
            {
                destination.Clear();
                destination.AddRange(_items);
            }
        }

        /// <summary>Returns the registered surface items that are of type <typeparamref name="T"/>.</summary>
        internal List<T> CopyItemsOfType<T>() where T : class
        {
            lock (_lock)
            {
                var result = new List<T>();
                foreach (var item in _items)
                    if (item is T typed) result.Add(typed);
                return result;
            }
        }

        /// <summary>
        /// Fills the frame snapshots: surface items in draw order, plus the handles index-aligned
        /// with them. Both lists are cleared first and filled atomically, so the draw loop can use
        /// them after the lock has been released.
        /// </summary>
        internal void CopyFrameSnapshot(List<ICxObjRenderItem> items, List<GLResourceHandle> handles)
        {
            lock (_lock)
            {
                items.Clear();
                handles.Clear();
                foreach (var item in _items)
                {
                    items.Add(item);
                    handles.Add(_pool.TryGetValue(item, out var handle) ? handle : null);
                }
            }
        }

        /// <summary>Invokes <paramref name="action"/> for every registered GL handle.</summary>
        internal void ForEachPoolHandle(Action<GLResourceHandle> action)
        {
            lock (_lock)
                foreach (var handle in _pool.Values)
                    action(handle);
        }

        // ── Overlay queries ──────────────────────────────────────────────────────

        /// <summary>Returns the overlay item stored under <paramref name="key"/>, or <c>null</c>.</summary>
        internal AbstractRenderItem GetOverlayByKey(string key)
        {
            if (key == null) return null;
            lock (_lock) return _overlayKeys.TryGetValue(key, out var item) ? item : null;
        }

        /// <summary>Returns a new array with the current overlay items, in draw order.</summary>
        internal IRenderItem[] SnapshotOverlayItems()
        {
            lock (_lock) return _renderItems.ToArray();
        }

        /// <summary>Returns a new list with the current overlay items, in draw order.</summary>
        internal List<IRenderItem> CopyOverlayItems()
        {
            lock (_lock) return new List<IRenderItem>(_renderItems);
        }

        /// <summary>Copies the current overlay items into <paramref name="destination"/> (cleared first).</summary>
        internal void CopyOverlayItemsInto(List<IRenderItem> destination)
        {
            lock (_lock)
            {
                destination.Clear();
                destination.AddRange(_renderItems);
            }
        }

        /// <summary>True when <paramref name="item"/> is registered in the overlay queue.</summary>
        internal bool ContainsOverlay(IRenderItem item)
        {
            lock (_lock) return _renderItems.Contains(item);
        }

        // ── Deferred release ─────────────────────────────────────────────────────

        /// <summary>Queues a handle for release on the render pass (lock-free).</summary>
        internal void EnqueueRelease(GLResourceHandle handle)
        {
            if (handle == null) return;
            _pendingRelease.Enqueue(handle);
        }

        /// <summary>
        /// Drains the deferred-release queue, calling <paramref name="release"/> for each handle.
        /// Runs without the registry lock (the queue is concurrent) because the callback issues
        /// GL calls.
        /// </summary>
        internal int DrainReleases(Action<GLResourceHandle> release)
        {
            int count = 0;
            while (_pendingRelease.TryDequeue(out var handle))
            {
                release(handle);
                count++;
            }
            return count;
        }

        /// <summary>Discards every queued handle without releasing it.</summary>
        internal void DropReleases()
        {
            while (_pendingRelease.TryDequeue(out _)) { }
        }

        // ── Overlay mutations ───────────────────────────────────────────────────

        /// <summary>
        /// Registers an overlay item under <paramref name="key"/> (upsert). An overlay item is
        /// registered at most once, so re-registering it under a different key renames it in place
        /// instead of creating a second queue entry.
        /// </summary>
        /// <param name="replaced">The item previously occupying the key, or <c>null</c>.</param>
        /// <returns><c>true</c> when the item was already registered under this exact key.</returns>
        internal bool RegisterOverlayItem(string key, AbstractRenderItem item, out AbstractRenderItem replaced)
        {
            replaced = null;
            lock (_lock)
            {
                bool hasOld = _overlayKeys.TryGetValue(key, out var old);
                if (hasOld && ReferenceEquals(old, item)) return true;

                int previousIndex = DetachOverlayItem(item);
                if (hasOld)
                {
                    int index = _renderItems.IndexOf(old);
                    if (index >= 0) _renderItems[index] = item;
                    else _renderItems.Add(item);
                    replaced = old;
                }
                else if (previousIndex >= 0)
                {
                    _renderItems.Insert(previousIndex, item);
                }
                else
                {
                    _renderItems.Add(item);
                }

                _overlayKeys[key] = item;
                return false;
            }
        }

        /// <summary>Appends an overlay item without a key (the historical behaviour).</summary>
        internal void AppendOverlayItem(IRenderItem item)
        {
            lock (_lock) _renderItems.Add(item);
        }

        /// <summary>Removes and returns the overlay item stored under <paramref name="key"/>.</summary>
        internal AbstractRenderItem RemoveOverlayItem(string key)
        {
            lock (_lock)
            {
                if (!_overlayKeys.TryGetValue(key, out var item)) return null;
                _overlayKeys.Remove(key);
                _renderItems.Remove(item);
                return item;
            }
        }

        /// <summary>Removes an overlay item by reference, releasing its key (if any).</summary>
        internal bool RemoveOverlayItem(IRenderItem item)
        {
            lock (_lock)
            {
                if (!_renderItems.Remove(item)) return false;
                RemoveOverlayKeyForItem(item);
                return true;
            }
        }

        /// <summary>Forgets every overlay item, invoking <paramref name="dispose"/> for each of them.</summary>
        internal void ClearOverlayItems(Action<IRenderItem> dispose)
        {
            lock (_lock)
            {
                foreach (var item in _renderItems)
                    dispose(item);
                _renderItems.Clear();
                _overlayKeys.Clear();
            }
        }

        // ── Surface mutations ───────────────────────────────────────────────────

        /// <summary>
        /// Upserts a surface item under <paramref name="key"/> (append when the key is new, replace
        /// in place otherwise). An item is registered at most once, so re-registering it under a
        /// different key is a rename that keeps its slot.
        /// </summary>
        /// <remarks>
        /// Draw-order slot rules, applied in this order:
        /// <list type="bullet">
        /// <item>New key, item not registered yet: appended at the end.</item>
        /// <item>New key, item already registered under another key: the item keeps the slot it
        /// occupied (rename).</item>
        /// <item>Existing key: the item takes the slot of the item it replaces.</item>
        /// <item>Both at once (existing key <i>and</i> the item already registered elsewhere): the
        /// item takes the <b>replaced</b> item's slot and the slot it held under its previous key is
        /// dropped, so it never occupies two slots.</item>
        /// </list>
        /// </remarks>
        /// <param name="handle">Handle to register for <paramref name="item"/>.</param>
        /// <param name="displaced">
        /// Invoked for every registration this upsert displaced (the item's previous key, or the item
        /// previously occupying the key). The handle has already been removed from the pool; the
        /// third argument is <c>true</c> when the displaced item must also be disposed.
        /// </param>
        /// <returns><c>true</c> when the item was already registered under this exact key.</returns>
        internal bool RegisterSurfaceItem(string key, ICxObjRenderItem item, GLResourceHandle handle,
            Action<ICxObjRenderItem, GLResourceHandle, bool> displaced)
        {
            lock (_lock)
            {
                bool hasOld = _keys.TryGetValue(key, out var old);
                if (hasOld && ReferenceEquals(old, item)) return true;

                int previousIndex = DetachSurfaceItem(item, displaced);
                if (hasOld)
                {
                    int index = _items.IndexOf(old);
                    ReleasePoolEntry(old, displaced, disposeItem: true);
                    if (index >= 0) _items[index] = item;
                    else _items.Add(item);
                }
                else if (previousIndex >= 0)
                {
                    _items.Insert(previousIndex, item);
                }
                else
                {
                    _items.Add(item);
                }

                _keys[key] = item;
                _pool[item] = handle;
                return false;
            }
        }

        /// <summary>Forgets every surface item, reporting each through <paramref name="displaced"/>.</summary>
        internal void ClearSurfaceItems(Action<ICxObjRenderItem, GLResourceHandle, bool> displaced)
        {
            lock (_lock)
            {
                foreach (var item in _items)
                    ReleasePoolEntry(item, displaced, disposeItem: true);
                _items.Clear();
                _keys.Clear();
            }
        }

        /// <summary>
        /// Removes the surface item stored under <paramref name="key"/>. The returned handle (if any)
        /// has already been detached from the pool and must be released by the caller.
        /// </summary>
        internal bool RemoveSurfaceItem(string key, out ICxObjRenderItem removed, out GLResourceHandle handle)
        {
            removed = null;
            handle = null;
            lock (_lock)
            {
                if (!_keys.TryGetValue(key, out removed)) return false;
                _keys.Remove(key);
                _items.Remove(removed);
                _pool.TryGetValue(removed, out handle);
                _pool.Remove(removed);
                return true;
            }
        }

        /// <summary>
        /// Removes a surface item by reference (releasing its key). The returned handle (if any) has
        /// already been detached from the pool and must be released by the caller.
        /// </summary>
        internal bool RemoveSurfaceItem(ICxObjRenderItem item, out GLResourceHandle handle)
        {
            handle = null;
            lock (_lock)
            {
                if (!_items.Remove(item)) return false;
                RemoveSurfaceKeyForItem(item);
                _pool.TryGetValue(item, out handle);
                _pool.Remove(item);
                return true;
            }
        }

        /// <summary>Returns the key <paramref name="item"/> is registered under, or <c>null</c>.</summary>
        internal string GetKeyOf(ICxObjRenderItem item)
        {
            if (item == null) return null;
            lock (_lock)
            {
                foreach (var kv in _keys)
                    if (ReferenceEquals(kv.Value, item)) return kv.Key;
                return null;
            }
        }

        /// <summary>Marks the item's handle as needing a GPU rebuild on the next render pass.</summary>
        internal void MarkNeedsUpdate(ICxObjRenderItem item)
        {
            lock (_lock)
                if (_pool.TryGetValue(item, out var handle))
                    handle.NeedsUpdate = true;
        }

        /// <summary>Invokes <paramref name="action"/> for every pool entry (item plus its handle).</summary>
        internal void ForEachPoolEntry(Action<ICxObjRenderItem, GLResourceHandle> action)
        {
            lock (_lock)
                foreach (var kv in _pool)
                    action(kv.Key, kv.Value);
        }

        /// <summary>
        /// Forgets every registered item. Surface handles are reported through
        /// <paramref name="releaseSurface"/> (already detached from the pool; the caller releases them
        /// directly, because disposal runs with the context current) and overlay items through
        /// <paramref name="disposeOverlay"/>.
        /// </summary>
        internal void DisposeAll(Action<ICxObjRenderItem, GLResourceHandle> releaseSurface,
            Action<IRenderItem> disposeOverlay)
        {
            lock (_lock)
            {
                foreach (var item in _items)
                {
                    _pool.TryGetValue(item, out var handle);
                    releaseSurface(item, handle);
                }
                _items.Clear();
                _keys.Clear();
                _pool.Clear();

                _overlayKeys.Clear();
                foreach (var item in _renderItems)
                    disposeOverlay(item);
                _renderItems.Clear();
            }
        }

        // ── Private helpers (caller holds the lock) ──────────────────────────────

        /// <summary>Removes <paramref name="item"/> from the overlay queue and its key; returns its index.</summary>
        private int DetachOverlayItem(AbstractRenderItem item)
        {
            int index = _renderItems.IndexOf(item);
            if (index < 0) return -1;

            _renderItems.RemoveAt(index);
            RemoveOverlayKeyForItem(item);
            return index;
        }

        /// <summary>Removes the key entry pointing at <paramref name="item"/>.</summary>
        private void RemoveOverlayKeyForItem(IRenderItem item)
        {
            string foundKey = null;
            foreach (var kv in _overlayKeys)
                if (ReferenceEquals(kv.Value, item)) { foundKey = kv.Key; break; }
            if (foundKey != null) _overlayKeys.Remove(foundKey);
        }

        /// <summary>Removes the key entry pointing at <paramref name="item"/>.</summary>
        private void RemoveSurfaceKeyForItem(ICxObjRenderItem item)
        {
            string foundKey = null;
            foreach (var kv in _keys)
                if (ReferenceEquals(kv.Value, item)) { foundKey = kv.Key; break; }
            if (foundKey != null) _keys.Remove(foundKey);
        }

        /// <summary>Detaches <paramref name="item"/> from the queue, its key and the pool; returns its index.</summary>
        private int DetachSurfaceItem(ICxObjRenderItem item,
            Action<ICxObjRenderItem, GLResourceHandle, bool> displaced)
        {
            int index = _items.IndexOf(item);
            if (index < 0) return -1;

            _items.RemoveAt(index);
            RemoveSurfaceKeyForItem(item);
            ReleasePoolEntry(item, displaced, disposeItem: false);
            return index;
        }

        /// <summary>
        /// Detaches <paramref name="item"/>'s handle from the pool and reports it. No GL calls are
        /// made; the caller decides when and how to release the handle.
        /// </summary>
        private void ReleasePoolEntry(ICxObjRenderItem item,
            Action<ICxObjRenderItem, GLResourceHandle, bool> displaced, bool disposeItem)
        {
            _pool.TryGetValue(item, out var handle);
            _pool.Remove(item);
            displaced(item, handle, disposeItem);
        }
    }
}
