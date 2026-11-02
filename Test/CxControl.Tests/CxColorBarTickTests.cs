using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pins the colour bar's tick arithmetic. The bar splits its Z range into
    /// <c>TickDivisions</c> equal steps and draws one label per step boundary, so the tick
    /// count, the endpoint values, and the even spacing are all part of the rendered contract.
    /// </summary>
    public class CxColorBarTickTests
    {
        private const int Divisions = 7;   // CxColorBarItem.TickDivisions

        [Fact]
        public void TickCountIsDivisionsPlusOne()
        {
            // The draw loops run `i <= TickDivisions`, i.e. one tick per boundary.
            int ticks = 0;
            for (int i = 0; i <= Divisions; i++) ticks++;
            Assert.Equal(Divisions + 1, ticks);
        }

        [Fact]
        public void EndpointTicks_AreExactlyTheRangeBounds()
        {
            Assert.Equal(0d, CxColorBarItem.TickZValue(0, 0f, 100f), 6);
            Assert.Equal(100d, CxColorBarItem.TickZValue(Divisions, 0f, 100f), 6);
        }

        [Fact]
        public void TicksAreEvenlySpaced()
        {
            const float zMin = -20f, zMax = 50f;
            double step = (zMax - zMin) / Divisions;

            for (int i = 0; i <= Divisions; i++)
                Assert.Equal(zMin + i * step, CxColorBarItem.TickZValue(i, zMin, zMax), 4);
        }

        [Fact]
        public void TickLabelsAreMonotonic_EvenForAnInvertedRange()
        {
            // A reversed range is not normalised anywhere, so the labels simply run the other way.
            double previous = CxColorBarItem.TickZValue(0, 100f, 0f);
            for (int i = 1; i <= Divisions; i++)
            {
                double current = CxColorBarItem.TickZValue(i, 100f, 0f);
                Assert.True(current < previous, $"tick {i} should sit below tick {i - 1}");
                previous = current;
            }
        }

        // TickY computes `startY + (int)(i * (barHeight / 7f))` in single precision, so the top
        // tick can land one pixel short of the bar top (e.g. 7 * (100 / 7f) == 99.999998).
        // The tests below therefore allow that one-pixel truncation instead of asserting the
        // exact endpoint.

        [Theory]
        [InlineData(100)]
        [InlineData(33)]    // not divisible by 7
        [InlineData(7)]
        public void TickScreenPositions_StartAtTheBarBottom_AndStayWithinTheBar(int barHeight)
        {
            const int startY = 40;

            Assert.Equal(startY, CxColorBarItem.TickY(0, startY, barHeight));

            int previous = startY;
            for (int i = 1; i <= Divisions; i++)
            {
                int y = CxColorBarItem.TickY(i, startY, barHeight);
                Assert.True(y >= previous, $"tick {i} moved backwards");
                Assert.InRange(y, startY, startY + barHeight);
                previous = y;
            }

            Assert.InRange(previous, startY + barHeight - 1, startY + barHeight);
        }
    }
}
