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
    /// <para>Operations that must be atomic as a group (register/replace, detach, dispose) wrap
    /// themselves in <see cref="Sync(Action)"/> and use the <c>*InLock</c> accessors inside it.
    /// Everything else (the typed queries and mutations) takes the lock itself, so a caller cannot
    /// forget it or get the order wrong.</para>
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

        // ── Critical section ─────────────────────────────────────────────────────

        /// <summary>Runs <paramref name="action"/> while holding the registry lock.</summary>
        internal void Sync(Action action)
        {
            lock (_lock) action();
        }

        /// <summary>Runs <paramref name="func"/> while holding the registry lock.</summary>
        internal T Sync<T>(Func<T> func)
        {
            lock (_lock) return func();
        }

        // ── Lock-held accessors (only valid inside Sync) ──────────────────────────

        /// <summary>Surface items in draw order. Caller must hold the lock via <see cref="Sync(Action)"/>.</summary>
        internal List<ICxObjRenderItem> ItemsInLock => _items;

        /// <summary>Surface key → item. Caller must hold the lock via <see cref="Sync(Action)"/>.</summary>
        internal Dictionary<string, ICxObjRenderItem> KeysInLock => _keys;

        /// <summary>Overlay items in draw order. Caller must hold the lock via <see cref="Sync(Action)"/>.</summary>
        internal List<IRenderItem> RenderItemsInLock => _renderItems;

        /// <summary>Overlay key → item. Caller must hold the lock via <see cref="Sync(Action)"/>.</summary>
        internal Dictionary<string, AbstractRenderItem> OverlayKeysInLock => _overlayKeys;

        /// <summary>Surface item → GL handle. Caller must hold the lock via <see cref="Sync(Action)"/>.</summary>
        internal Dictionary<ICxObjRenderItem, GLResourceHandle> PoolInLock => _pool;

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

        /// <summary>Returns the GL handle registered for <paramref name="item"/>.</summary>
        internal bool TryGetHandle(ICxObjRenderItem item, out GLResourceHandle handle)
        {
            lock (_lock) return _pool.TryGetValue(item, out handle);
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

        /// <summary>Returns a new list with the surface keys currently registered.</summary>
        internal List<string> CopyKeys()
        {
            lock (_lock) return new List<string>(_keys.Keys);
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

        // ── Surface mutations ────────────────────────────────────────────────────

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
    }
}
