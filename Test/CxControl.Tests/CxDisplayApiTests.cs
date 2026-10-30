using System;
using System.Drawing;
using System.Threading;
using VisionNet.DataType;
using Xunit;
using Xunit.Sdk;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Tests for the <see cref="CxDisplay"/> surface/overlay registration API — the flows the M1
    /// refactor rewrote. Nothing here renders: the control is never shown and no GL context is
    /// created, so these assertions cover the registry orchestration only.
    /// </summary>
    public class CxDisplayApiTests
    {
        /// <summary>WinForms controls must be created and used on an STA thread.</summary>
        private static void OnSta(Action action)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) throw new XunitException(error.ToString());
        }

        private static CxSurface NewSurface() =>
            new CxSurface(2, 2, new short[] { 0, 1, 2, 3 }, null, 0f, 0f, 0f, 1f, 1f, 1f);

        private static CxMesh NewMesh(float z)
        {
            var mesh = new CxMesh
            {
                Vertices = new[]
                {
                    new CxPoint3D(0f, 0f, z),
                    new CxPoint3D(1f, 0f, z),
                    new CxPoint3D(1f, 1f, z),
                    new CxPoint3D(0f, 1f, z),
                },
                Indices = new uint[] { 0, 1, 2, 0, 2, 3 },
            };
            return mesh;
        }

        private static CxBox3D[] NewBoxes() =>
            new[] { new CxBox3D(new CxPoint3D(0f, 0f, 0f), new CxSize3D(1f, 1f, 1f)) };

        // ── Surface registration ────────────────────────────────────────────────

        [Fact]
        public void SetMesh_RegistersUnderTheKey()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var mesh = NewMesh(1f);
                    var item = display.SetMesh(mesh, "A", null, null, null, false);

                    Assert.Same(mesh, item.Mesh);
                    Assert.Same(item, display.GetMesh("A"));
                    Assert.Same(item, display.GetSurfaceItem("A"));
                    Assert.Null(display.GetMesh("B"));
                }
            });
        }

        [Fact]
        public void SetMesh_SameKey_ReplacesTheItem()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var first = display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    var second = display.SetMesh(NewMesh(2f), "A", null, null, null, false);

                    Assert.Same(second, display.GetMesh("A"));
                    Assert.NotSame(first, second);
                }
            });
        }

        [Fact]
        public void SetSurface_OnAMeshKey_ReplacesAcrossTypes()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    var surfaceItem = display.SetSurface(NewSurface(), "A", null, null, null, 2000000, false);

                    Assert.Same(surfaceItem, display.GetSurface("A"));
                    Assert.Null(display.GetMesh("A"));
                }
            });
        }

        [Fact]
        public void SetSurfaceItem_OtherKey_RenamesTheRegistration()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var item = display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    display.SetMesh(NewMesh(2f), "B", null, null, null, false);

                    // Re-registering the *same instance* under a new key is a rename, not a duplicate.
                    display.SetSurfaceItem("C", item, false);

                    Assert.Null(display.GetMesh("A"));
                    Assert.NotNull(display.GetMesh("B"));
                    Assert.Same(item, display.GetMesh("C"));
                    Assert.False(item.IsDisposed);
                }
            });
        }

        [Fact]
        public void SetSurfaceItem_SameInstanceSameKey_IsANoOpAndDoesNotDisposeIt()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var item = display.SetMesh(NewMesh(1f), "A", null, null, null, false);

                    display.SetSurfaceItem("A", item, false);

                    // Re-registering the same instance must not release/dispose the caller's item.
                    Assert.Same(item, display.GetMesh("A"));
                    Assert.False(item.IsDisposed);
                }
            });
        }

        [Fact]
        public void RemoveSurfaceItem_ByKey_IsTrueThenFalse()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    display.SetMesh(NewMesh(1f), "A", null, null, null, false);

                    Assert.True(display.RemoveSurfaceItem("A"));
                    Assert.Null(display.GetMesh("A"));
                    Assert.False(display.RemoveSurfaceItem("A"));
                }
            });
        }

        [Fact]
        public void RemoveSurfaceItem_ByItem_ReleasesItsKey()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var item = display.SetMesh(NewMesh(1f), "A", null, null, null, false);

                    display.RemoveSurfaceItem(item);

                    Assert.Null(display.GetMesh("A"));
                    Assert.Null(display.GetSurfaceItem("A"));
                }
            });
        }

        [Fact]
        public void ClearSurfaceItems_EmptiesEveryKey()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    display.SetSurface(NewSurface(), "B", null, null, null, 2000000, false);

                    display.ClearSurfaceItems();

                    Assert.Null(display.GetSurfaceItem("A"));
                    Assert.Null(display.GetSurfaceItem("B"));
                }
            });
        }

        // ── Overlay registration ────────────────────────────────────────────────

        [Fact]
        public void SetOverlayItem_UpsertsAndDisposesTheReplacedItem()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var first = display.SetBox(NewBoxes(), Color.Red, 1f, "bx");
                    var second = display.SetBox(NewBoxes(), Color.Blue, 1f, "bx");

                    Assert.Same(second, display.GetOverlayItem("bx"));
                    Assert.True(first.IsDisposed);
                    Assert.False(second.IsDisposed);
                }
            });
        }

        [Fact]
        public void SetOverlayItem_SameInstanceSameKey_IsANoOp()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var item = display.SetBox(NewBoxes(), Color.Red, 1f, "bx");

                    display.SetOverlayItem("bx", item);

                    Assert.Same(item, display.GetOverlayItem("bx"));
                    Assert.False(item.IsDisposed);
                }
            });
        }

        [Fact]
        public void RemoveOverlayItem_ByKey_IsTrueThenFalse()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    display.SetBox(NewBoxes(), Color.Red, 1f, "bx");

                    Assert.True(display.RemoveOverlayItem("bx"));
                    Assert.Null(display.GetOverlayItem("bx"));
                    Assert.False(display.RemoveOverlayItem("bx"));
                }
            });
        }

        [Fact]
        public void ClearOverlayItems_EmptiesTheOverlayQueue()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    display.SetBox(NewBoxes(), Color.Red, 1f, "bx");
                    display.SetPoint(new[] { new CxPoint3D(0f, 0f, 0f) }, Color.White, 1f, PointShape.Point, "pt");

                    display.ClearOverlayItems();

                    Assert.Null(display.GetOverlayItem("bx"));
                    Assert.Null(display.GetOverlayItem("pt"));
                }
            });
        }

        // ── Display-level mode propagation ──────────────────────────────────────

        [Fact]
        public void SurfaceMode_IsPropagatedToEveryItem()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var a = display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    var b = display.SetMesh(NewMesh(2f), "B", null, null, null, false);

                    display.SurfaceMode = SurfaceMode.Mesh;

                    Assert.Equal(SurfaceMode.Mesh, a.SurfaceMode);
                    Assert.Equal(SurfaceMode.Mesh, b.SurfaceMode);
                }
            });
        }

        [Fact]
        public void SurfaceColorMode_IsPropagatedToEveryItem()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var a = display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    var b = display.SetSurface(NewSurface(), "B", null, null, null, 2000000, false);

                    display.SurfaceColorMode = SurfaceColorMode.Lit;

                    Assert.Equal(SurfaceColorMode.Lit, a.SurfaceColorMode);
                    Assert.Equal(SurfaceColorMode.Lit, b.SurfaceColorMode);
                }
            });
        }

        [Fact]
        public void OpacityAndSolidColor_ArePerItem()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var a = display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    var b = display.SetMesh(NewMesh(2f), "B", null, null, null, false);

                    a.Opacity = 0.25f;
                    a.SolidColor = Color.Lime;

                    Assert.Equal(0.25f, a.Opacity);
                    Assert.Equal(1f, b.Opacity);
                    Assert.Equal(Color.Lime, a.SolidColor);
                    Assert.NotEqual(Color.Lime, b.SolidColor);
                }
            });
        }

        // ── Groups and view helpers ─────────────────────────────────────────────

        [Fact]
        public void Groups_AddAndRemoveMember()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    var item = display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                    var group = display.Groups.Create("g1");

                    display.Groups.Add(group, item);
                    Assert.Same(group, display.Groups.FindGroupOf(item));

                    Assert.True(display.Groups.Remove(group, item));
                    Assert.Null(display.Groups.FindGroupOf(item));
                }
            });
        }

        [Fact]
        public void FitView_WithRegisteredItems_DoesNotThrow()
        {
            OnSta(() =>
            {
                using (var display = new CxDisplay())
                {
                    display.SetMesh(NewMesh(1f), "A", null, null, null, false);

                    display.FitView();
                    display.Redraw();
                }
            });
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            OnSta(() =>
            {
                var display = new CxDisplay();
                display.SetMesh(NewMesh(1f), "A", null, null, null, false);
                display.SetBox(NewBoxes(), Color.Red, 1f, "bx");

                display.Dispose();
                display.Dispose();
            });
        }
    }
}
