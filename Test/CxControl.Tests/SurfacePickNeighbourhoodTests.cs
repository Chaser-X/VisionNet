using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pure-geometry tests for <see cref="CxExtension.SearchSurfaceNeighbourhood"/>, the 5×5 grid
    /// search extracted out of the hover-picking path. It takes no GL context and no display
    /// instance, so the picking maths can be pinned without a live render loop.
    /// </summary>
    public class SurfacePickNeighbourhoodTests
    {
        // 5x5 grid: x = col, y = row, z = 0.5 * raw. Raw value -32768 marks a hole.
        private static CxSurface Surface(short[] data, byte[] intensity = null, float[] diff = null)
        {
            var surface = new CxSurface(5, 5, data, intensity, 0f, 0f, 0f, 1f, 1f, 0.5f);
            // The CxSurface constructor does not take Diff — it is a plain auto-property.
            surface.Diff = diff;
            return surface;
        }

        private static short[] FlatData(short value = 0)
        {
            var data = new short[25];
            for (int i = 0; i < data.Length; i++) data[i] = value;
            return data;
        }

        private static CxMatrix4X4 Identity => CxMatrix4X4.Identity();

        /// <summary>
        /// Runs the search and returns the best point / value it settled on.
        /// <paramref name="local"/> defaults to <paramref name="world"/> (identity pose).
        /// </summary>
        private static (CxPoint3D? Point, float? Value, float Dist) Search(
            CxSurface surface, CxPoint3D world, bool useDiff = false,
            CxMatrix4X4 model = null, CxPoint3D? local = null)
        {
            float bestDist = float.MaxValue;
            CxPoint3D? bestPoint = null;
            float? bestValue = null;
            CxExtension.SearchSurfaceNeighbourhood(surface, model ?? Identity, local ?? world, world,
                useDiff, ref bestDist, ref bestPoint, ref bestValue);
            return (bestPoint, bestValue, bestDist);
        }

        [Fact]
        public void FindsTheCellContainingThePickedPoint()
        {
            // Flat surface at z = 0.5 * 7 = 3.5. Pick directly above grid cell (2, 3).
            var surface = Surface(FlatData(7));
            var result = Search(surface, new CxPoint3D(2f, 3f, 3.5f));

            Assert.NotNull(result.Point);
            Assert.Equal(2f, result.Point.Value.X, 4);
            Assert.Equal(3f, result.Point.Value.Y, 4);
            Assert.Equal(3.5f, result.Point.Value.Z, 4);
            Assert.Equal(0f, result.Dist, 4);
        }

        [Fact]
        public void SearchesOnlyTheFiveByFiveWindow()
        {
            // Only cell (0,2) holds data.
            var data = FlatData(CxExtension.NoDataValue);
            data[2 * 5 + 0] = 0;
            var surface = Surface(data);

            // Picking cell (2,2) puts (0,2) exactly two columns away — inside the ±2 window.
            var found = Search(surface, new CxPoint3D(2f, 2f, 0f));
            Assert.NotNull(found.Point);
            Assert.Equal(0f, found.Point.Value.X, 4);
            Assert.Equal(2f, found.Point.Value.Y, 4);

            // Picking cell (4,2) puts it four columns away — outside the window, so nothing is found
            // even though the surface does hold data.
            var missed = Search(surface, new CxPoint3D(4f, 2f, 0f));
            Assert.Null(missed.Point);
        }

        [Fact]
        public void PickedCellOutsideTheGrid_FindsNothing()
        {
            var surface = Surface(FlatData());

            var result = Search(surface, new CxPoint3D(100f, 100f, 0f));

            Assert.Null(result.Point);
            Assert.Equal(float.MaxValue, result.Dist);
        }

        [Fact]
        public void AllNoDataInWindow_FindsNothing()
        {
            var surface = Surface(FlatData(CxExtension.NoDataValue));

            var result = Search(surface, new CxPoint3D(2f, 2f, 0f));

            Assert.Null(result.Point);
        }

        [Fact]
        public void CandidateBeyondTheTolerance_IsRejected()
        {
            // The whole grid is flat at z = 0, but the picked point sits 100 units above it.
            // The tolerance is 5 * (1 + 1 + 0.25) = 11.25 squared units, so every candidate is
            // far too distant to belong to this surface.
            var surface = Surface(FlatData());
            var result = Search(surface, new CxPoint3D(2f, 2f, 100f));

            Assert.Null(result.Point);
        }

        [Fact]
        public void KeepsTheCloserOfTwoCandidates()
        {
            // Two cells at different heights: (2,2) at z=5 and (3,2) at z=0. Pick at z=4.5,
            // x just inside the (2,2) cell so both are in the window. (2,2) is closer.
            var data = FlatData();
            data[2 * 5 + 2] = 10;   // z = 5
            var surface = Surface(data);

            var result = Search(surface, new CxPoint3D(2f, 2f, 4.5f));

            Assert.NotNull(result.Point);
            Assert.Equal(2f, result.Point.Value.X, 4);
            Assert.Equal(5f, result.Point.Value.Z, 4);
        }

        [Fact]
        public void ReportsIntensityByDefault_AndDiffInDiffMode()
        {
            var intensity = new byte[25];
            var diff = new float[25];
            intensity[2 * 5 + 2] = 200;
            diff[2 * 5 + 2] = -12.5f;
            var surface = Surface(FlatData(), intensity, diff);
            var world = new CxPoint3D(2f, 2f, 0f);

            var byIntensity = Search(surface, world);
            var byDiff = Search(surface, world, useDiff: true);

            Assert.NotNull(byIntensity.Value);
            Assert.NotNull(byDiff.Value);
            Assert.Equal(200f, byIntensity.Value.Value);
            Assert.Equal(-12.5f, byDiff.Value.Value);
        }

        [Fact]
        public void MissingValueArray_ReportsNullRatherThanThrowing()
        {
            var surface = Surface(FlatData());   // no intensity and no diff arrays at all

            Assert.Null(Search(surface, new CxPoint3D(2f, 2f, 0f)).Value);
            Assert.Null(Search(surface, new CxPoint3D(2f, 2f, 0f), useDiff: true).Value);
        }

        [Fact]
        public void ModelMatrixMovesTheCandidateIntoWorldSpace()
        {
            // The item is posed 10 units along +X, so the unprojected point is local (2,2,0)
            // but must be reported back in world space at x = 12.
            var surface = Surface(FlatData());
            var model = CxMatrix4X4.Translation(10, 0, 0);

            var result = Search(surface, new CxPoint3D(12f, 2f, 0f), model: model,
                local: new CxPoint3D(2f, 2f, 0f));

            Assert.NotNull(result.Point);
            Assert.Equal(12f, result.Point.Value.X, 4);
            Assert.Equal(0f, result.Dist, 4);
        }

        [Fact]
        public void AnAlreadyBetterCandidateIsNotOverwritten()
        {
            // A mesh/point-cloud item earlier in the frame sets bestDist to 0 (an exact hit);
            // the grid search must then leave that result alone.
            var surface = Surface(FlatData());
            var pinned = new CxPoint3D(99f, 99f, 99f);

            float bestDist = 0f;
            CxPoint3D? bestPoint = pinned;
            float? bestValue = null;
            CxExtension.SearchSurfaceNeighbourhood(surface, Identity,
                new CxPoint3D(2f, 2f, 0f), new CxPoint3D(2f, 2f, 0f), false,
                ref bestDist, ref bestPoint, ref bestValue);

            Assert.Equal(pinned, bestPoint);
            Assert.Equal(0f, bestDist);
        }
    }
}
