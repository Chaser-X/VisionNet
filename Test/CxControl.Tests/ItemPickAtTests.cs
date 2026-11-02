using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Covers the polymorphic pick dispatch: each built-in surface item answers a world-space pick
    /// query through its own <c>PickAt</c> override, so <see cref="CxDisplay"/> no longer branches
    /// on the concrete item type. These tests pin what each item contributes.
    /// </summary>
    public class ItemPickAtTests
    {
        private static CxSurface Surface5x5(short fill = 0)
        {
            var data = new short[25];
            for (int i = 0; i < data.Length; i++) data[i] = fill;
            return new CxSurface(5, 5, data, null, 0f, 0f, 0f, 1f, 1f, 0.5f);
        }

        private static CxPointCloud Cloud5x5()
        {
            var data = new short[25 * 3];
            return new CxPointCloud(5, 5, data, null, 0f, 0f, 0f, 1f, 1f, 1f);
        }

        private static CxMesh Mesh()
        {
            var mesh = new CxMesh();
            mesh.Vertices = new[]
            {
                new CxPoint3D(0, 0, 0), new CxPoint3D(1, 0, 0), new CxPoint3D(0, 1, 0),
            };
            mesh.Indices = new uint[] { 0, 1, 2 };
            return mesh;
        }

        /// <summary>Runs <paramref name="item"/>'s pick contribution and reports what it produced.</summary>
        private static (CxPoint3D? Point, float? Value, float Dist) Pick(
            AbstractCxObjRenderItem item, CxPoint3D world, bool useDiff = false,
            float startDist = float.MaxValue)
        {
            float bestDist = startDist;
            CxPoint3D? bestPoint = null;
            float? bestValue = null;
            item.PickAt(world, useDiff, ref bestDist, ref bestPoint, ref bestValue);
            return (bestPoint, bestValue, bestDist);
        }

        [Fact]
        public void SurfaceItem_SearchesItsGrid()
        {
            // Flat surface at z = 0.5 * 8 = 4; pick straight above cell (1, 3).
            using (var item = new CxSurfaceItem(Surface5x5(8)))
            {
                var result = Pick(item, new CxPoint3D(1f, 3f, 4f));

                Assert.NotNull(result.Point);
                Assert.Equal(1f, result.Point.Value.X, 4);
                Assert.Equal(3f, result.Point.Value.Y, 4);
                Assert.Equal(4f, result.Point.Value.Z, 4);
                Assert.Equal(0f, result.Dist, 4);
            }
        }

        [Fact]
        public void SurfaceItem_UndoesThePoseBeforeIndexingTheGrid()
        {
            // Posed 10 units along +X, so the world query at x = 11 corresponds to grid cell (1, 3).
            using (var item = new CxSurfaceItem(Surface5x5(8)))
            {
                item.ModelMatrix = CxMatrix4X4.Translation(10, 0, 0);

                var result = Pick(item, new CxPoint3D(11f, 3f, 4f));

                Assert.NotNull(result.Point);
                // Reported back in world space, i.e. the posed position, not the local cell.
                Assert.Equal(11f, result.Point.Value.X, 4);
                Assert.Equal(0f, result.Dist, 4);
            }
        }

        [Fact]
        public void MeshItem_AnswersWithTheQueryPointItself()
        {
            using (var item = new CxMeshItem(Mesh()))
            {
                var world = new CxPoint3D(4f, 5f, 6f);

                var result = Pick(item, world);

                Assert.NotNull(result.Point);
                Assert.Equal(world, result.Point.Value);
                Assert.Equal(0f, result.Dist);
                Assert.Null(result.Value);
            }
        }

        [Fact]
        public void PointCloudItem_AnswersWithTheQueryPointItself()
        {
            using (var item = new CxPointCloudItem(Cloud5x5()))
            {
                var world = new CxPoint3D(4f, 5f, 6f);

                var result = Pick(item, world);

                Assert.NotNull(result.Point);
                Assert.Equal(world, result.Point.Value);
                Assert.Equal(0f, result.Dist);
                Assert.Null(result.Value);
            }
        }

        [Fact]
        public void GridlessItems_DoNotOverwriteAnExactHitFromAnEarlierItem()
        {
            // bestDist == 0 means an earlier item already produced an exact hit; a mesh must
            // leave it alone rather than replacing the point with its own query coordinate.
            using (var item = new CxMeshItem(Mesh()))
            {
                var pinned = new CxPoint3D(9f, 9f, 9f);

                var result = Pick(item, new CxPoint3D(1f, 1f, 1f), startDist: 0f);

                Assert.Equal(0f, result.Dist);
                Assert.Null(result.Point);
            }
        }

        [Fact]
        public void DisposedSurface_ContributesNothing()
        {
            var item = new CxSurfaceItem(Surface5x5(8));
            item.Dispose();

            var result = Pick(item, new CxPoint3D(1f, 3f, 4f));

            Assert.Null(result.Point);
            Assert.Equal(float.MaxValue, result.Dist);
        }

        [Fact]
        public void EveryBuiltInItem_ReportsTheSharedSurfaceProgram()
        {
            // CxDisplay dispatches on this flag instead of comparing shader source text, so it
            // must stay true for all three built-in items.
            using (var surface = new CxSurfaceItem(Surface5x5()))
            using (var cloud = new CxPointCloudItem(Cloud5x5()))
            using (var mesh = new CxMeshItem(Mesh()))
            {
                Assert.True(surface.UsesSharedSurfaceProgram);
                Assert.True(cloud.UsesSharedSurfaceProgram);
                Assert.True(mesh.UsesSharedSurfaceProgram);
            }
        }
    }
}
