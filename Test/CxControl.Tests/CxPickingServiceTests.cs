using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Exercises <see cref="CxPickingService.FindNearest(CxItemRegistry, CxPoint3D, SurfaceColorMode)"/>
    /// over a real <see cref="CxItemRegistry"/>. The GL-dependent entry points (depth readback and
    /// unprojection) need a live context, but the registry-driven search does not — and that is the
    /// part the S3/S4 refactor rewrote.
    /// </summary>
    public class CxPickingServiceTests
    {
        private static CxItemRegistry NewRegistry() => new CxItemRegistry();

        private static CxSurfaceItem SurfaceItem(float zOffset, byte[] intensity = null)
            => new CxSurfaceItem(new CxSurface(3, 3,
                new short[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, intensity,
                0f, 0f, zOffset, 1f, 1f, 1f));

        private static CxMeshItem MeshItem()
        {
            var mesh = new CxMesh();
            mesh.Vertices = new[]
            {
                new CxPoint3D(0, 0, 0), new CxPoint3D(1, 0, 0), new CxPoint3D(0, 1, 0),
            };
            mesh.Indices = new uint[] { 0, 1, 2 };
            return new CxMeshItem(mesh);
        }

        private static CxItemRegistry RegistryWith(params (string Key, ICxObjRenderItem Item)[] items)
        {
            var registry = new CxItemRegistry();
            foreach (var entry in items)
                registry.RegisterSurfaceItem(entry.Key, entry.Item, new GLResourceHandle(),
                    (i, h, d) => { });
            return registry;
        }

        [Fact]
        public void EmptyRegistry_FindsNothing()
        {
            var service = new CxPickingService();

            var result = service.FindNearest(NewRegistry(), new CxPoint3D(0f, 0f, 0f),
                SurfaceColorMode.Color);

            Assert.Null(result.Location);
            Assert.Null(result.Intensity);
        }

        [Fact]
        public void SingleSurface_FindsItsGridPoint()
        {
            using (var item = SurfaceItem(zOffset: 5f))
            {
                var service = new CxPickingService();
                var registry = RegistryWith(("s", item));

                var result = service.FindNearest(registry, new CxPoint3D(1f, 2f, 5f),
                    SurfaceColorMode.Color);

                Assert.NotNull(result.Location);
                Assert.Equal(1f, result.Location.Value.X, 4);
                Assert.Equal(2f, result.Location.Value.Y, 4);
                Assert.Equal(5f, result.Location.Value.Z, 4);
            }
        }

        [Fact]
        public void GridlessItemWins_OverAGridCandidate()
        {
            // The mesh answers with the query point itself (distance 0), which outranks the
            // surface's grid candidate regardless of registration order.
            using (var surface = SurfaceItem(zOffset: 5f))
            using (var mesh = MeshItem())
            {
                var service = new CxPickingService();
                var world = new CxPoint3D(1f, 2f, 5f);

                var withMeshFirst = service.FindNearest(
                    RegistryWith(("m", mesh), ("s", surface)), world, SurfaceColorMode.Color);
                var withSurfaceFirst = service.FindNearest(
                    RegistryWith(("s", surface), ("m", mesh)), world, SurfaceColorMode.Color);

                Assert.NotNull(withMeshFirst.Location);
                Assert.NotNull(withSurfaceFirst.Location);
                Assert.Equal(world, withMeshFirst.Location.Value);
                Assert.Equal(world, withSurfaceFirst.Location.Value);
            }
        }

        [Fact]
        public void DiffMode_ReportsDiffValuesInsteadOfIntensity()
        {
            var surface = new CxSurface(3, 3,
                new short[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 7, 7, 7, 7, 7, 7, 7, 7, 7 },
                0f, 0f, 0f, 1f, 1f, 1f);
            surface.Diff = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            using (var item = new CxSurfaceItem(surface))
            {
                var service = new CxPickingService();
                var registry = RegistryWith(("s", item));
                var world = new CxPoint3D(1f, 2f, 0f);

                var colorMode = service.FindNearest(registry, world, SurfaceColorMode.Color);
                var diffMode = service.FindNearest(registry, world, SurfaceColorMode.Diff);

                // The exact-hit cell is (1, 2), i.e. index row 2 * width 3 + col 1 = 7.
                Assert.NotNull(colorMode.Location);
                Assert.Equal(1f, colorMode.Location.Value.X, 4);
                Assert.Equal(2f, colorMode.Location.Value.Y, 4);

                Assert.NotNull(colorMode.Intensity);
                Assert.NotNull(diffMode.Intensity);
                Assert.Equal(7f, colorMode.Intensity.Value);    // intensity[7] (all 7)
                Assert.Equal(8f, diffMode.Intensity.Value);     // diff[7] == 8
            }
        }

        [Fact]
        public void DisposedItem_IsSkipped()
        {
            var item = SurfaceItem(zOffset: 5f);
            var registry = RegistryWith(("s", item));
            item.Dispose();

            var service = new CxPickingService();
            var result = service.FindNearest(registry, new CxPoint3D(1f, 2f, 5f),
                SurfaceColorMode.Color);

            Assert.Null(result.Location);
        }

        [Fact]
        public void RepeatedQueries_ReuseTheScratchBufferWithoutLeakingItems()
        {
            // The service clears its snapshot on both exit paths; a second query must see the same
            // answer rather than accumulating or dropping items.
            using (var item = SurfaceItem(zOffset: 5f))
            {
                var service = new CxPickingService();
                var registry = RegistryWith(("s", item));
                var world = new CxPoint3D(1f, 2f, 5f);

                for (int i = 0; i < 5; i++)
                {
                    var result = service.FindNearest(registry, world, SurfaceColorMode.Color);
                    Assert.NotNull(result.Location);
                    Assert.Equal(1f, result.Location.Value.X, 4);
                }
            }
        }
    }
}
