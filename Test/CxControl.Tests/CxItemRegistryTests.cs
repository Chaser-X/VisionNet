using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Direct tests for <see cref="CxItemRegistry"/>, the single owner of the surface/overlay
    /// queues, their key maps, the GL handle pool and the deferred-release queue. No GL context
    /// and no <see cref="CxDisplay"/> are involved: every assertion is about the registry's own
    /// semantics, which is what the M1 refactor rewrote.
    /// </summary>
    public class CxItemRegistryTests
    {
        private static CxSurfaceItem NewSurfaceItem(float z = 0f) =>
            new CxSurfaceItem(new CxSurface(2, 2,
                new short[] { 0, 0, 0, 0 }, null, 0f, 0f, z, 1f, 1f, 1f));

        private static CxBox3DItem NewOverlayItem() =>
            new CxBox3DItem(new[]
            {
                new CxBox3D(new CxPoint3D(0f, 0f, 0f), new CxSize3D(1f, 1f, 1f))
            }, Color.Red);

        private static List<(ICxObjRenderItem Item, GLResourceHandle Handle, bool Dispose)> NoDisplaced()
            => new List<(ICxObjRenderItem, GLResourceHandle, bool)>();

        // ── Surface: registration ────────────────────────────────────────────────

        [Fact]
        public void Register_NewKey_AppendsAndRegistersHandle()
        {
            var reg = new CxItemRegistry();
            var item = NewSurfaceItem();
            var handle = new GLResourceHandle();

            bool already = reg.RegisterSurfaceItem("A", item, handle, (i, h, d) => { });

            Assert.False(already);
            Assert.Equal(1, reg.Count);
            Assert.Same(item, reg.GetByKey("A"));

            var items = new List<ICxObjRenderItem>();
            var handles = new List<GLResourceHandle>();
            reg.CopyFrameSnapshot(items, handles);
            Assert.Same(item, items[0]);
            Assert.Same(handle, handles[0]);
        }

        [Fact]
        public void Register_ExistingKeyForAnItemRegisteredElsewhere_TakesTheReplacedSlot()
        {
            var reg = new CxItemRegistry();
            var a = NewSurfaceItem(1f);
            var b = NewSurfaceItem(2f);
            var c = NewSurfaceItem(3f);
            reg.RegisterSurfaceItem("A", a, new GLResourceHandle(), (i, h, d) => { });
            reg.RegisterSurfaceItem("B", b, new GLResourceHandle(), (i, h, d) => { });
            reg.RegisterSurfaceItem("C", c, new GLResourceHandle(), (i, h, d) => { });

            // "B" is occupied by b while a is registered under "A": a takes b's slot and the slot it
            // held under "A" is dropped, so it never occupies two slots.
            reg.RegisterSurfaceItem("B", a, new GLResourceHandle(), (i, h, d) => { });

            var order = reg.SnapshotItems();
            Assert.Equal(2, order.Length);
            Assert.Same(a, order[0]);
            Assert.Same(c, order[1]);
            Assert.Null(reg.GetByKey("A"));
            Assert.Same(a, reg.GetByKey("B"));
        }

        [Fact]
        public void Register_SameKey_ReplacesInPlaceAndReportsDispose()
        {
            var reg = new CxItemRegistry();
            var first = NewSurfaceItem(1f);
            var second = NewSurfaceItem(2f);
            var middle = NewSurfaceItem(3f);
            var firstHandle = new GLResourceHandle();
            var displaced = NoDisplaced();

            reg.RegisterSurfaceItem("A", first, firstHandle, (i, h, d) => displaced.Add((i, h, d)));
            reg.RegisterSurfaceItem("B", middle, new GLResourceHandle(), (i, h, d) => { });
            reg.RegisterSurfaceItem("A", second, new GLResourceHandle(), (i, h, d) => displaced.Add((i, h, d)));

            Assert.Equal(2, reg.Count);
            Assert.Same(second, reg.GetByKey("A"));
            Assert.Single(displaced);
            Assert.Same(first, displaced[0].Item);
            Assert.Same(firstHandle, displaced[0].Handle);
            Assert.True(displaced[0].Dispose);

            // The replacement keeps A's slot, so draw order stays A, B.
            var order = reg.SnapshotItems();
            Assert.Same(second, order[0]);
            Assert.Same(middle, order[1]);
        }

        [Fact]
        public void Register_OtherKey_RenamesAndKeepsSlotWithoutDisposing()
        {
            var reg = new CxItemRegistry();
            var renamed = NewSurfaceItem(1f);
            var other = NewSurfaceItem(2f);
            var renamedHandle = new GLResourceHandle();
            var displaced = NoDisplaced();

            reg.RegisterSurfaceItem("A", renamed, renamedHandle, (i, h, d) => displaced.Add((i, h, d)));
            reg.RegisterSurfaceItem("B", other, new GLResourceHandle(), (i, h, d) => { });
            reg.RegisterSurfaceItem("C", renamed, new GLResourceHandle(), (i, h, d) => displaced.Add((i, h, d)));

            Assert.Equal(2, reg.Count);
            Assert.Null(reg.GetByKey("A"));
            Assert.Same(renamed, reg.GetByKey("C"));
            Assert.Same(other, reg.GetByKey("B"));

            // A rename detaches the old registration (handle released) but must not dispose the item.
            Assert.Single(displaced);
            Assert.Same(renamed, displaced[0].Item);
            Assert.Same(renamedHandle, displaced[0].Handle);
            Assert.False(displaced[0].Dispose);

            var order = reg.SnapshotItems();
            Assert.Same(renamed, order[0]);
            Assert.Same(other, order[1]);
        }

        [Fact]
        public void Register_SameInstanceSameKey_IsNoOp()
        {
            var reg = new CxItemRegistry();
            var item = NewSurfaceItem();
            var displaced = NoDisplaced();

            reg.RegisterSurfaceItem("A", item, new GLResourceHandle(), (i, h, d) => displaced.Add((i, h, d)));
            bool already = reg.RegisterSurfaceItem("A", item, new GLResourceHandle(), (i, h, d) => displaced.Add((i, h, d)));

            Assert.True(already);
            Assert.Equal(1, reg.Count);
            Assert.Empty(displaced);
        }

        // ── Surface: removal ─────────────────────────────────────────────────────

        [Fact]
        public void RemoveByKey_DetachesItemKeyAndHandle()
        {
            var reg = new CxItemRegistry();
            var item = NewSurfaceItem();
            var handle = new GLResourceHandle();
            reg.RegisterSurfaceItem("A", item, handle, (i, h, d) => { });

            Assert.True(reg.RemoveSurfaceItem("A", out var removed, out var removedHandle));
            Assert.Same(item, removed);
            Assert.Same(handle, removedHandle);
            Assert.Equal(0, reg.Count);
            Assert.Null(reg.GetByKey("A"));
            Assert.False(reg.RemoveSurfaceItem("A", out _, out _));
        }

        [Fact]
        public void RemoveByReference_ReleasesItsKey()
        {
            var reg = new CxItemRegistry();
            var item = NewSurfaceItem();
            reg.RegisterSurfaceItem("A", item, new GLResourceHandle(), (i, h, d) => { });

            Assert.True(reg.RemoveSurfaceItem(item, out var handle));
            Assert.NotNull(handle);
            Assert.Null(reg.GetByKey("A"));
            Assert.Equal(0, reg.Count);
            Assert.False(reg.RemoveSurfaceItem(item, out _));
        }

        [Fact]
        public void ClearSurfaceItems_ReportsEveryItemForDisposal()
        {
            var reg = new CxItemRegistry();
            var a = NewSurfaceItem(1f);
            var b = NewSurfaceItem(2f);
            reg.RegisterSurfaceItem("A", a, new GLResourceHandle(), (i, h, d) => { });
            reg.RegisterSurfaceItem("B", b, new GLResourceHandle(), (i, h, d) => { });
            var reported = NoDisplaced();

            reg.ClearSurfaceItems((i, h, d) => reported.Add((i, h, d)));

            Assert.Equal(0, reg.Count);
            Assert.Null(reg.GetByKey("A"));
            Assert.Null(reg.GetByKey("B"));
            Assert.Equal(2, reported.Count);
            Assert.All(reported, r => Assert.True(r.Dispose));
            Assert.All(reported, r => Assert.NotNull(r.Handle));
        }

        // ── Surface: render-pass side ────────────────────────────────────────────

        [Fact]
        public void CopyFrameSnapshot_IsIndexAlignedWithHandles()
        {
            var reg = new CxItemRegistry();
            var a = NewSurfaceItem(1f);
            var b = NewSurfaceItem(2f);
            var ha = new GLResourceHandle();
            var hb = new GLResourceHandle();
            reg.RegisterSurfaceItem("A", a, ha, (i, h, d) => { });
            reg.RegisterSurfaceItem("B", b, hb, (i, h, d) => { });

            var items = new List<ICxObjRenderItem> { NewSurfaceItem() };
            var handles = new List<GLResourceHandle> { new GLResourceHandle() };
            reg.CopyFrameSnapshot(items, handles);

            Assert.Equal(2, items.Count);
            Assert.Equal(2, handles.Count);
            Assert.Same(a, items[0]);
            Assert.Same(ha, handles[0]);
            Assert.Same(b, items[1]);
            Assert.Same(hb, handles[1]);
        }

        [Fact]
        public void ForEachPoolEntry_VisitsEveryItem()
        {
            var reg = new CxItemRegistry();
            reg.RegisterSurfaceItem("A", NewSurfaceItem(1f), new GLResourceHandle(), (i, h, d) => { });
            reg.RegisterSurfaceItem("B", NewSurfaceItem(2f), new GLResourceHandle(), (i, h, d) => { });
            var seen = new List<ICxObjRenderItem>();

            reg.ForEachPoolEntry((item, handle) => seen.Add(item));

            Assert.Equal(2, seen.Count);
        }

        [Fact]
        public void DrainReleases_HandsBackEachHandleExactlyOnce()
        {
            var reg = new CxItemRegistry();
            var first = new GLResourceHandle();
            var second = new GLResourceHandle();
            reg.EnqueueRelease(first);
            reg.EnqueueRelease(second);
            var drained = new List<GLResourceHandle>();

            Assert.Equal(2, reg.DrainReleases(h => drained.Add(h)));
            Assert.Equal(2, drained.Count);
            Assert.Equal(0, reg.DrainReleases(h => drained.Add(h)));
            Assert.Equal(2, drained.Count);
        }

        [Fact]
        public void EnqueueRelease_IgnoresNull()
        {
            var reg = new CxItemRegistry();
            reg.EnqueueRelease(null);
            Assert.Equal(0, reg.DrainReleases(h => { }));
        }

        [Fact]
        public void MarkNeedsUpdate_SetsTheHandleFlag()
        {
            var reg = new CxItemRegistry();
            var item = NewSurfaceItem();
            var handle = new GLResourceHandle { NeedsUpdate = false };
            reg.RegisterSurfaceItem("A", item, handle, (i, h, d) => { });

            reg.MarkNeedsUpdate(item);

            Assert.True(handle.NeedsUpdate);
        }

        [Fact]
        public void GetKeyOf_ResolvesTheRegisteredKey()
        {
            var reg = new CxItemRegistry();
            var item = NewSurfaceItem();
            reg.RegisterSurfaceItem("A", item, new GLResourceHandle(), (i, h, d) => { });

            Assert.Equal("A", reg.GetKeyOf(item));
            Assert.Null(reg.GetKeyOf(NewSurfaceItem()));
            Assert.Null(reg.GetKeyOf(null));
        }

        [Fact]
        public void CopyItemsOfType_FiltersByConcreteType()
        {
            var reg = new CxItemRegistry();
            reg.RegisterSurfaceItem("A", NewSurfaceItem(), new GLResourceHandle(), (i, h, d) => { });
            reg.RegisterSurfaceItem("B", new CxMeshItem(new CxMesh()), new GLResourceHandle(), (i, h, d) => { });

            Assert.Single(reg.CopyItemsOfType<CxSurfaceItem>());
            Assert.Single(reg.CopyItemsOfType<CxMeshItem>());
        }

        // ── Overlay ──────────────────────────────────────────────────────────────

        [Fact]
        public void OverlayRegister_NewKeyAndReplace()
        {
            var reg = new CxItemRegistry();
            var first = NewOverlayItem();
            var second = NewOverlayItem();

            Assert.False(reg.RegisterOverlayItem("bx", first, out var replaced));
            Assert.Null(replaced);
            Assert.Same(first, reg.GetOverlayByKey("bx"));

            Assert.False(reg.RegisterOverlayItem("bx", second, out replaced));
            Assert.Same(first, replaced);
            Assert.Same(second, reg.GetOverlayByKey("bx"));
            Assert.Single(reg.SnapshotOverlayItems());
        }

        [Fact]
        public void OverlayRegister_SameInstanceSameKey_IsNoOp()
        {
            var reg = new CxItemRegistry();
            var item = NewOverlayItem();
            reg.RegisterOverlayItem("bx", item, out _);

            Assert.True(reg.RegisterOverlayItem("bx", item, out var replaced));
            Assert.Null(replaced);
            Assert.Single(reg.SnapshotOverlayItems());
        }

        [Fact]
        public void OverlayRegister_OtherKey_RenamesWithoutDuplicating()
        {
            var reg = new CxItemRegistry();
            var item = NewOverlayItem();
            reg.RegisterOverlayItem("bx", item, out _);

            reg.RegisterOverlayItem("by", item, out var replaced);

            Assert.Null(replaced);
            Assert.Single(reg.SnapshotOverlayItems());
            Assert.Null(reg.GetOverlayByKey("bx"));
            Assert.Same(item, reg.GetOverlayByKey("by"));
        }

        [Fact]
        public void AppendOverlayItem_AddsUnkeyedEntry()
        {
            var reg = new CxItemRegistry();
            var item = NewOverlayItem();

            reg.AppendOverlayItem(item);

            Assert.Single(reg.SnapshotOverlayItems());
            Assert.True(reg.ContainsOverlay(item));
            Assert.Null(reg.GetOverlayByKey("bx"));
        }

        [Fact]
        public void RemoveOverlayItem_ByKeyAndByReference()
        {
            var reg = new CxItemRegistry();
            var item = NewOverlayItem();
            reg.RegisterOverlayItem("bx", item, out _);

            Assert.Same(item, reg.RemoveOverlayItem("bx"));
            Assert.Null(reg.GetOverlayByKey("bx"));
            Assert.Empty(reg.SnapshotOverlayItems());
            Assert.Null(reg.RemoveOverlayItem("bx"));

            reg.RegisterOverlayItem("bx", item, out _);
            Assert.True(reg.RemoveOverlayItem(item));
            Assert.Null(reg.GetOverlayByKey("bx"));
            Assert.False(reg.RemoveOverlayItem(item));
        }

        [Fact]
        public void ClearOverlayItems_DisposesEveryItem()
        {
            var reg = new CxItemRegistry();
            var a = NewOverlayItem();
            var b = NewOverlayItem();
            reg.RegisterOverlayItem("a", a, out _);
            reg.AppendOverlayItem(b);
            var disposed = new List<IRenderItem>();

            reg.ClearOverlayItems(item => disposed.Add(item));

            Assert.Equal(2, disposed.Count);
            Assert.Empty(reg.SnapshotOverlayItems());
            Assert.Null(reg.GetOverlayByKey("a"));
            Assert.False(reg.ContainsOverlay(a));
        }

        [Fact]
        public void CopyOverlayItems_ReturnsAnIndependentList()
        {
            var reg = new CxItemRegistry();
            var item = NewOverlayItem();
            reg.RegisterOverlayItem("a", item, out _);

            var copy = reg.CopyOverlayItems();
            copy.Clear();

            Assert.Single(reg.SnapshotOverlayItems());
        }
    }
}
