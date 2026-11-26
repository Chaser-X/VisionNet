using System;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Tests
{
    /// <summary>
    /// Regression tests for the numerical defects fixed in <see cref="CxMatrix3X3"/> and
    /// <see cref="CxMatrix4X4"/>:
    /// <list type="number">
    /// <item><description><c>LookAt</c> returned the view matrix transposed (its translation sat in
    /// <c>Data[12..14]</c>), so the result did not match the row-major <c>M · v</c> convention of
    /// <c>TransformPoint3D</c>;</description></item>
    /// <item><description><c>RotationAxis</c> skipped normalisation for axes shorter than 1e-6, which
    /// produced matrices that are not rotations at all;</description></item>
    /// <item><description><c>Inverse</c>, <c>Solve2x2</c> and <c>Solve3x3</c> rejected well-conditioned
    /// systems whose determinant merely happened to be small in absolute terms;</description></item>
    /// <item><description><c>TransformPoint2D</c> and <c>TransformPoint3D</c> skipped the perspective
    /// divide for finite non-zero <c>w</c> smaller than 1e-6.</description></item>
    /// </list>
    /// Expected values come from analytic matrices, prescribed solutions and independent
    /// oracles — never from the production inverse.
    /// </summary>
    [Trait("Category", "NumericalAudit")]
    public class CxMatrixNumericalTests
    {
        /// <summary>
        /// Relative tolerance for single-precision results, with an absolute floor of the same
        /// size near zero.
        /// </summary>
        private const float Tol = 1e-5f;

        // ── Assertion helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Asserts <paramref name="actual"/> matches <paramref name="expected"/> within
        /// <paramref name="tol"/> relative, or absolutely when the expected value is near zero.
        /// </summary>
        private static void AssertNear(float expected, float actual, string what, float tol = Tol) =>
            Assert.True(Math.Abs(expected - actual) <= tol * Math.Max(1f, Math.Abs(expected)),
                $"{what}: expected {expected:G9}, got {actual:G9} (tolerance {tol:G3} relative)");

        /// <summary>Fails when any element is not finite; always called before a tolerance check.</summary>
        private static void AssertAllFinite(float[] values, string what)
        {
            for (int i = 0; i < values.Length; i++)
                Assert.True(!float.IsNaN(values[i]) && !float.IsInfinity(values[i]),
                    $"{what}: element {i} is {values[i]}");
        }

        private static void AssertAllFinite(CxMatrix3X3 m, string what) => AssertAllFinite(m.Data, what);

        private static void AssertAllFinite(CxMatrix4X4 m, string what) => AssertAllFinite(m.Data, what);

        /// <summary>Asserts two 4×4 matrices agree element by element, after checking both are finite.</summary>
        private static void AssertMatrixNear(CxMatrix4X4 expected, CxMatrix4X4 actual, string what)
        {
            AssertAllFinite(expected, what + " (expected)");
            AssertAllFinite(actual, what);
            for (int i = 0; i < 16; i++)
                AssertNear(expected.Data[i], actual.Data[i], $"{what} [{i / 4},{i % 4}]");
        }

        /// <summary>Asserts a row-major 3×3 block is the identity within tolerance.</summary>
        private static void AssertIdentity3x3(float[] block, string what)
        {
            AssertAllFinite(block, what);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    AssertNear(i == j ? 1f : 0f, block[i * 3 + j], $"{what} [{i},{j}]");
        }

        // ── Independent oracles and builders ────────────────────────────────────

        /// <summary>Copies the upper-left 3×3 block of a 4×4 matrix in row-major order.</summary>
        private static float[] Block3x3(CxMatrix4X4 m) =>
            new float[]
            {
                m.Data[0], m.Data[1], m.Data[2],
                m.Data[4], m.Data[5], m.Data[6],
                m.Data[8], m.Data[9], m.Data[10],
            };

        /// <summary>Multiplies a row-major 3×3 block by a vector, without the production transform.</summary>
        private static float[] ApplyBlock3x3(float[] a, float x0, float x1, float x2) =>
            new float[]
            {
                a[0] * x0 + a[1] * x1 + a[2] * x2,
                a[3] * x0 + a[4] * x1 + a[5] * x2,
                a[6] * x0 + a[7] * x1 + a[8] * x2,
            };

        /// <summary>Multiplies two row-major 3×3 blocks, without the production operator.</summary>
        private static float[] MultiplyBlock3x3(float[] a, float[] b)
        {
            var r = new float[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
            return r;
        }

        /// <summary>Builds a 4×4 matrix from a row-major 3×3 block and an identity fourth row and column.</summary>
        private static CxMatrix4X4 FromBlock3x3(float[] a) =>
            new CxMatrix4X4(new float[]
            {
                a[0], a[1], a[2], 0,
                a[3], a[4], a[5], 0,
                a[6], a[7], a[8], 0,
                0, 0, 0, 1,
            });

        /// <summary>Builds a 3×3 affine matrix whose upper-left 2×2 block is given explicitly.</summary>
        private static CxMatrix3X3 Affine2x2(float a00, float a01, float a10, float a11) =>
            new CxMatrix3X3(new float[] { a00, a01, 0, a10, a11, 0, 0, 0, 1 });

        /// <summary>Builds <c>alpha · I</c> as a 4×4 matrix (note that <c>Scale</c> leaves <c>Data[15]</c> at 1).</summary>
        private static CxMatrix4X4 ScaledIdentity4x4(float alpha) =>
            new CxMatrix4X4(new float[]
            {
                alpha, 0, 0, 0,
                0, alpha, 0, 0,
                0, 0, alpha, 0,
                0, 0, 0, alpha,
            });

        /// <summary>
        /// Independent row-major Rodrigues construction from a unit axis, used as the expected
        /// rotation for the <c>RotationAxis</c> magnitude tests.
        /// </summary>
        private static CxMatrix4X4 Rodrigues(float kx, float ky, float kz, float angleDeg)
        {
            float rad = angleDeg * (float)Math.PI / 180f;
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad), t = 1f - c;
            return new CxMatrix4X4(new float[]
            {
                t * kx * kx + c,      t * kx * ky - s * kz, t * kx * kz + s * ky, 0,
                t * kx * ky + s * kz, t * ky * ky + c,      t * ky * kz - s * kx, 0,
                t * kx * kz - s * ky, t * ky * kz + s * kx, t * kz * kz + c,      0,
                0, 0, 0, 1,
            });
        }

        /// <summary>
        /// Builds the expected row-major view matrix straight from the definition of the camera
        /// basis (<c>s = f × up</c>, <c>u = s × f</c>), using its own cross products and lengths
        /// rather than the production <see cref="CxVector3D"/> helpers.
        /// </summary>
        private static CxMatrix4X4 ExpectedViewMatrix(
            float ex, float ey, float ez, float cx, float cy, float cz, float ux, float uy, float uz)
        {
            float fx = cx - ex, fy = cy - ey, fz = cz - ez;
            float fLen = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);
            fx /= fLen; fy /= fLen; fz /= fLen;

            float sx = fy * uz - fz * uy;
            float sy = fz * ux - fx * uz;
            float sz = fx * uy - fy * ux;
            float sLen = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
            sx /= sLen; sy /= sLen; sz /= sLen;

            float vx = sy * fz - sz * fy;
            float vy = sz * fx - sx * fz;
            float vz = sx * fy - sy * fx;

            return new CxMatrix4X4(new float[]
            {
                 sx,  sy,  sz, -(sx * ex + sy * ey + sz * ez),
                 vx,  vy,  vz, -(vx * ex + vy * ey + vz * ez),
                -fx, -fy, -fz,   fx * ex + fy * ey + fz * ez,
                 0,   0,   0,   1,
            });
        }

        /// <summary>Asserts the upper-left 3×3 block is a proper rotation: orthonormal rows, determinant +1.</summary>
        private static void AssertProperRotation(CxMatrix4X4 m, string what)
        {
            float[] r = Block3x3(m);

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    float dot = r[i * 3] * r[j * 3] + r[i * 3 + 1] * r[j * 3 + 1] + r[i * 3 + 2] * r[j * 3 + 2];
                    AssertNear(i == j ? 1f : 0f, dot, $"{what}: R·Rᵀ[{i},{j}]");
                }

            float det = r[0] * (r[4] * r[8] - r[5] * r[7])
                      - r[1] * (r[3] * r[8] - r[5] * r[6])
                      + r[2] * (r[3] * r[7] - r[4] * r[6]);
            AssertNear(1f, det, $"{what}: det(R)");
        }

        // ── LookAt ──────────────────────────────────────────────────────────────

        /// <summary>
        /// A camera one unit to the side and ten units up the +Z axis maps itself to the origin
        /// and the world origin onto the negative Z axis, sqrt(101) away.
        /// </summary>
        [Fact]
        public void LookAt_EyeOnPositiveZ_MapsEyeToOriginAndTargetToNegativeZ()
        {
            var eye = new CxPoint3D(1f, 0f, 10f);
            var center = new CxPoint3D(0f, 0f, 0f);
            CxMatrix4X4 m = CxMatrix4X4.LookAt(eye, center, new CxVector3D(0f, 1f, 0f));

            AssertAllFinite(m, "LookAt(eye on +Z)");

            CxPoint3D eyeImage = m.TransformPoint3D(eye);
            AssertNear(0f, eyeImage.X, "eye image X");
            AssertNear(0f, eyeImage.Y, "eye image Y");
            AssertNear(0f, eyeImage.Z, "eye image Z");

            CxPoint3D targetImage = m.TransformPoint3D(center);
            AssertNear(0f, targetImage.X, "target image X");
            AssertNear(0f, targetImage.Y, "target image Y");
            AssertNear(-(float)Math.Sqrt(101.0), targetImage.Z, "target image Z");
        }

        /// <summary>
        /// The mirrored camera. With the transposed layout the world origin came back exactly at
        /// the camera position instead of on the negative Z axis.
        /// </summary>
        [Fact]
        public void LookAt_EyeOnNegativeZ_MapsWorldOriginToNegativeZ()
        {
            var eye = new CxPoint3D(-1f, 0f, -10f);
            var center = new CxPoint3D(0f, 0f, 0f);
            CxMatrix4X4 m = CxMatrix4X4.LookAt(eye, center, new CxVector3D(0f, 1f, 0f));

            AssertAllFinite(m, "LookAt(eye on -Z)");

            CxPoint3D eyeImage = m.TransformPoint3D(eye);
            AssertNear(0f, eyeImage.X, "eye image X");
            AssertNear(0f, eyeImage.Y, "eye image Y");
            AssertNear(0f, eyeImage.Z, "eye image Z");

            CxPoint3D originImage = m.TransformPoint3D(center);
            AssertNear(0f, originImage.X, "origin image X");
            AssertNear(0f, originImage.Y, "origin image Y");
            AssertNear(-(float)Math.Sqrt(101.0), originImage.Z, "origin image Z");
        }

        /// <summary>
        /// Off-axis camera and target: every element must match the view matrix built from the
        /// definition of the camera basis, the rotation rows must be orthonormal with determinant
        /// +1, the translation must be <c>-R · eye</c>, and the last row must be
        /// <c>(0, 0, 0, 1)</c>.
        /// </summary>
        [Fact]
        public void LookAt_TiltedOffAxis_MatchesIndependentViewMatrix()
        {
            var eye = new CxPoint3D(3f, 4f, 10f);
            var center = new CxPoint3D(0.5f, -1f, 2f);
            CxMatrix4X4 m = CxMatrix4X4.LookAt(eye, center, new CxVector3D(0f, 1f, 0f));
            CxMatrix4X4 expected = ExpectedViewMatrix(3f, 4f, 10f, 0.5f, -1f, 2f, 0f, 1f, 0f);

            AssertMatrixNear(expected, m, "LookAt tilted");
            AssertNear(0f, m.Data[12], "last row [3,0]");
            AssertNear(0f, m.Data[13], "last row [3,1]");
            AssertNear(0f, m.Data[14], "last row [3,2]");
            AssertNear(1f, m.Data[15], "last row [3,3]");
            AssertProperRotation(m, "LookAt tilted");

            // The translation column is -R · eye, so the camera position lands on the origin.
            for (int row = 0; row < 3; row++)
                AssertNear(0f,
                    m.Data[row * 4] * eye.X + m.Data[row * 4 + 1] * eye.Y
                    + m.Data[row * 4 + 2] * eye.Z + m.Data[row * 4 + 3],
                    $"R·eye + t, row {row}");
        }

        // ── RotationAxis ────────────────────────────────────────────────────────

        /// <summary>
        /// The magnitude of the axis must not change the rotation: an axis scaled by 1e-7 — or by
        /// 1e30 — points the same way as an ordinary one. The old length threshold left such an
        /// axis unnormalised (or overflowed its length to infinity) and returned a matrix that is
        /// not a rotation at all.
        /// </summary>
        [Theory]
        [InlineData(1e-7f)]
        [InlineData(1f)]
        [InlineData(1e3f)]
        [InlineData(1e30f)]
        [InlineData(1e-25f)]
        [InlineData(1e-30f)]
        public void RotationAxis_AxisMagnitude_DoesNotChangeRotation(float scale)
        {
            CxMatrix4X4 m = CxMatrix4X4.RotationAxis(new CxVector3D(0.6f * scale, 0f, 0.8f * scale), 60f);
            CxMatrix4X4 expected = Rodrigues(0.6f, 0f, 0.8f, 60f);
            string what = $"RotationAxis scaled by {scale:G3}";

            AssertMatrixNear(expected, m, what);
            AssertProperRotation(m, what);
        }

        /// <summary>
        /// 60° about the unit axis (0.6, 0, 0.8): the axis itself is invariant, and the
        /// perpendicular vector (0.8, 0, -0.6) goes to
        /// <c>v·cos 60° + (k × v)·sin 60° = (0.4, √3/2, -0.3)</c>.
        /// </summary>
        [Fact]
        public void RotationAxis_SixtyDegrees_RotatesPerpendicularVectorAnalytically()
        {
            CxMatrix4X4 m = CxMatrix4X4.RotationAxis(new CxVector3D(0.6f, 0f, 0.8f), 60f);
            float[] block = Block3x3(m);

            float[] axisImage = ApplyBlock3x3(block, 0.6f, 0f, 0.8f);
            AssertAllFinite(axisImage, "axis image");
            AssertNear(0.6f, axisImage[0], "axis image X");
            AssertNear(0f, axisImage[1], "axis image Y");
            AssertNear(0.8f, axisImage[2], "axis image Z");

            float[] perpendicularImage = ApplyBlock3x3(block, 0.8f, 0f, -0.6f);
            AssertAllFinite(perpendicularImage, "perpendicular image");
            AssertNear(0.4f, perpendicularImage[0], "perpendicular image X");
            AssertNear((float)(Math.Sqrt(3.0) / 2.0), perpendicularImage[1], "perpendicular image Y");
            AssertNear(-0.3f, perpendicularImage[2], "perpendicular image Z");
        }

        /// <summary>
        /// Compatibility of the undefined-input propagation: a NaN axis is left unnormalised, so the
        /// elements that do not mix the NaN component stay finite. Only the classification is pinned
        /// here - no NaN bit pattern is claimed - and the axis is an invalid input either way.
        /// </summary>
        [Fact]
        public void RotationAxis_NaNAxis_KeepsNonFinitePropagation()
        {
            CxMatrix4X4 m = CxMatrix4X4.RotationAxis(new CxVector3D(float.NaN, 0f, 0f), 60f);

            Assert.True(float.IsNaN(m.Data[0]), "element mixing the NaN component stays NaN");
            Assert.True(float.IsNaN(m.Data[9]), "element mixing the NaN component stays NaN");
            Assert.False(float.IsNaN(m.Data[5]), "element free of the NaN component stays finite");
            Assert.False(float.IsNaN(m.Data[10]), "element free of the NaN component stays finite");
            AssertNear(0.5f, m.Data[5], "finite element tyy + cos60");
            AssertNear(0.5f, m.Data[10], "finite element tzz + cos60");
            AssertNear(1f, m.Data[15], "homogeneous corner");
            AssertNear(0f, m.Data[12], "last row [3,0]");
        }

        /// <summary>
        /// The infinite axis takes the other legacy branch (its length exceeds the threshold), which
        /// again leaves the elements that avoid the infinite component finite. Same classification
        /// caveat as the NaN case.
        /// </summary>
        [Fact]
        public void RotationAxis_InfiniteAxis_KeepsNonFinitePropagation()
        {
            CxMatrix4X4 m = CxMatrix4X4.RotationAxis(new CxVector3D(float.PositiveInfinity, 0f, 0f), 60f);

            Assert.True(float.IsNaN(m.Data[0]), "element mixing the infinite component stays NaN");
            Assert.True(float.IsNaN(m.Data[9]), "element mixing the infinite component stays NaN");
            Assert.False(float.IsNaN(m.Data[5]), "element free of the infinite component stays finite");
            Assert.False(float.IsNaN(m.Data[10]), "element free of the infinite component stays finite");
            AssertNear(0.5f, m.Data[5], "finite element tyy + cos60");
            AssertNear(0.5f, m.Data[10], "finite element tzz + cos60");
            AssertNear(1f, m.Data[15], "homogeneous corner");
            AssertNear(0f, m.Data[12], "last row [3,0]");
        }

        // ── CxMatrix3X3.Inverse ─────────────────────────────────────────────────

        /// <summary>
        /// A uniform scale whose determinant is far below the old absolute threshold (5e-4 gives
        /// 2.5e-7) must still invert to the analytic reciprocal. The reciprocal is asserted
        /// scale-free so that an overflowed result cannot hide behind the tolerance floor.
        /// </summary>
        [Theory]
        [InlineData(5e-4f)]
        [InlineData(1e-3f)]
        [InlineData(1f)]
        [InlineData(1e20f)]
        public void Matrix3X3_Inverse_UniformScale_MatchesAnalyticReciprocal(float scale)
        {
            CxMatrix3X3 m = CxMatrix3X3.Scale(scale, scale);
            CxMatrix3X3 inverse = m.Inverse();
            string what = $"Scale({scale:G3}) inverse";

            AssertAllFinite(inverse, what);
            AssertNear(1f, inverse.Data[0] * scale, what + " [0,0] · scale");
            AssertNear(1f, inverse.Data[4] * scale, what + " [1,1] · scale");
            AssertNear(1f, inverse.Data[8], what + " [2,2]");
            AssertIdentity3x3(MultiplyBlock3x3(m.Data, inverse.Data), what + " A·A⁻¹");
        }

        /// <summary>
        /// An ordinary asymmetric matrix with determinant 25 inverts to the analytic adjugate
        /// <c>[[0.48, -0.16, 0.04], [0.04, 0.32, -0.08], [-0.12, 0.04, 0.24]]</c>.
        /// </summary>
        [Fact]
        public void Matrix3X3_Inverse_OrdinaryAsymmetric_MatchesAnalyticAdjugate()
        {
            var m = new CxMatrix3X3(new float[] { 2, 1, 0, 0, 3, 1, 1, 0, 4 });
            CxMatrix3X3 inverse = m.Inverse();
            const string what = "inverse of [[2,1,0],[0,3,1],[1,0,4]]";

            AssertAllFinite(inverse, what);
            AssertNear(0.48f, inverse.Data[0], what + " [0,0]");
            AssertNear(-0.16f, inverse.Data[1], what + " [0,1]");
            AssertNear(0.04f, inverse.Data[2], what + " [0,2]");
            AssertNear(0.04f, inverse.Data[3], what + " [1,0]");
            AssertNear(0.32f, inverse.Data[4], what + " [1,1]");
            AssertNear(-0.08f, inverse.Data[5], what + " [1,2]");
            AssertNear(-0.12f, inverse.Data[6], what + " [2,0]");
            AssertNear(0.04f, inverse.Data[7], what + " [2,1]");
            AssertNear(0.24f, inverse.Data[8], what + " [2,2]");
            AssertIdentity3x3(MultiplyBlock3x3(m.Data, inverse.Data), what + " A·A⁻¹");
        }

        /// <summary>
        /// A homogeneous row whose translation dwarfs its coefficients: the row scale is 1000 while
        /// the pivot in that row is 5e-4, so a test that measured the pivot against the whole row
        /// would call this invertible matrix singular. The analytic inverse
        /// <c>[[2000, 0, -2e6], [0, 2000, 4e6], [0, 0, 1]]</c> is recovered exactly, and a point
        /// round trip is deliberately not asserted: the 2e6-magnitude entries cancel against
        /// coordinates of the same size, so only the elementwise comparison is meaningful here.
        /// </summary>
        [Fact]
        public void Matrix3X3_Inverse_SmallScaleWithLargeTranslation_MatchesAnalyticInverse()
        {
            CxMatrix3X3 m = CxMatrix3X3.Translation(1000f, -2000f) * CxMatrix3X3.Scale(5e-4f, 5e-4f);
            CxMatrix3X3 inverse = m.Inverse();
            const string what = "inverse of T(1000,-2000) · Scale(5e-4)";

            AssertAllFinite(inverse, what);
            AssertNear(2000f, inverse.Data[0], what + " [0,0]");
            AssertNear(0f, inverse.Data[1], what + " [0,1]");
            AssertNear(-2e6f, inverse.Data[2], what + " [0,2]");
            AssertNear(0f, inverse.Data[3], what + " [1,0]");
            AssertNear(2000f, inverse.Data[4], what + " [1,1]");
            AssertNear(4e6f, inverse.Data[5], what + " [1,2]");
            AssertNear(0f, inverse.Data[6], what + " [2,0]");
            AssertNear(0f, inverse.Data[7], what + " [2,1]");
            AssertNear(1f, inverse.Data[8], what + " [2,2]");
        }

        /// <summary>
        /// Genuinely singular matrices stay rejected: a row that is a multiple of another, a row
        /// of zeros, and the same rank deficiency after a uniform scaling down by 1e-7.
        /// </summary>
        [Fact]
        public void Matrix3X3_Inverse_SingularMatrices_Throw()
        {
            var dependentRows = new CxMatrix3X3(new float[] { 1, 2, 3, 2, 4, 6, 0, 0, 1 });
            var zeroRow = new CxMatrix3X3(new float[] { 1, 2, 3, 0, 0, 0, 0, 0, 1 });
            var scaledDependentRows = new CxMatrix3X3(new float[]
            {
                1e-7f, 2e-7f, 3e-7f,
                2e-7f, 4e-7f, 6e-7f,
                0f, 0f, 1e-7f,
            });

            Assert.Throws<InvalidOperationException>(() => { dependentRows.Inverse(); });
            Assert.Throws<InvalidOperationException>(() => { zeroRow.Inverse(); });
            Assert.Throws<InvalidOperationException>(() => { scaledDependentRows.Inverse(); });
        }

        // ── CxMatrix3X3.Solve2x2 ────────────────────────────────────────────────

        /// <summary>An ordinary asymmetric 2×2 system solves to the prescribed x = (5, -2).</summary>
        [Fact]
        public void Solve2x2_OrdinaryAsymmetric_ReturnsPrescribedSolution()
        {
            CxMatrix3X3 m = Affine2x2(3f, 2f, 1f, 4f);
            var b = new CxVector2D(3f * 5f + 2f * -2f, 1f * 5f + 4f * -2f);

            CxVector2D x = m.Solve2x2(b);

            AssertAllFinite(new float[] { x.X, x.Y }, "Solve2x2 solution");
            AssertNear(5f, x.X, "solution X");
            AssertNear(-2f, x.Y, "solution Y");
        }

        /// <summary>The first column has no usable pivot, so the solve has to swap rows.</summary>
        [Fact]
        public void Solve2x2_RowSwapRequired_ReturnsPrescribedSolution()
        {
            CxMatrix3X3 m = Affine2x2(0f, 2f, 3f, 0f);
            var b = new CxVector2D(0f * 5f + 2f * -2f, 3f * 5f + 0f * -2f);

            CxVector2D x = m.Solve2x2(b);

            AssertAllFinite(new float[] { x.X, x.Y }, "Solve2x2 row swap");
            AssertNear(5f, x.X, "solution X");
            AssertNear(-2f, x.Y, "solution Y");
        }

        /// <summary>
        /// A uniformly small system is well conditioned: 1e-4 · I has determinant 1e-8, far below
        /// the old absolute threshold, yet its solution is exact.
        /// </summary>
        [Theory]
        [InlineData(1e-3f)]
        [InlineData(1e-4f)]
        [InlineData(1e-6f)]
        public void Solve2x2_SmallUniformScale_ReturnsPrescribedSolution(float scale)
        {
            CxMatrix3X3 m = Affine2x2(scale, 0f, 0f, scale);
            var b = new CxVector2D(scale * 5f, scale * -2f);

            CxVector2D x = m.Solve2x2(b);

            AssertAllFinite(new float[] { x.X, x.Y }, $"Solve2x2 at scale {scale:G3}");
            AssertNear(5f, x.X, $"solution X at scale {scale:G3}");
            AssertNear(-2f, x.Y, $"solution Y at scale {scale:G3}");
        }

        /// <summary>
        /// A diagonal system whose two rows differ by nine orders of magnitude: the small row must
        /// not be treated as a zero row by the singularity test.
        /// </summary>
        [Fact]
        public void Solve2x2_MixedRowScales_ReturnsPrescribedSolution()
        {
            CxMatrix3X3 m = Affine2x2(1e-6f, 0f, 0f, 1e3f);
            var b = new CxVector2D(1e-6f * 5f, 1e3f * -2f);

            CxVector2D x = m.Solve2x2(b);

            AssertAllFinite(new float[] { x.X, x.Y }, "Solve2x2 mixed row scales");
            AssertNear(5f, x.X, "solution X");
            AssertNear(-2f, x.Y, "solution Y");
        }

        /// <summary>
        /// Two rows coupled across a scale gap of 1e40: without equilibrating the equations the
        /// elimination factor is 1e40, which overflows to infinity and turns the whole solution into
        /// NaN. Scaling the rows - and the right-hand side with them - recovers the prescribed x.
        /// </summary>
        [Fact]
        public void Solve2x2_WidelyScaledCoupledRows_ReturnsPrescribedSolution()
        {
            CxMatrix3X3 m = Affine2x2(1e-20f, 1e-20f, 1e20f, 2e20f);
            var b = new CxVector2D(1e-20f * 1f + 1e-20f * 2f, 1e20f * 1f + 2e20f * 2f);

            CxVector2D x = m.Solve2x2(b);

            AssertAllFinite(new float[] { x.X, x.Y }, "Solve2x2 coupled rows");
            AssertNear(1f, x.X, "solution X");
            AssertNear(2f, x.Y, "solution Y");
        }

        /// <summary>
        /// Boundary of the conditional equilibration: a row-scale ratio of 9 or 10 leaves the system
        /// untouched (the threshold is exceeded only above 10, so the equality case is unscaled) while
        /// 11 scales the rows. Both branches must return the prescribed solution - with scaled partial
        /// pivoting every elimination factor is bounded by the row-scale ratio, so the unscaled branch
        /// is just as accurate at these ratios.
        /// </summary>
        [Theory]
        [InlineData(9f)]
        [InlineData(10f)]
        [InlineData(11f)]
        public void Solve2x2_RowScaleRatioAtEquilibrationThreshold_ReturnsPrescribedSolution(float ratio)
        {
            CxMatrix3X3 m = Affine2x2(ratio, 0f, 0f, 1f);
            var b = new CxVector2D(ratio * 1f, 1f * 2f);

            CxVector2D x = m.Solve2x2(b);

            AssertAllFinite(new float[] { x.X, x.Y }, $"Solve2x2 at row ratio {ratio:G3}");
            AssertNear(1f, x.X, $"solution X at row ratio {ratio:G3}");
            AssertNear(2f, x.Y, $"solution Y at row ratio {ratio:G3}");
        }

        /// <summary>The translation column and the third row must not take part in the solve.</summary>
        [Fact]
        public void Solve2x2_IgnoresTranslationAndThirdRow()
        {
            var m = new CxMatrix3X3(new float[] { 3, 2, 7, 1, 4, -3, 5, 6, 2 });
            var b = new CxVector2D(3f * 5f + 2f * -2f, 1f * 5f + 4f * -2f);

            CxVector2D x = m.Solve2x2(b);

            AssertAllFinite(new float[] { x.X, x.Y }, "Solve2x2 with translation");
            AssertNear(5f, x.X, "solution X");
            AssertNear(-2f, x.Y, "solution Y");
        }

        /// <summary>A rank-deficient and an all-zero 2×2 submatrix must both be rejected.</summary>
        [Fact]
        public void Solve2x2_SingularSubmatrix_Throws()
        {
            CxMatrix3X3 dependent = Affine2x2(2f, 4f, 1f, 2f);
            CxMatrix3X3 zero = Affine2x2(0f, 0f, 0f, 0f);

            Assert.Throws<InvalidOperationException>(() => { dependent.Solve2x2(new CxVector2D(1f, 1f)); });
            Assert.Throws<InvalidOperationException>(() => { zero.Solve2x2(new CxVector2D(1f, 1f)); });
        }

        // ── CxMatrix3X3.TransformPoint2D ────────────────────────────────────────

        /// <summary>
        /// <c>alpha · I</c> divides by a small positive or negative w, so the point comes back
        /// unchanged instead of being returned multiplied by alpha.
        /// </summary>
        [Theory]
        [InlineData(1e-7f)]
        [InlineData(-1e-7f)]
        [InlineData(1e-8f)]
        public void TransformPoint2D_SmallUniformScale_DividesBySmallW(float alpha)
        {
            var m = new CxMatrix3X3(new float[] { alpha, 0, 0, 0, alpha, 0, 0, 0, alpha });
            CxPoint2D p = m.TransformPoint2D(new CxPoint2D(3f, 4f));

            AssertAllFinite(new float[] { p.X, p.Y }, $"TransformPoint2D at alpha={alpha:G3}");
            AssertNear(3f, p.X, $"X at alpha={alpha:G3}");
            AssertNear(4f, p.Y, $"Y at alpha={alpha:G3}");
        }

        /// <summary>
        /// Only the homogeneous w shrinks: the divide must still happen, giving the affine result
        /// scaled by 1/w (3e7 rather than 3).
        /// </summary>
        [Theory]
        [InlineData(1e-7f)]
        [InlineData(-1e-7f)]
        public void TransformPoint2D_OnlyWShrinks_DividesBySmallW(float alpha)
        {
            var m = new CxMatrix3X3(new float[] { 1, 0, 0, 0, 1, 0, 0, 0, alpha });
            CxPoint2D p = m.TransformPoint2D(new CxPoint2D(3f, 4f));

            AssertAllFinite(new float[] { p.X, p.Y }, $"TransformPoint2D at w={alpha:G3}");
            AssertNear(3f / alpha, p.X, $"X at w={alpha:G3}");
            AssertNear(4f / alpha, p.Y, $"Y at w={alpha:G3}");
        }

        /// <summary>An ordinary affine transform keeps working: translation and scale in one matrix.</summary>
        [Fact]
        public void TransformPoint2D_StandardAffine_Regression()
        {
            var m = new CxMatrix3X3(new float[] { 2, 0, 5, 0, 3, -2, 0, 0, 1 });
            CxPoint2D p = m.TransformPoint2D(new CxPoint2D(3f, 4f));

            AssertAllFinite(new float[] { p.X, p.Y }, "TransformPoint2D affine");
            AssertNear(11f, p.X, "X of (2·3 + 5, 3·4 − 2)");
            AssertNear(10f, p.Y, "Y of (2·3 + 5, 3·4 − 2)");
        }

        /// <summary>
        /// The preserved policy for degenerate w: w == 0 and NaN leave the point undivided, while
        /// an infinite w still divides (which gives zero).
        /// </summary>
        [Fact]
        public void TransformPoint2D_DegenerateW_KeepsUndividedResult()
        {
            var zeroW = new CxMatrix3X3(new float[] { 1, 0, 3, 0, 1, 4, 0, 0, 0 });
            var nanW = new CxMatrix3X3(new float[] { 1, 0, 3, 0, 1, 4, float.NaN, 0, 0 });
            var infiniteW = new CxMatrix3X3(new float[] { 1, 0, 3, 0, 1, 4, float.PositiveInfinity, 0, 0 });

            CxPoint2D zero = zeroW.TransformPoint2D(new CxPoint2D(1f, 1f));
            AssertNear(4f, zero.X, "w = 0 keeps the undivided X");
            AssertNear(5f, zero.Y, "w = 0 keeps the undivided Y");

            CxPoint2D nan = nanW.TransformPoint2D(new CxPoint2D(1f, 1f));
            AssertNear(4f, nan.X, "w = NaN keeps the undivided X");
            AssertNear(5f, nan.Y, "w = NaN keeps the undivided Y");

            CxPoint2D infinite = infiniteW.TransformPoint2D(new CxPoint2D(1f, 1f));
            AssertNear(0f, infinite.X, "w = +∞ divides X to 0");
            AssertNear(0f, infinite.Y, "w = +∞ divides Y to 0");
        }

        // ── CxMatrix4X4.TransformPoint3D ────────────────────────────────────────

        /// <summary>
        /// <c>alpha · I</c> divides by a small positive or negative w, so the point comes back
        /// unchanged instead of being returned multiplied by alpha.
        /// </summary>
        [Theory]
        [InlineData(1e-7f)]
        [InlineData(-1e-7f)]
        [InlineData(1e-8f)]
        public void TransformPoint3D_SmallUniformScale_DividesBySmallW(float alpha)
        {
            CxMatrix4X4 m = ScaledIdentity4x4(alpha);
            CxPoint3D p = m.TransformPoint3D(new CxPoint3D(3f, 4f, 5f));

            AssertAllFinite(new float[] { p.X, p.Y, p.Z }, $"TransformPoint3D at alpha={alpha:G3}");
            AssertNear(3f, p.X, $"X at alpha={alpha:G3}");
            AssertNear(4f, p.Y, $"Y at alpha={alpha:G3}");
            AssertNear(5f, p.Z, $"Z at alpha={alpha:G3}");
        }

        /// <summary>
        /// Only the homogeneous w shrinks: the divide must still happen, giving the affine result
        /// scaled by 1/w (3e7 rather than 3).
        /// </summary>
        [Theory]
        [InlineData(1e-7f)]
        [InlineData(-1e-7f)]
        public void TransformPoint3D_OnlyWShrinks_DividesBySmallW(float alpha)
        {
            var m = new CxMatrix4X4(new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, alpha,
            });
            CxPoint3D p = m.TransformPoint3D(new CxPoint3D(3f, 4f, 5f));

            AssertAllFinite(new float[] { p.X, p.Y, p.Z }, $"TransformPoint3D at w={alpha:G3}");
            AssertNear(3f / alpha, p.X, $"X at w={alpha:G3}");
            AssertNear(4f / alpha, p.Y, $"Y at w={alpha:G3}");
            AssertNear(5f / alpha, p.Z, $"Z at w={alpha:G3}");
        }

        /// <summary>
        /// Ordinary perspective regression: with <c>w = 1 - z</c> the point (2, 3, 4) has
        /// <c>w = -3</c> and comes back divided by it.
        /// </summary>
        [Fact]
        public void TransformPoint3D_StandardPerspective_Regression()
        {
            var m = new CxMatrix4X4(new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, -1, 1,
            });
            CxPoint3D p = m.TransformPoint3D(new CxPoint3D(2f, 3f, 4f));

            AssertAllFinite(new float[] { p.X, p.Y, p.Z }, "TransformPoint3D perspective");
            AssertNear(2f / -3f, p.X, "X of (2, 3, 4) with w = -3");
            AssertNear(3f / -3f, p.Y, "Y of (2, 3, 4) with w = -3");
            AssertNear(4f / -3f, p.Z, "Z of (2, 3, 4) with w = -3");
        }

        /// <summary>
        /// The preserved policy for degenerate w: w == 0 and NaN leave the point undivided, while
        /// an infinite w still divides (which gives zero).
        /// </summary>
        [Fact]
        public void TransformPoint3D_DegenerateW_KeepsUndividedResult()
        {
            var zeroW = new CxMatrix4X4(new float[]
            {
                1, 0, 0, 3,
                0, 1, 0, 4,
                0, 0, 1, 5,
                0, 0, 0, 0,
            });
            var nanW = new CxMatrix4X4(new float[]
            {
                1, 0, 0, 3,
                0, 1, 0, 4,
                0, 0, 1, 5,
                0, 0, 0, float.NaN,
            });
            var infiniteW = new CxMatrix4X4(new float[]
            {
                1, 0, 0, 3,
                0, 1, 0, 4,
                0, 0, 1, 5,
                0, 0, 0, float.PositiveInfinity,
            });

            CxPoint3D zero = zeroW.TransformPoint3D(new CxPoint3D(1f, 1f, 1f));
            AssertNear(4f, zero.X, "w = 0 keeps the undivided X");
            AssertNear(5f, zero.Y, "w = 0 keeps the undivided Y");
            AssertNear(6f, zero.Z, "w = 0 keeps the undivided Z");

            CxPoint3D nan = nanW.TransformPoint3D(new CxPoint3D(1f, 1f, 1f));
            AssertNear(4f, nan.X, "w = NaN keeps the undivided X");
            AssertNear(5f, nan.Y, "w = NaN keeps the undivided Y");
            AssertNear(6f, nan.Z, "w = NaN keeps the undivided Z");

            CxPoint3D infinite = infiniteW.TransformPoint3D(new CxPoint3D(1f, 1f, 1f));
            AssertNear(0f, infinite.X, "w = +∞ divides X to 0");
            AssertNear(0f, infinite.Y, "w = +∞ divides Y to 0");
            AssertNear(0f, infinite.Z, "w = +∞ divides Z to 0");
        }

        // ── CxMatrix4X4.Solve3x3 ────────────────────────────────────────────────

        /// <summary>
        /// A uniformly scaled system with the prescribed solution x = (1, 2, 3): the determinant
        /// of 1e-3 · I is 1e-9, below the old absolute threshold, yet the system is perfectly
        /// conditioned.
        /// </summary>
        [Theory]
        [InlineData(1e-3f)]
        [InlineData(1e-6f)]
        [InlineData(1e6f)]
        public void Solve3x3_UniformScale_ReturnsPrescribedSolution(float scale)
        {
            var a = new float[] { scale, 0, 0, 0, scale, 0, 0, 0, scale };
            float[] b = ApplyBlock3x3(a, 1f, 2f, 3f);

            CxVector3D x = FromBlock3x3(a).Solve3x3(new CxVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new float[] { x.X, x.Y, x.Z }, $"Solve3x3 at scale {scale:G3}");
            AssertNear(1f, x.X, $"solution X at scale {scale:G3}");
            AssertNear(2f, x.Y, $"solution Y at scale {scale:G3}");
            AssertNear(3f, x.Z, $"solution Z at scale {scale:G3}");
        }

        /// <summary>The first column has no usable pivot, so the solve has to swap rows.</summary>
        [Fact]
        public void Solve3x3_RowSwapRequired_ReturnsPrescribedSolution()
        {
            var a = new float[] { 0, 2, 0, 3, 0, 0, 0, 0, 4 };
            float[] b = ApplyBlock3x3(a, 1f, 2f, 3f);

            CxVector3D x = FromBlock3x3(a).Solve3x3(new CxVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new float[] { x.X, x.Y, x.Z }, "Solve3x3 row swap");
            AssertNear(1f, x.X, "solution X");
            AssertNear(2f, x.Y, "solution Y");
            AssertNear(3f, x.Z, "solution Z");
        }

        /// <summary>An ordinary asymmetric system (determinant 25) solves to x = (1, 2, 3).</summary>
        [Fact]
        public void Solve3x3_OrdinaryAsymmetric_ReturnsPrescribedSolution()
        {
            var a = new float[] { 2, 1, 0, 0, 3, 1, 1, 0, 4 };
            float[] b = ApplyBlock3x3(a, 1f, 2f, 3f);

            CxVector3D x = FromBlock3x3(a).Solve3x3(new CxVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new float[] { x.X, x.Y, x.Z }, "Solve3x3 asymmetric");
            AssertNear(1f, x.X, "solution X");
            AssertNear(2f, x.Y, "solution Y");
            AssertNear(3f, x.Z, "solution Z");
        }

        /// <summary>
        /// A moderately ill-conditioned but perfectly invertible system (condition number
        /// ≈ 3/δ = 3000, determinant 1e-6 — exactly the smallest value the old absolute threshold
        /// accepted) must be solved, not rejected. The tolerance follows the conditioning bound
        /// (cond · eps ≈ 4e-4) rather than the accuracy of the algorithm.
        /// </summary>
        [Fact]
        public void Solve3x3_IllConditionedButInvertible_IsSolvedWithinConditioningBound()
        {
            const float delta = 1e-3f;
            var a = new float[] { 1, 1, 1, 1, 1 + delta, 1, 1, 1, 1 + delta };
            float[] b = ApplyBlock3x3(a, 1f, 2f, 3f);

            CxVector3D x = FromBlock3x3(a).Solve3x3(new CxVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new float[] { x.X, x.Y, x.Z }, "Solve3x3 ill-conditioned");
            AssertNear(1f, x.X, "solution X", 1e-3f);
            AssertNear(2f, x.Y, "solution Y", 1e-3f);
            AssertNear(3f, x.Z, "solution Z", 1e-3f);
        }

        /// <summary>
        /// A diagonal system whose rows span nine orders of magnitude: every pivot is compared
        /// against the scale of its own row and column, so the small rows are solved rather than
        /// rejected.
        /// </summary>
        [Fact]
        public void Solve3x3_MixedRowScales_ReturnsPrescribedSolution()
        {
            var a = new float[] { 1e-6f, 0f, 0f, 0f, 1e-2f, 0f, 0f, 0f, 1e3f };
            float[] b = ApplyBlock3x3(a, 1f, 2f, 3f);

            CxVector3D x = FromBlock3x3(a).Solve3x3(new CxVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new float[] { x.X, x.Y, x.Z }, "Solve3x3 mixed row scales");
            AssertNear(1f, x.X, "solution X");
            AssertNear(2f, x.Y, "solution Y");
            AssertNear(3f, x.Z, "solution Z");
        }

        /// <summary>
        /// The 3D counterpart of the coupled-row case: the same 1e40 elimination factor would
        /// overflow to infinity and poison the solution with NaN.
        /// </summary>
        [Fact]
        public void Solve3x3_WidelyScaledCoupledRows_ReturnsPrescribedSolution()
        {
            var a = new float[] { 1e-20f, 1e-20f, 0f, 1e20f, 2e20f, 0f, 0f, 0f, 1f };
            float[] b = ApplyBlock3x3(a, 1f, 2f, 3f);

            CxVector3D x = FromBlock3x3(a).Solve3x3(new CxVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new float[] { x.X, x.Y, x.Z }, "Solve3x3 coupled rows");
            AssertNear(1f, x.X, "solution X");
            AssertNear(2f, x.Y, "solution Y");
            AssertNear(3f, x.Z, "solution Z");
        }

        /// <summary>
        /// A singular 3×3 submatrix is rejected even though the surrounding 4×4 matrix is
        /// invertible, and a uniformly scaled singular submatrix stays rejected as well.
        /// </summary>
        [Fact]
        public void Solve3x3_SingularSubmatrix_Throws()
        {
            var singular = new float[] { 1, 2, 3, 2, 4, 6, 1, 0, 1 };
            var scaledSingular = new float[]
            {
                1e-4f, 2e-4f, 3e-4f,
                2e-4f, 4e-4f, 6e-4f,
                0f, 0f, 1e-4f,
            };

            Assert.Throws<InvalidOperationException>(
                () => { FromBlock3x3(singular).Solve3x3(new CxVector3D(1f, 2f, 3f)); });
            Assert.Throws<InvalidOperationException>(
                () => { FromBlock3x3(scaledSingular).Solve3x3(new CxVector3D(1f, 2f, 3f)); });
        }

        /// <summary>The translation column and the fourth row must not take part in the solve.</summary>
        [Fact]
        public void Solve3x3_IgnoresFourthRowAndColumn()
        {
            var a = new float[] { 2, 1, 0, 0, 3, 1, 1, 0, 4 };
            float[] b = ApplyBlock3x3(a, 1f, 2f, 3f);
            var m = new CxMatrix4X4(new float[]
            {
                2, 1, 0, 7,
                0, 3, 1, -3,
                1, 0, 4, 9,
                5, 6, 7, 8,
            });

            CxVector3D x = m.Solve3x3(new CxVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new float[] { x.X, x.Y, x.Z }, "Solve3x3 with translation");
            AssertNear(1f, x.X, "solution X");
            AssertNear(2f, x.Y, "solution Y");
            AssertNear(3f, x.Z, "solution Z");
        }
    }
}
