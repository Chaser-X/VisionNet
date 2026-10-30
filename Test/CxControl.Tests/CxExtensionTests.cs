using System;
using System.Collections.Generic;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pure-CPU tests for <see cref="CxExtension"/>: the bounding-box overloads that scan the
    /// compact source arrays must agree with the older "materialise every point" path, and the
    /// colour mapping must stay on its documented endpoints.
    /// </summary>
    public class CxExtensionTests
    {
        private static CxSurface Surface2x2(short[] data) =>
            new CxSurface(2, 2, data, null, 10f, 20f, 0f, 1f, 2f, 0.5f);

        [Fact]
        public void SurfaceBoundingBox_MatchesToPointsPath()
        {
            var surface = Surface2x2(new short[] { 0, 10, 20, 30 });

            var fast = CxExtension.CalculateBoundingBox(surface);
            var slow = CxExtension.CalculateBoundingBox(surface.ToPoints());

            Assert.NotNull(fast);
            Assert.NotNull(slow);
            Assert.Equal((double)slow.Value.Center.X, fast.Value.Center.X, 4);
            Assert.Equal((double)slow.Value.Center.Y, fast.Value.Center.Y, 4);
            Assert.Equal((double)slow.Value.Center.Z, fast.Value.Center.Z, 4);
            Assert.Equal((double)slow.Value.Size.Width, fast.Value.Size.Width, 4);
            Assert.Equal((double)slow.Value.Size.Height, fast.Value.Size.Height, 4);
            Assert.Equal((double)slow.Value.Size.Depth, fast.Value.Size.Depth, 4);
        }

        [Fact]
        public void SurfaceBoundingBox_SkipsInvalidPoints()
        {
            // raw -32768 marks a hole; that point must not contribute on any axis.
            var surface = Surface2x2(new short[] { 0, 10, 20, -32768 });

            var fast = CxExtension.CalculateBoundingBox(surface);
            var slow = CxExtension.CalculateBoundingBox(surface.ToPoints());

            Assert.Equal((double)slow.Value.Size.Width, fast.Value.Size.Width, 4);
            Assert.Equal((double)slow.Value.Size.Height, fast.Value.Size.Height, 4);
            Assert.Equal((double)slow.Value.Size.Depth, fast.Value.Size.Depth, 4);
            // The hole is the corner at col 1 / row 1, so X spans only col 0 -> col 1 minus that
            // corner, and Y only row 0 -> row 1 minus it (offsets/scales: x +1/col, y +2/row).
            Assert.Equal(1f, fast.Value.Size.Width, 4);
            Assert.Equal(2f, fast.Value.Size.Height, 4);
        }

        [Fact]
        public void SurfaceBoundingBox_AllInvalid_ReturnsNull()
        {
            var surface = Surface2x2(new short[] { -32768, -32768, -32768, -32768 });
            Assert.Null(CxExtension.CalculateBoundingBox(surface));
        }

        [Fact]
        public void SurfaceBoundingBox_RejectsEmptyInput()
        {
            Assert.Null(CxExtension.CalculateBoundingBox((CxSurface)null));
            Assert.Null(CxExtension.CalculateBoundingBox(new CxSurface(0, 0)));
        }

        [Fact]
        public void PointCloudBoundingBox_MatchesToPointsPath()
        {
            // Packed triples: two points, the second one a hole.
            var cloud = new CxPointCloud(1, 2,
                new short[] { 0, 0, 0, 5, -32768, 5 }, null, 0f, 0f, 0f, 1f, 1f, 1f);

            var fast = CxExtension.CalculateBoundingBox(cloud);
            var slow = CxExtension.CalculateBoundingBox(cloud.ToPoints());

            Assert.NotNull(fast);
            Assert.NotNull(slow);
            Assert.Equal((double)slow.Value.Center.X, fast.Value.Center.X, 4);
            Assert.Equal((double)slow.Value.Center.Y, fast.Value.Center.Y, 4);
            Assert.Equal((double)slow.Value.Center.Z, fast.Value.Center.Z, 4);
            Assert.Equal((double)slow.Value.Size.Width, fast.Value.Size.Width, 4);
            Assert.Equal((double)slow.Value.Size.Depth, fast.Value.Size.Depth, 4);
        }

        [Fact]
        public void PointCloudBoundingBox_RejectsEmptyInput()
        {
            Assert.Null(CxExtension.CalculateBoundingBox((CxPointCloud)null));
            Assert.Null(CxExtension.CalculateBoundingBox(new CxPointCloud(0, 0)));
        }

        [Fact]
        public void ComputeWorldZRange_AppliesTheModelMatrix()
        {
            var box = new CxBox3D(
                new CxPoint3D(0f, 0f, 0f),
                new CxSize3D(2f, 2f, 4f));

            CxExtension.ComputeWorldZRange(box, CxMatrix4X4.Translation(0f, 0f, 10f),
                out float zMin, out float zMax);

            // Local Z spans -2..2; the +10 translation moves the world range to 8..12.
            Assert.Equal(8f, zMin, 4);
            Assert.Equal(12f, zMax, 4);
        }

        [Fact]
        public void ComputeWorldZRange_NullBox_YieldsASmallCentredSpan()
        {
            // Documented behaviour: never return a collapsed range, so colour mapping cannot
            // divide by zero.
            CxExtension.ComputeWorldZRange(null, CxMatrix4X4.Identity(), out float zMin, out float zMax);

            Assert.Equal(-0.5f, zMin, 4);
            Assert.Equal(0.5f, zMax, 4);
        }

        [Fact]
        public void GetColorByHeight_HitsTheDocumentedEndpoints()
        {
            var low = CxExtension.GetColorByHeight(0.0, 0.0, 100.0);
            var high = CxExtension.GetColorByHeight(100.0, 0.0, 100.0);

            Assert.Equal(0.0, low.r, 4);
            Assert.Equal(0.0, low.g, 4);
            Assert.Equal(0.5, low.b, 4);

            Assert.Equal(1.0, high.r, 4);
            Assert.Equal(1.0, high.g, 4);
            Assert.Equal(1.0, high.b, 4);
        }

        [Fact]
        public void CalculateBoundingBox_EnumerableOverload_IgnoresInfinity()
        {
            var points = new List<CxPoint3D>
            {
                new CxPoint3D(0f, 0f, 0f),
                new CxPoint3D(float.NegativeInfinity, 1f, 1f),
                new CxPoint3D(2f, 2f, 2f),
            };

            var box = CxExtension.CalculateBoundingBox(points);

            Assert.NotNull(box);
            Assert.Equal(1f, box.Value.Center.X, 4);
            Assert.Equal(2f, box.Value.Size.Width, 4);
        }
    }
}
