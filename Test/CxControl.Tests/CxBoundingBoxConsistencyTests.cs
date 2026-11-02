using System.Collections.Generic;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Cross-path consistency for the three <see cref="CxExtension.CalculateBoundingBox"/> overloads.
    /// The structured <see cref="CxSurface"/> / <see cref="CxPointCloud"/> scans are hand-written
    /// duplicates of the <see cref="CxPoint3D"/> scan, so they must agree point-for-point — including
    /// how they treat the <c>-32768</c> no-data marker. These tests pin that agreement before the
    /// shared finalisation is factored out into a single helper.
    /// </summary>
    public class CxBoundingBoxConsistencyTests
    {
        // 2x2 surface: x = 10 + col, y = 20 + 2*row, z = 0 + 0.5*raw.
        private static CxSurface Surface(short[] data) =>
            new CxSurface(2, 2, data, null, 10f, 20f, 0f, 1f, 2f, 0.5f);

        // The same four points expressed as raw point-cloud triples (offset 0, scale 1).
        private static CxPointCloud Cloud(short[] data) =>
            new CxPointCloud(2, 2, data, null, 0f, 0f, 0f, 1f, 1f, 1f);

        private static readonly short[] SurfaceFull = { 0, 10, 20, 30 };
        private static readonly short[] CloudFull =
            { 10, 20, 0, 11, 20, 5, 10, 22, 10, 11, 22, 15 };

        [Fact]
        public void SurfaceAndPointCloudAgree_OnFullyPopulatedData()
        {
            var fromSurface = CxExtension.CalculateBoundingBox(Surface(SurfaceFull));
            var fromCloud = CxExtension.CalculateBoundingBox(Cloud(CloudFull));

            Assert.NotNull(fromSurface);
            Assert.NotNull(fromCloud);
            AssertSameBox(fromSurface.Value, fromCloud.Value);
        }

        [Fact]
        public void AllThreeOverloadsAgree_OnTheSamePoints()
        {
            var points = new List<CxPoint3D>
            {
                new CxPoint3D(10, 20, 0), new CxPoint3D(11, 20, 5),
                new CxPoint3D(10, 22, 10), new CxPoint3D(11, 22, 15),
            };

            var fromPoints = CxExtension.CalculateBoundingBox(points);
            var fromSurface = CxExtension.CalculateBoundingBox(Surface(SurfaceFull));
            var fromCloud = CxExtension.CalculateBoundingBox(Cloud(CloudFull));

            Assert.NotNull(fromPoints);
            AssertSameBox(fromPoints.Value, fromSurface.Value);
            AssertSameBox(fromPoints.Value, fromCloud.Value);
        }

        [Fact]
        public void SurfaceAndPointCloudAgree_WhenAWholeRowIsNoData()
        {
            // Row 1 is a hole in both representations.
            var fromSurface = CxExtension.CalculateBoundingBox(Surface(new short[] { 0, 10, -32768, -32768 }));
            var fromCloud = CxExtension.CalculateBoundingBox(
                Cloud(new short[] { 10, 20, 0, 11, 20, 5, -32768, -32768, -32768, -32768, -32768, -32768 }));

            Assert.NotNull(fromSurface);
            Assert.NotNull(fromCloud);
            AssertSameBox(fromSurface.Value, fromCloud.Value);
        }

        [Fact]
        public void SurfaceAndPointCloudAgree_OnAllNoData()
        {
            var fromSurface = CxExtension.CalculateBoundingBox(
                Surface(new short[] { -32768, -32768, -32768, -32768 }));
            var fromCloud = CxExtension.CalculateBoundingBox(
                Cloud(new short[] { -32768, -32768, -32768, -32768, -32768, -32768,
                                    -32768, -32768, -32768, -32768, -32768, -32768 }));

            Assert.Null(fromSurface);
            Assert.Null(fromCloud);
        }

        [Fact]
        public void PointCloudHoleIsPerAxis_NotPerPoint()
        {
            // Only the Z channel of the second point is a hole. The point-cloud scan rejects the
            // whole triple (any channel equal to -32768), so the box collapses onto the one point
            // that survives — it does not contribute the other two channels of the rejected triple.
            var fromCloud = CxExtension.CalculateBoundingBox(
                Cloud(new short[] { 10, 20, 0,
                                    11, 20, -32768,
                                    -32768, -32768, -32768,
                                    -32768, -32768, -32768 }));

            Assert.NotNull(fromCloud);
            Assert.Equal(10f, fromCloud.Value.Center.X, 4);
            Assert.Equal(20f, fromCloud.Value.Center.Y, 4);
            Assert.Equal(0f, fromCloud.Value.Size.Width, 4);
            Assert.Equal(0f, fromCloud.Value.Size.Height, 4);
            Assert.Equal(0f, fromCloud.Value.Size.Depth, 4);
        }

        private static void AssertSameBox(CxBox3D expected, CxBox3D actual)
        {
            Assert.Equal((double)expected.Center.X, actual.Center.X, 4);
            Assert.Equal((double)expected.Center.Y, actual.Center.Y, 4);
            Assert.Equal((double)expected.Center.Z, actual.Center.Z, 4);
            Assert.Equal((double)expected.Size.Width, actual.Size.Width, 4);
            Assert.Equal((double)expected.Size.Height, actual.Size.Height, 4);
            Assert.Equal((double)expected.Size.Depth, actual.Size.Depth, 4);
        }
    }
}
