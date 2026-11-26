using System;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Tests
{
    /// <summary>
    /// Regression tests for the numerical stability of <c>Length</c> and <c>Normalize</c> on the four
    /// vector types.
    /// <para>
    /// Both members used to square the raw components (<c>X * X + Y * Y + …</c>) and, for
    /// <c>Normalize</c>, divide by the resulting length. That intermediate overflows for large
    /// magnitudes and underflows for small ones even when the true length is representable, so
    /// valid finite non-zero vectors lost their length and their unit direction. The fixed
    /// implementations scale by the largest absolute component first.
    /// </para>
    /// <para>
    /// Every expected value below is derived analytically (Pythagorean triples, component ratios) or
    /// computed in <see cref="double"/> from the components — never by calling the code under test.
    /// </para>
    /// </summary>
    [Trait("Category", "NumericalAudit")]
    public class DataTypeVectorNumericalTests
    {
        /// <summary>Relative tolerance for single-precision results.</summary>
        private const float FloatRelTol = 1e-6f;

        /// <summary>Relative tolerance for double-precision results.</summary>
        private const double DoubleRelTol = 1e-12;

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static void AssertFinite(float value, string what) =>
            Assert.False(float.IsNaN(value) || float.IsInfinity(value), $"{what} is not finite: {value:G9}");

        private static void AssertFinite(double value, string what) =>
            Assert.False(double.IsNaN(value) || double.IsInfinity(value), $"{what} is not finite: {value:G17}");

        private static void AssertRelative(float expected, float actual, float relTol, string what)
        {
            AssertFinite(actual, what);
            Assert.True(Math.Abs(expected - actual) <= Math.Abs(expected) * relTol,
                $"{what}: expected {expected:G9}, got {actual:G9} (relative tolerance {relTol:G3})");
        }

        private static void AssertRelative(double expected, double actual, double relTol, string what)
        {
            AssertFinite(actual, what);
            Assert.True(Math.Abs(expected - actual) <= Math.Abs(expected) * relTol,
                $"{what}: expected {expected:G17}, got {actual:G17} (relative tolerance {relTol:G3})");
        }

        private static void AssertNearZero(float actual, float tol, string what)
        {
            AssertFinite(actual, what);
            Assert.True(Math.Abs(actual) <= tol, $"{what}: expected ~0, got {actual:G9}");
        }

        private static void AssertNearZero(double actual, double tol, string what)
        {
            AssertFinite(actual, what);
            Assert.True(Math.Abs(actual) <= tol, $"{what}: expected ~0, got {actual:G17}");
        }

        /// <summary>
        /// Recomputes the magnitude in <see cref="double"/> from the returned components, so the
        /// unit-length claim never depends on the production <c>Length</c> implementation.
        /// </summary>
        private static void AssertUnitLength(float x, float y, string what)
        {
            AssertFinite(x, what + ".X");
            AssertFinite(y, what + ".Y");
            double norm = Math.Sqrt((double)x * x + (double)y * y);
            Assert.True(Math.Abs(norm - 1.0) <= FloatRelTol, $"{what}: |v| = {norm:G17}, expected 1");
        }

        private static void AssertUnitLength(float x, float y, float z, string what)
        {
            AssertFinite(x, what + ".X");
            AssertFinite(y, what + ".Y");
            AssertFinite(z, what + ".Z");
            double norm = Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
            Assert.True(Math.Abs(norm - 1.0) <= FloatRelTol, $"{what}: |v| = {norm:G17}, expected 1");
        }

        private static void AssertUnitLength(double x, double y, string what)
        {
            AssertFinite(x, what + ".X");
            AssertFinite(y, what + ".Y");
            double norm = Math.Sqrt(x * x + y * y);
            Assert.True(Math.Abs(norm - 1.0) <= DoubleRelTol, $"{what}: |v| = {norm:G17}, expected 1");
        }

        private static void AssertUnitLength(double x, double y, double z, string what)
        {
            AssertFinite(x, what + ".X");
            AssertFinite(y, what + ".Y");
            AssertFinite(z, what + ".Z");
            double norm = Math.Sqrt(x * x + y * y + z * z);
            Assert.True(Math.Abs(norm - 1.0) <= DoubleRelTol, $"{what}: |v| = {norm:G17}, expected 1");
        }

        // ── Ordinary magnitudes ─────────────────────────────────────────────────

        /// <summary>Pythagorean triples, so the expected length is an exact integer.</summary>
        [Theory]
        [InlineData(3f, 4f, 5f)]
        [InlineData(6f, 8f, 10f)]
        [InlineData(5f, 12f, 13f)]
        [InlineData(-8f, 15f, 17f)]
        [InlineData(1f, 0f, 1f)]
        [InlineData(0f, 7f, 7f)]
        public void Vector2D_OrdinaryLength_MatchesAnalyticMagnitude(float x, float y, float expected)
        {
            AssertRelative(expected, new CxVector2D(x, y).Length, FloatRelTol, "Length");
        }

        /// <summary>Pythagorean quadruples, so the expected length is an exact integer.</summary>
        [Theory]
        [InlineData(2f, -3f, 6f, 7f)]
        [InlineData(-2f, 3f, -6f, 7f)]
        [InlineData(1f, 2f, 2f, 3f)]
        [InlineData(3f, 4f, 12f, 13f)]
        [InlineData(0f, 0f, 5f, 5f)]
        [InlineData(0f, 0f, -5f, 5f)]
        public void Vector3D_OrdinaryLength_MatchesAnalyticMagnitude(float x, float y, float z, float expected)
        {
            AssertRelative(expected, new CxVector3D(x, y, z).Length, FloatRelTol, "Length");
        }

        [Theory]
        [InlineData(3.0, 4.0, 5.0)]
        [InlineData(6.0, 8.0, 10.0)]
        [InlineData(5.0, 12.0, 13.0)]
        [InlineData(-8.0, 15.0, 17.0)]
        [InlineData(0.0, -7.0, 7.0)]
        public void DVector2D_OrdinaryLength_MatchesAnalyticMagnitude(double x, double y, double expected)
        {
            AssertRelative(expected, new CxDVector2D(x, y).Length, DoubleRelTol, "Length");
        }

        [Theory]
        [InlineData(2.0, -3.0, 6.0, 7.0)]
        [InlineData(-2.0, 3.0, -6.0, 7.0)]
        [InlineData(1.0, 2.0, 2.0, 3.0)]
        [InlineData(3.0, 4.0, 12.0, 13.0)]
        [InlineData(0.0, 0.0, -5.0, 5.0)]
        public void DVector3D_OrdinaryLength_MatchesAnalyticMagnitude(double x, double y, double z, double expected)
        {
            AssertRelative(expected, new CxDVector3D(x, y, z).Length, DoubleRelTol, "Length");
        }

        // ── Ordinary directions ─────────────────────────────────────────────────

        [Theory]
        [InlineData(3f, 4f, 0.6f, 0.8f)]
        [InlineData(-3f, -4f, -0.6f, -0.8f)]
        [InlineData(0f, 7f, 0f, 1f)]
        [InlineData(0f, -7f, 0f, -1f)]
        [InlineData(2f, -3f, 0.5547002f, -0.8320503f)] // ±(2, -3) / sqrt(13)
        [InlineData(-2f, 3f, -0.5547002f, 0.8320503f)]
        public void Vector2D_OrdinaryDirection_NormalizeMatchesAnalyticUnitVector(
            float x, float y, float expectedX, float expectedY)
        {
            CxVector2D n = new CxVector2D(x, y).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(expectedX, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, FloatRelTol, "Normalize.Y");
        }

        [Theory]
        [InlineData(2f, -3f, 6f, 0.2857143f, -0.4285714f, 0.8571429f)] // ±(2, -3, 6) / 7
        [InlineData(-2f, 3f, -6f, -0.2857143f, 0.4285714f, -0.8571429f)]
        [InlineData(1f, 2f, 2f, 0.3333333f, 0.6666667f, 0.6666667f)]    // ±(1, 2, 2) / 3
        [InlineData(0f, 0f, -5f, 0f, 0f, -1f)]
        public void Vector3D_OrdinaryDirection_NormalizeMatchesAnalyticUnitVector(
            float x, float y, float z, float expectedX, float expectedY, float expectedZ)
        {
            CxVector3D n = new CxVector3D(x, y, z).Normalize();

            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(expectedX, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, FloatRelTol, "Normalize.Y");
            AssertRelative(expectedZ, n.Z, FloatRelTol, "Normalize.Z");
        }

        [Theory]
        [InlineData(3.0, 4.0, 0.6, 0.8)]
        [InlineData(-3.0, -4.0, -0.6, -0.8)]
        [InlineData(2.0, -3.0, 0.5547001962252291, -0.8320502943378437)]
        public void DVector2D_OrdinaryDirection_NormalizeMatchesAnalyticUnitVector(
            double x, double y, double expectedX, double expectedY)
        {
            CxDVector2D n = new CxDVector2D(x, y).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(expectedX, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, DoubleRelTol, "Normalize.Y");
        }

        [Theory]
        [InlineData(2.0, -3.0, 6.0, 0.2857142857142857, -0.42857142857142855, 0.8571428571428571)]
        [InlineData(-2.0, 3.0, -6.0, -0.2857142857142857, 0.42857142857142855, -0.8571428571428571)]
        [InlineData(0.0, 0.0, -5.0, 0.0, 0.0, -1.0)]
        public void DVector3D_OrdinaryDirection_NormalizeMatchesAnalyticUnitVector(
            double x, double y, double z, double expectedX, double expectedY, double expectedZ)
        {
            CxDVector3D n = new CxDVector3D(x, y, z).Normalize();

            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(expectedX, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, DoubleRelTol, "Normalize.Y");
            AssertRelative(expectedZ, n.Z, DoubleRelTol, "Normalize.Z");
        }

        // ── Axis-aligned extreme magnitudes ─────────────────────────────────────
        // A single non-zero component makes the true length trivially the component itself, so the
        // expected value is exact and needs no reference implementation. Squaring 1e20 overflows
        // float and squaring 1e-30 underflows it; the same happens for 1e200 / 1e-200 in double.

        [Theory]
        [InlineData(1e20f, 0f, 1e20f)]
        [InlineData(0f, 1e20f, 1e20f)]
        [InlineData(-1e20f, 0f, 1e20f)]
        [InlineData(0f, -1e20f, 1e20f)]
        [InlineData(1e-30f, 0f, 1e-30f)]
        [InlineData(0f, 1e-30f, 1e-30f)]
        [InlineData(-1e-30f, 0f, 1e-30f)]
        public void Vector2D_AxisMagnitude_LengthIsRepresentableAndFinite(float x, float y, float expected)
        {
            float length = new CxVector2D(x, y).Length;

            AssertFinite(length, "Length");
            AssertRelative(expected, length, FloatRelTol, "Length");
        }

        [Theory]
        [InlineData(1e20f, 0f, 0f, 1e20f)]
        [InlineData(0f, 1e20f, 0f, 1e20f)]
        [InlineData(0f, 0f, -1e20f, 1e20f)]
        [InlineData(1e-30f, 0f, 0f, 1e-30f)]
        [InlineData(0f, 0f, 1e-30f, 1e-30f)]
        public void Vector3D_AxisMagnitude_LengthIsRepresentableAndFinite(float x, float y, float z, float expected)
        {
            float length = new CxVector3D(x, y, z).Length;

            AssertFinite(length, "Length");
            AssertRelative(expected, length, FloatRelTol, "Length");
        }

        [Theory]
        [InlineData(1e200, 0.0, 1e200)]
        [InlineData(0.0, 1e200, 1e200)]
        [InlineData(-1e200, 0.0, 1e200)]
        [InlineData(1e-200, 0.0, 1e-200)]
        [InlineData(0.0, -1e-200, 1e-200)]
        public void DVector2D_AxisMagnitude_LengthIsRepresentableAndFinite(double x, double y, double expected)
        {
            double length = new CxDVector2D(x, y).Length;

            AssertFinite(length, "Length");
            AssertRelative(expected, length, DoubleRelTol, "Length");
        }

        [Theory]
        [InlineData(1e200, 0.0, 0.0, 1e200)]
        [InlineData(0.0, 1e200, 0.0, 1e200)]
        [InlineData(0.0, 0.0, -1e200, 1e200)]
        [InlineData(1e-200, 0.0, 0.0, 1e-200)]
        [InlineData(0.0, 0.0, 1e-200, 1e-200)]
        public void DVector3D_AxisMagnitude_LengthIsRepresentableAndFinite(double x, double y, double z, double expected)
        {
            double length = new CxDVector3D(x, y, z).Length;

            AssertFinite(length, "Length");
            AssertRelative(expected, length, DoubleRelTol, "Length");
        }

        [Theory]
        [InlineData(1e20f, 0f, 1f, 0f)]
        [InlineData(0f, 1e20f, 0f, 1f)]
        [InlineData(-1e20f, 0f, -1f, 0f)]
        [InlineData(1e-30f, 0f, 1f, 0f)]
        [InlineData(0f, -1e-30f, 0f, -1f)]
        public void Vector2D_AxisMagnitude_NormalizeReturnsAxisUnitVector(
            float x, float y, float expectedX, float expectedY)
        {
            CxVector2D n = new CxVector2D(x, y).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(expectedX, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, FloatRelTol, "Normalize.Y");
        }

        [Theory]
        [InlineData(1e200, 0.0, 1.0, 0.0)]
        [InlineData(0.0, 1e200, 0.0, 1.0)]
        [InlineData(-1e200, 0.0, -1.0, 0.0)]
        [InlineData(1e-200, 0.0, 1.0, 0.0)]
        [InlineData(0.0, -1e-200, 0.0, -1.0)]
        public void DVector2D_AxisMagnitude_NormalizeReturnsAxisUnitVector(
            double x, double y, double expectedX, double expectedY)
        {
            CxDVector2D n = new CxDVector2D(x, y).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(expectedX, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, DoubleRelTol, "Normalize.Y");
        }

        // ── Mixed components under a large common scale ─────────────────────────
        // The same 2-3-6 / 2-3 ratios as the ordinary cases, but scaled far beyond the range where
        // the squared components survive. A common scale must cancel out of the direction exactly,
        // including when every component is negative.

        [Theory]
        [InlineData(2e20f, -3e20f, 0.5547002f, -0.8320503f)]
        [InlineData(-2e20f, 3e20f, -0.5547002f, 0.8320503f)]
        [InlineData(-2e20f, -3e20f, -0.5547002f, -0.8320503f)]
        public void Vector2D_LargeCommonScale_NormalizePreservesDirection(
            float x, float y, float expectedX, float expectedY)
        {
            CxVector2D n = new CxVector2D(x, y).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(expectedX, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, FloatRelTol, "Normalize.Y");
        }

        [Theory]
        [InlineData(2e20f, -3e20f, 6e20f, 0.2857143f, -0.4285714f, 0.8571429f)]
        [InlineData(-2e20f, 3e20f, -6e20f, -0.2857143f, 0.4285714f, -0.8571429f)]
        public void Vector3D_LargeCommonScale_NormalizePreservesDirection(
            float x, float y, float z, float expectedX, float expectedY, float expectedZ)
        {
            CxVector3D n = new CxVector3D(x, y, z).Normalize();

            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(expectedX, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, FloatRelTol, "Normalize.Y");
            AssertRelative(expectedZ, n.Z, FloatRelTol, "Normalize.Z");
        }

        [Theory]
        [InlineData(2e200, -3e200, 0.5547001962252291, -0.8320502943378437)]
        [InlineData(-2e200, 3e200, -0.5547001962252291, 0.8320502943378437)]
        public void DVector2D_LargeCommonScale_NormalizePreservesDirection(
            double x, double y, double expectedX, double expectedY)
        {
            CxDVector2D n = new CxDVector2D(x, y).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(expectedX, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, DoubleRelTol, "Normalize.Y");
        }

        [Theory]
        [InlineData(2e200, -3e200, 6e200, 0.2857142857142857, -0.42857142857142855, 0.8571428571428571)]
        [InlineData(-2e200, 3e200, -6e200, -0.2857142857142857, 0.42857142857142855, -0.8571428571428571)]
        public void DVector3D_LargeCommonScale_NormalizePreservesDirection(
            double x, double y, double z, double expectedX, double expectedY, double expectedZ)
        {
            CxDVector3D n = new CxDVector3D(x, y, z).Normalize();

            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(expectedX, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(expectedY, n.Y, DoubleRelTol, "Normalize.Y");
            AssertRelative(expectedZ, n.Z, DoubleRelTol, "Normalize.Z");
        }

        // ── Components whose spread exceeds the usable dynamic range ────────────

        /// <summary>
        /// 1e-30 next to 1e20: after scaling, the small component is 1e-50 and underflows to zero in
        /// float, so the direction is legitimately the +X axis. What matters is that the result is a
        /// finite unit vector rather than the zero vector the old implementation returned.
        /// </summary>
        [Fact]
        public void Vector2D_WidelyMixedMagnitudes_NormalizeStaysUnitAndFinite()
        {
            CxVector2D n = new CxVector2D(1e20f, 1e-30f).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(1f, n.X, FloatRelTol, "Normalize.X");
            AssertNearZero(n.Y, 1e-6f, "Normalize.Y");
        }

        /// <summary>Same spread in 3D, with the dominant component in Z and a negative small one.</summary>
        [Fact]
        public void Vector3D_WidelyMixedMagnitudes_NormalizeStaysUnitAndFinite()
        {
            CxVector3D n = new CxVector3D(1e20f, -1e-30f, 2e20f).Normalize();

            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(0.4472136f, n.X, FloatRelTol, "Normalize.X"); // 1 / sqrt(5)
            AssertNearZero(n.Y, 1e-6f, "Normalize.Y");
            AssertRelative(0.8944272f, n.Z, FloatRelTol, "Normalize.Z"); // 2 / sqrt(5)
        }

        /// <summary>
        /// 1e-200 next to 1e200 underflows to zero in double as well, so the direction is the +X axis.
        /// </summary>
        [Fact]
        public void DVector2D_WidelyMixedMagnitudes_NormalizeStaysUnitAndFinite()
        {
            CxDVector2D n = new CxDVector2D(1e200, 1e-200).Normalize();

            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(1.0, n.X, DoubleRelTol, "Normalize.X");
            AssertNearZero(n.Y, 1e-12, "Normalize.Y");
        }

        /// <summary>Same spread in 3D, with the dominant component in Z and a negative small one.</summary>
        [Fact]
        public void DVector3D_WidelyMixedMagnitudes_NormalizeStaysUnitAndFinite()
        {
            CxDVector3D n = new CxDVector3D(1e200, -1e-200, 2e200).Normalize();

            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(0.4472135954999579, n.X, DoubleRelTol, "Normalize.X"); // 1 / sqrt(5)
            AssertNearZero(n.Y, 1e-12, "Normalize.Y");
            AssertRelative(0.8944271909999159, n.Z, DoubleRelTol, "Normalize.Z"); // 2 / sqrt(5)
        }

        // ── Length that is itself unrepresentable ───────────────────────────────

        /// <summary>
        /// A 3-4-5 triangle scaled by 8e37: the largest component (3.2e38) is representable, but the
        /// true length (4e38) exceeds <see cref="float.MaxValue"/>. <c>Length</c> therefore saturates
        /// to +Infinity, while <c>Normalize</c> — which must not depend on that length — still returns
        /// the exact unit direction. The old code returned the zero vector here.
        /// </summary>
        [Fact]
        public void Vector2D_LengthOverflowsFloatRange_NormalizeIsStillExact()
        {
            var v = new CxVector2D(2.4e38f, 3.2e38f);

            Assert.True(float.IsPositiveInfinity(v.Length),
                $"Length should saturate to +Infinity, got {v.Length:G9}");

            CxVector2D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(0.6f, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(0.8f, n.Y, FloatRelTol, "Normalize.Y");
        }

        /// <summary>Double-precision counterpart: the true length is 2e308, beyond double.MaxValue.</summary>
        [Fact]
        public void DVector2D_LengthOverflowsDoubleRange_NormalizeIsStillExact()
        {
            var v = new CxDVector2D(1.2e308, 1.6e308);

            Assert.True(double.IsPositiveInfinity(v.Length),
                $"Length should saturate to +Infinity, got {v.Length:G17}");

            CxDVector2D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(0.6, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(0.8, n.Y, DoubleRelTol, "Normalize.Y");
        }

        [Fact]
        public void Vector3D_LengthOverflowsFloatRange_NormalizeIsStillExact()
        {
            var v = new CxVector3D(2.4e38f, 3.2e38f, 0f);

            Assert.True(float.IsPositiveInfinity(v.Length),
                $"Length should saturate to +Infinity, got {v.Length:G9}");

            CxVector3D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(0.6f, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(0.8f, n.Y, FloatRelTol, "Normalize.Y");
            AssertNearZero(n.Z, 1e-6f, "Normalize.Z");
        }

        [Fact]
        public void DVector3D_LengthOverflowsDoubleRange_NormalizeIsStillExact()
        {
            var v = new CxDVector3D(1.2e308, 1.6e308, 0.0);

            Assert.True(double.IsPositiveInfinity(v.Length),
                $"Length should saturate to +Infinity, got {v.Length:G17}");

            CxDVector3D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(0.6, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(0.8, n.Y, DoubleRelTol, "Normalize.Y");
            AssertNearZero(n.Z, 1e-12, "Normalize.Z");
        }

        // ── Tiny magnitudes whose squares underflow ─────────────────────────────
        // 3e-30 squared is 9e-60, far below the smallest float subnormal (1.4e-45), so the old code
        // computed a length of exactly 0 and Normalize divided by it. The true length 5e-30 is
        // comfortably representable.

        [Fact]
        public void Vector2D_TinyMagnitude_LengthIsFiniteAndNormalizeIsExact()
        {
            var v = new CxVector2D(3e-30f, 4e-30f);

            float length = v.Length;
            AssertFinite(length, "Length");
            AssertRelative(5e-30f, length, FloatRelTol, "Length");

            CxVector2D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(0.6f, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(0.8f, n.Y, FloatRelTol, "Normalize.Y");
        }

        [Fact]
        public void Vector3D_TinyMagnitude_LengthIsFiniteAndNormalizeIsExact()
        {
            var v = new CxVector3D(3e-30f, 4e-30f, 0f);

            float length = v.Length;
            AssertFinite(length, "Length");
            AssertRelative(5e-30f, length, FloatRelTol, "Length");

            CxVector3D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(0.6f, n.X, FloatRelTol, "Normalize.X");
            AssertRelative(0.8f, n.Y, FloatRelTol, "Normalize.Y");
            AssertNearZero(n.Z, 1e-6f, "Normalize.Z");
        }

        /// <summary>Double counterpart: 9e-600 underflows to zero, while 5e-300 is representable.</summary>
        [Fact]
        public void DVector2D_TinyMagnitude_LengthIsFiniteAndNormalizeIsExact()
        {
            var v = new CxDVector2D(3e-300, 4e-300);

            double length = v.Length;
            AssertFinite(length, "Length");
            AssertRelative(5e-300, length, DoubleRelTol, "Length");

            CxDVector2D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, "Normalize");
            AssertRelative(0.6, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(0.8, n.Y, DoubleRelTol, "Normalize.Y");
        }

        [Fact]
        public void DVector3D_TinyMagnitude_LengthIsFiniteAndNormalizeIsExact()
        {
            var v = new CxDVector3D(3e-300, 4e-300, 0.0);

            double length = v.Length;
            AssertFinite(length, "Length");
            AssertRelative(5e-300, length, DoubleRelTol, "Length");

            CxDVector3D n = v.Normalize();
            AssertUnitLength(n.X, n.Y, n.Z, "Normalize");
            AssertRelative(0.6, n.X, DoubleRelTol, "Normalize.X");
            AssertRelative(0.8, n.Y, DoubleRelTol, "Normalize.Y");
            AssertNearZero(n.Z, 1e-12, "Normalize.Z");
        }

        // ── Preserved undefined-input policy ────────────────────────────────────

        /// <summary>
        /// The zero vector has always had a zero length and an undefined (NaN) direction. The scaling
        /// change must not turn that into an exception or an invented direction.
        /// </summary>
        [Fact]
        public void ZeroVector_KeepsZeroLengthAndUndefinedDirection()
        {
            Assert.Equal(0f, new CxVector2D(0f, 0f).Length);
            Assert.Equal(0f, new CxVector3D(0f, 0f, 0f).Length);
            Assert.Equal(0.0, new CxDVector2D(0.0, 0.0).Length);
            Assert.Equal(0.0, new CxDVector3D(0.0, 0.0, 0.0).Length);

            Assert.True(float.IsNaN(new CxVector2D(0f, 0f).Normalize().X), "CxVector2D zero direction");
            Assert.True(float.IsNaN(new CxVector3D(0f, 0f, 0f).Normalize().Z), "CxVector3D zero direction");
            Assert.True(double.IsNaN(new CxDVector2D(0.0, 0.0).Normalize().Y), "CxDVector2D zero direction");
            Assert.True(double.IsNaN(new CxDVector3D(0.0, 0.0, 0.0).Normalize().Z), "CxDVector3D zero direction");
        }

        /// <summary>
        /// NaN components must stay NaN and infinite components must stay infinite — the scaling
        /// shortcut is only valid for finite non-zero magnitudes.
        /// </summary>
        [Fact]
        public void NonFiniteComponents_KeepNaNAndInfinityResults()
        {
            Assert.True(float.IsNaN(new CxVector2D(float.NaN, 1f).Length), "CxVector2D NaN length");
            Assert.True(float.IsNaN(new CxVector2D(float.NaN, 1f).Normalize().X), "CxVector2D NaN direction");
            Assert.True(float.IsPositiveInfinity(new CxVector2D(float.PositiveInfinity, 1f).Length), "CxVector2D infinite length");

            Assert.True(float.IsNaN(new CxVector3D(1f, float.NaN, 1f).Length), "CxVector3D NaN length");
            Assert.True(float.IsPositiveInfinity(new CxVector3D(1f, float.NegativeInfinity, 1f).Length), "CxVector3D infinite length");

            Assert.True(double.IsNaN(new CxDVector2D(double.NaN, 1.0).Length), "CxDVector2D NaN length");
            Assert.True(double.IsPositiveInfinity(new CxDVector2D(1.0, double.NegativeInfinity).Length), "CxDVector2D infinite length");

            Assert.True(double.IsNaN(new CxDVector3D(double.NaN, 1.0, 1.0).Length), "CxDVector3D NaN length");
            Assert.True(double.IsNaN(new CxDVector3D(double.NaN, 1.0, 1.0).Normalize().Y), "CxDVector3D NaN direction");
            Assert.True(double.IsPositiveInfinity(new CxDVector3D(1.0, 1.0, double.PositiveInfinity).Length), "CxDVector3D infinite length");
        }
    }
}
