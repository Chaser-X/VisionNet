using System;
using VisionNet;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Tests
{
    /// <summary>
    /// API-consistency regressions for the single-point homogeneous transform.
    /// <para>
    /// <see cref="VisionOperator.TransformPoint3D"/> and <see cref="CxMatrix4X4.TransformPoint3D"/>
    /// are documented as having the same semantics, but the operator used to skip the perspective
    /// divide whenever <c>|w| &lt;= 1e-6</c> while the matrix method divides for every usable
    /// <c>w</c>. A valid but small <c>w</c> therefore produced two different points from the same
    /// matrix depending on which entry point was called. Every case below states the expected point
    /// analytically and also checks that both entry points agree.
    /// </para>
    /// <para>
    /// Only the single-point API is exercised; the OpenCL/bulk paths
    /// (<c>TransformSurface</c>, <c>TransformPointCloud</c>, the <c>CxTransform*</c> kernels) keep
    /// their own <c>w</c> handling and are deliberately out of scope here.
    /// </para>
    /// </summary>
    [Trait("Category", "NumericalAudit")]
    public class TransformPointConsistencyTests
    {
        /// <summary>Relative tolerance for single-precision comparisons.</summary>
        private const float RelTol = 1e-6f;

        /// <summary>A probe point with three distinct, non-symmetric components.</summary>
        private static readonly CxPoint3D Probe = new CxPoint3D(2f, -3f, 6f);

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static void AssertFinite(float value, string what) =>
            Assert.False(float.IsNaN(value) || float.IsInfinity(value), $"{what} is not finite: {value:G9}");

        /// <summary>
        /// Relative comparison that also accepts two exact zeros. A relative-only test would divide
        /// by an expected value of zero, and an absolute-only test would be meaningless at 1e7.
        /// </summary>
        private static void AssertClose(float expected, float actual, string what)
        {
            AssertFinite(actual, what);
            float tol = Math.Max(Math.Abs(expected), Math.Abs(actual)) * RelTol;
            Assert.True(Math.Abs(expected - actual) <= tol,
                $"{what}: expected {expected:G9}, got {actual:G9} (relative tolerance {RelTol:G3})");
        }

        /// <summary>Checks the three components of an operator result against analytic values.</summary>
        private static void AssertPointIs(CxPoint3D actual, float x, float y, float z, string what)
        {
            AssertClose(x, actual.X, what + ".X");
            AssertClose(y, actual.Y, what + ".Y");
            AssertClose(z, actual.Z, what + ".Z");
        }

        /// <summary>
        /// Compares the two entry points against each other, using the matrix method as the reference
        /// the operator is required to match.
        /// </summary>
        private static void AssertBothEntriesAgree(CxPoint3D viaOperator, CxPoint3D viaMatrix, string what)
        {
            AssertClose(viaMatrix.X, viaOperator.X, what + ".X");
            AssertClose(viaMatrix.Y, viaOperator.Y, what + ".Y");
            AssertClose(viaMatrix.Z, viaOperator.Z, what + ".Z");
        }

        private static CxMatrix4X4 Matrix(float[] data) => new CxMatrix4X4(data);

        /// <summary>Uniform scale by <paramref name="alpha"/>: maps <c>v</c> to <c>alpha·v</c> and <c>w</c> to <c>alpha</c>.</summary>
        private static CxMatrix4X4 UniformScale(float alpha) => Matrix(new float[]
        {
            alpha, 0,     0,     0,
            0,     alpha, 0,     0,
            0,     0,     alpha, 0,
            0,     0,     0,     alpha,
        });

        /// <summary>Identity with only the bottom-right element set to <paramref name="alpha"/>.</summary>
        private static CxMatrix4X4 IdentityWithW(float alpha) => Matrix(new float[]
        {
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, alpha,
        });

        // ── Tiny non-zero w: the two entry points must agree ────────────────────
        // |w| of 1e-7 and 1e-9 is below the old operator threshold of 1e-6 but is a perfectly usable
        // homogeneous weight, so the divide must happen. The matrix method already did.

        /// <summary>
        /// <c>alpha·I</c> with a tiny <c>alpha</c>: the transform yields <c>alpha·p</c> with
        /// <c>w = alpha</c>, so the perspective divide recovers <c>p</c> itself. The old operator
        /// returned <c>alpha·p</c> (~1e-7) instead.
        /// </summary>
        [Theory]
        [InlineData(1e-7f)]
        [InlineData(-1e-7f)]
        [InlineData(1e-9f)]
        [InlineData(-1e-9f)]
        public void TransformPoint3D_UniformTinyScale_DividesByTinyW(float alpha)
        {
            CxMatrix4X4 m = UniformScale(alpha);

            CxPoint3D viaOperator = VisionOperator.TransformPoint3D(Probe, m);
            CxPoint3D viaMatrix = m.TransformPoint3D(Probe);

            AssertPointIs(viaOperator, 2f, -3f, 6f, $"alpha={alpha:G3} operator");
            AssertBothEntriesAgree(viaOperator, viaMatrix, $"alpha={alpha:G3}");
        }

        /// <summary>
        /// Only <c>w</c> is tiny and the upper 3x3 is the identity, so the point scales by
        /// <c>1 / alpha</c> — a factor of 1e7 or 1e9. The old operator left it undivided.
        /// </summary>
        [Theory]
        [InlineData(1e-7f)]
        [InlineData(-1e-7f)]
        [InlineData(1e-9f)]
        [InlineData(-1e-9f)]
        public void TransformPoint3D_TinyWOnly_DividesByTinyW(float alpha)
        {
            CxMatrix4X4 m = IdentityWithW(alpha);

            CxPoint3D viaOperator = VisionOperator.TransformPoint3D(Probe, m);
            CxPoint3D viaMatrix = m.TransformPoint3D(Probe);

            AssertPointIs(viaOperator,
                (float)(2.0 / alpha), (float)(-3.0 / alpha), (float)(6.0 / alpha),
                $"alpha={alpha:G3} operator");
            AssertBothEntriesAgree(viaOperator, viaMatrix, $"alpha={alpha:G3}");
        }

        // ── Ordinary w: unchanged behaviour ─────────────────────────────────────

        /// <summary>An ordinary translation has <c>w = 1</c>, so the divide is a no-op.</summary>
        [Fact]
        public void TransformPoint3D_OrdinaryTranslation_DividesToAnalyticResult()
        {
            CxMatrix4X4 m = Matrix(new float[]
            {
                1, 0, 0,  10,
                0, 1, 0, -20,
                0, 0, 1,  30,
                0, 0, 0,   1,
            });

            CxPoint3D viaOperator = VisionOperator.TransformPoint3D(Probe, m);
            CxPoint3D viaMatrix = m.TransformPoint3D(Probe);

            AssertPointIs(viaOperator, 12f, -23f, 36f, "operator");
            AssertBothEntriesAgree(viaOperator, viaMatrix, "translation");
        }

        /// <summary>
        /// A genuine perspective row: <c>w = z + 1 = 7</c> for the probe, so every component is
        /// divided by 7. Guards that ordinary (non-tiny, non-unit) <c>w</c> still divides.
        /// </summary>
        [Fact]
        public void TransformPoint3D_OrdinaryPerspective_DividesToAnalyticResult()
        {
            CxMatrix4X4 m = Matrix(new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 1, 1,
            });

            CxPoint3D viaOperator = VisionOperator.TransformPoint3D(Probe, m);
            CxPoint3D viaMatrix = m.TransformPoint3D(Probe);

            AssertPointIs(viaOperator, 0.2857143f, -0.4285714f, 0.8571429f, "operator"); // ±(2, -3, 6) / 7
            AssertBothEntriesAgree(viaOperator, viaMatrix, "perspective");
        }

        // ── Degenerate w: the pre-existing policy must be preserved ─────────────

        /// <summary>
        /// <c>w == 0</c> has never divided and still must not: the raw <c>M · v</c> is returned.
        /// </summary>
        [Fact]
        public void TransformPoint3D_ZeroW_KeepsUndividedResult()
        {
            CxMatrix4X4 m = Matrix(new float[]
            {
                2, 0, 0, 10,
                0, 3, 0, 20,
                0, 0, 4, 30,
                0, 0, 0,  0,
            });

            CxPoint3D viaOperator = VisionOperator.TransformPoint3D(Probe, m);
            CxPoint3D viaMatrix = m.TransformPoint3D(Probe);

            AssertPointIs(viaOperator, 14f, 11f, 54f, "operator");
            AssertBothEntriesAgree(viaOperator, viaMatrix, "zero w");
        }

        /// <summary>
        /// A NaN <c>w</c> also skips the divide, so the result stays finite instead of becoming NaN.
        /// </summary>
        [Fact]
        public void TransformPoint3D_NaNw_KeepsUndividedResult()
        {
            CxMatrix4X4 m = Matrix(new float[]
            {
                2, 0, 0, 10,
                0, 3, 0, 20,
                0, 0, 4, 30,
                0, 0, 0, float.NaN,
            });

            CxPoint3D viaOperator = VisionOperator.TransformPoint3D(Probe, m);
            CxPoint3D viaMatrix = m.TransformPoint3D(Probe);

            AssertPointIs(viaOperator, 14f, 11f, 54f, "operator");
            AssertBothEntriesAgree(viaOperator, viaMatrix, "NaN w");
        }

        /// <summary>
        /// An infinite <c>w</c> is neither zero nor NaN, so it divides: a finite numerator over an
        /// infinite denominator is zero.
        /// </summary>
        [Fact]
        public void TransformPoint3D_InfiniteW_DividesToZero()
        {
            CxMatrix4X4 m = Matrix(new float[]
            {
                2, 0, 0, 10,
                0, 3, 0, 20,
                0, 0, 4, 30,
                0, 0, 0, float.PositiveInfinity,
            });

            CxPoint3D viaOperator = VisionOperator.TransformPoint3D(Probe, m);
            CxPoint3D viaMatrix = m.TransformPoint3D(Probe);

            AssertPointIs(viaOperator, 0f, 0f, 0f, "operator");
            AssertBothEntriesAgree(viaOperator, viaMatrix, "infinite w");
        }

        // ── Preserved null contract ─────────────────────────────────────────────

        /// <summary>
        /// The operator has never guarded against a null matrix. Delegating keeps that contract:
        /// the reference is dereferenced and <see cref="NullReferenceException"/> is thrown.
        /// </summary>
        [Fact]
        public void TransformPoint3D_NullMatrix_ThrowsNullReferenceException()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                VisionOperator.TransformPoint3D(Probe, null);
            });
        }
    }
}
