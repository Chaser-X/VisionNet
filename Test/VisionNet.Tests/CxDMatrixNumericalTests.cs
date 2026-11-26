using System;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Tests
{
    /// <summary>
    /// Double-precision counterpart of <see cref="CxMatrixNumericalTests"/>: regression tests for
    /// the numerical defects fixed in <see cref="CxDMatrix3X3"/> and <see cref="CxDMatrix4X4"/>
    /// (transposed <c>LookAt</c>, <c>RotationAxis</c> skipping normalisation of tiny axes,
    /// absolute determinant thresholds rejecting well-conditioned scaled systems, and the
    /// perspective divide being skipped for small finite <c>w</c>).
    /// Expected values come from analytic matrices, prescribed solutions and independent
    /// oracles — never from the production inverse.
    /// </summary>
    [Trait("Category", "NumericalAudit")]
    public class CxDMatrixNumericalTests
    {
        /// <summary>
        /// Relative tolerance for double-precision results, with an absolute floor of the same
        /// size near zero.
        /// </summary>
        private const double Tol = 1e-12;

        // ── Assertion helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Asserts <paramref name="actual"/> matches <paramref name="expected"/> within
        /// <paramref name="tol"/> relative, or absolutely when the expected value is near zero.
        /// </summary>
        private static void AssertNear(double expected, double actual, string what, double tol = Tol) =>
            Assert.True(Math.Abs(expected - actual) <= tol * Math.Max(1.0, Math.Abs(expected)),
                $"{what}: expected {expected:G17}, got {actual:G17} (tolerance {tol:G3} relative)");

        /// <summary>Fails when any element is not finite; always called before a tolerance check.</summary>
        private static void AssertAllFinite(double[] values, string what)
        {
            for (int i = 0; i < values.Length; i++)
                Assert.True(!double.IsNaN(values[i]) && !double.IsInfinity(values[i]),
                    $"{what}: element {i} is {values[i]}");
        }

        private static void AssertAllFinite(CxDMatrix3X3 m, string what) => AssertAllFinite(m.Data, what);

        private static void AssertAllFinite(CxDMatrix4X4 m, string what) => AssertAllFinite(m.Data, what);

        /// <summary>Asserts two 4×4 matrices agree element by element, after checking both are finite.</summary>
        private static void AssertMatrixNear(CxDMatrix4X4 expected, CxDMatrix4X4 actual, string what)
        {
            AssertAllFinite(expected, what + " (expected)");
            AssertAllFinite(actual, what);
            for (int i = 0; i < 16; i++)
                AssertNear(expected.Data[i], actual.Data[i], $"{what} [{i / 4},{i % 4}]");
        }

        /// <summary>Asserts a row-major 3×3 block is the identity within tolerance.</summary>
        private static void AssertIdentity3x3(double[] block, string what)
        {
            AssertAllFinite(block, what);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    AssertNear(i == j ? 1.0 : 0.0, block[i * 3 + j], $"{what} [{i},{j}]");
        }

        // ── Independent oracles and builders ────────────────────────────────────

        /// <summary>Copies the upper-left 3×3 block of a 4×4 matrix in row-major order.</summary>
        private static double[] Block3x3(CxDMatrix4X4 m) =>
            new double[]
            {
                m.Data[0], m.Data[1], m.Data[2],
                m.Data[4], m.Data[5], m.Data[6],
                m.Data[8], m.Data[9], m.Data[10],
            };

        /// <summary>Multiplies a row-major 3×3 block by a vector, without the production transform.</summary>
        private static double[] ApplyBlock3x3(double[] a, double x0, double x1, double x2) =>
            new double[]
            {
                a[0] * x0 + a[1] * x1 + a[2] * x2,
                a[3] * x0 + a[4] * x1 + a[5] * x2,
                a[6] * x0 + a[7] * x1 + a[8] * x2,
            };

        /// <summary>Multiplies two row-major 3×3 blocks, without the production operator.</summary>
        private static double[] MultiplyBlock3x3(double[] a, double[] b)
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
            return r;
        }

        /// <summary>Builds a 4×4 matrix from a row-major 3×3 block and an identity fourth row and column.</summary>
        private static CxDMatrix4X4 FromBlock3x3(double[] a) =>
            new CxDMatrix4X4(new double[]
            {
                a[0], a[1], a[2], 0,
                a[3], a[4], a[5], 0,
                a[6], a[7], a[8], 0,
                0, 0, 0, 1,
            });

        /// <summary>Builds a 3×3 affine matrix whose upper-left 2×2 block is given explicitly.</summary>
        private static CxDMatrix3X3 Affine2x2(double a00, double a01, double a10, double a11) =>
            new CxDMatrix3X3(new double[] { a00, a01, 0, a10, a11, 0, 0, 0, 1 });

        /// <summary>Builds <c>alpha · I</c> as a 4×4 matrix (note that <c>Scale</c> leaves <c>Data[15]</c> at 1).</summary>
        private static CxDMatrix4X4 ScaledIdentity4x4(double alpha) =>
            new CxDMatrix4X4(new double[]
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
        private static CxDMatrix4X4 Rodrigues(double kx, double ky, double kz, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad), t = 1.0 - c;
            return new CxDMatrix4X4(new double[]
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
        /// rather than the production <see cref="CxDVector3D"/> helpers.
        /// </summary>
        private static CxDMatrix4X4 ExpectedViewMatrix(
            double ex, double ey, double ez, double cx, double cy, double cz, double ux, double uy, double uz)
        {
            double fx = cx - ex, fy = cy - ey, fz = cz - ez;
            double fLen = Math.Sqrt(fx * fx + fy * fy + fz * fz);
            fx /= fLen; fy /= fLen; fz /= fLen;

            double sx = fy * uz - fz * uy;
            double sy = fz * ux - fx * uz;
            double sz = fx * uy - fy * ux;
            double sLen = Math.Sqrt(sx * sx + sy * sy + sz * sz);
            sx /= sLen; sy /= sLen; sz /= sLen;

            double vx = sy * fz - sz * fy;
            double vy = sz * fx - sx * fz;
            double vz = sx * fy - sy * fx;

            return new CxDMatrix4X4(new double[]
            {
                 sx,  sy,  sz, -(sx * ex + sy * ey + sz * ez),
                 vx,  vy,  vz, -(vx * ex + vy * ey + vz * ez),
                -fx, -fy, -fz,   fx * ex + fy * ey + fz * ez,
                 0,   0,   0,   1,
            });
        }

        /// <summary>Asserts the upper-left 3×3 block is a proper rotation: orthonormal rows, determinant +1.</summary>
        private static void AssertProperRotation(CxDMatrix4X4 m, string what)
        {
            double[] r = Block3x3(m);

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    double dot = r[i * 3] * r[j * 3] + r[i * 3 + 1] * r[j * 3 + 1] + r[i * 3 + 2] * r[j * 3 + 2];
                    AssertNear(i == j ? 1.0 : 0.0, dot, $"{what}: R·Rᵀ[{i},{j}]");
                }

            double det = r[0] * (r[4] * r[8] - r[5] * r[7])
                       - r[1] * (r[3] * r[8] - r[5] * r[6])
                       + r[2] * (r[3] * r[7] - r[4] * r[6]);
            AssertNear(1.0, det, $"{what}: det(R)");
        }

        // ── LookAt ──────────────────────────────────────────────────────────────

        /// <summary>
        /// A camera one unit to the side and ten units up the +Z axis maps itself to the origin
        /// and the world origin onto the negative Z axis, sqrt(101) away.
        /// </summary>
        [Fact]
        public void LookAt_EyeOnPositiveZ_MapsEyeToOriginAndTargetToNegativeZ()
        {
            var eye = new CxDPoint3D(1.0, 0.0, 10.0);
            var center = new CxDPoint3D(0.0, 0.0, 0.0);
            CxDMatrix4X4 m = CxDMatrix4X4.LookAt(eye, center, new CxDVector3D(0.0, 1.0, 0.0));

            AssertAllFinite(m, "LookAt(eye on +Z)");

            CxDPoint3D eyeImage = m.TransformPoint3D(eye);
            AssertNear(0.0, eyeImage.X, "eye image X");
            AssertNear(0.0, eyeImage.Y, "eye image Y");
            AssertNear(0.0, eyeImage.Z, "eye image Z");

            CxDPoint3D targetImage = m.TransformPoint3D(center);
            AssertNear(0.0, targetImage.X, "target image X");
            AssertNear(0.0, targetImage.Y, "target image Y");
            AssertNear(-Math.Sqrt(101.0), targetImage.Z, "target image Z");
        }

        /// <summary>
        /// The mirrored camera. With the transposed layout the world origin came back exactly at
        /// the camera position instead of on the negative Z axis.
        /// </summary>
        [Fact]
        public void LookAt_EyeOnNegativeZ_MapsWorldOriginToNegativeZ()
        {
            var eye = new CxDPoint3D(-1.0, 0.0, -10.0);
            var center = new CxDPoint3D(0.0, 0.0, 0.0);
            CxDMatrix4X4 m = CxDMatrix4X4.LookAt(eye, center, new CxDVector3D(0.0, 1.0, 0.0));

            AssertAllFinite(m, "LookAt(eye on -Z)");

            CxDPoint3D eyeImage = m.TransformPoint3D(eye);
            AssertNear(0.0, eyeImage.X, "eye image X");
            AssertNear(0.0, eyeImage.Y, "eye image Y");
            AssertNear(0.0, eyeImage.Z, "eye image Z");

            CxDPoint3D originImage = m.TransformPoint3D(center);
            AssertNear(0.0, originImage.X, "origin image X");
            AssertNear(0.0, originImage.Y, "origin image Y");
            AssertNear(-Math.Sqrt(101.0), originImage.Z, "origin image Z");
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
            var eye = new CxDPoint3D(3.0, 4.0, 10.0);
            var center = new CxDPoint3D(0.5, -1.0, 2.0);
            CxDMatrix4X4 m = CxDMatrix4X4.LookAt(eye, center, new CxDVector3D(0.0, 1.0, 0.0));
            CxDMatrix4X4 expected = ExpectedViewMatrix(3.0, 4.0, 10.0, 0.5, -1.0, 2.0, 0.0, 1.0, 0.0);

            AssertMatrixNear(expected, m, "LookAt tilted");
            AssertNear(0.0, m.Data[12], "last row [3,0]");
            AssertNear(0.0, m.Data[13], "last row [3,1]");
            AssertNear(0.0, m.Data[14], "last row [3,2]");
            AssertNear(1.0, m.Data[15], "last row [3,3]");
            AssertProperRotation(m, "LookAt tilted");

            // The translation column is -R · eye, so the camera position lands on the origin.
            for (int row = 0; row < 3; row++)
                AssertNear(0.0,
                    m.Data[row * 4] * eye.X + m.Data[row * 4 + 1] * eye.Y
                    + m.Data[row * 4 + 2] * eye.Z + m.Data[row * 4 + 3],
                    $"R·eye + t, row {row}");
        }

        // ── RotationAxis ────────────────────────────────────────────────────────

        /// <summary>
        /// The magnitude of the axis must not change the rotation: an axis scaled by 1e-13 — or by
        /// 1e300 — points the same way as an ordinary one. The old length threshold left such an
        /// axis unnormalised (or overflowed its length to infinity) and returned a matrix that is
        /// not a rotation at all.
        /// </summary>
        [Theory]
        [InlineData(1e-13)]
        [InlineData(1.0)]
        [InlineData(1e3)]
        [InlineData(1e300)]
        [InlineData(1e-200)]
        [InlineData(1e-250)]
        public void RotationAxis_AxisMagnitude_DoesNotChangeRotation(double scale)
        {
            CxDMatrix4X4 m = CxDMatrix4X4.RotationAxis(new CxDVector3D(0.6 * scale, 0.0, 0.8 * scale), 60.0);
            CxDMatrix4X4 expected = Rodrigues(0.6, 0.0, 0.8, 60.0);
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
            CxDMatrix4X4 m = CxDMatrix4X4.RotationAxis(new CxDVector3D(0.6, 0.0, 0.8), 60.0);
            double[] block = Block3x3(m);

            double[] axisImage = ApplyBlock3x3(block, 0.6, 0.0, 0.8);
            AssertAllFinite(axisImage, "axis image");
            AssertNear(0.6, axisImage[0], "axis image X");
            AssertNear(0.0, axisImage[1], "axis image Y");
            AssertNear(0.8, axisImage[2], "axis image Z");

            double[] perpendicularImage = ApplyBlock3x3(block, 0.8, 0.0, -0.6);
            AssertAllFinite(perpendicularImage, "perpendicular image");
            AssertNear(0.4, perpendicularImage[0], "perpendicular image X");
            AssertNear(Math.Sqrt(3.0) / 2.0, perpendicularImage[1], "perpendicular image Y");
            AssertNear(-0.3, perpendicularImage[2], "perpendicular image Z");
        }

        /// <summary>
        /// Compatibility of the undefined-input propagation: a NaN axis is left unnormalised, so the
        /// elements that do not mix the NaN component stay finite. Only the classification is pinned
        /// here - no NaN bit pattern is claimed - and the axis is an invalid input either way.
        /// </summary>
        [Fact]
        public void RotationAxis_NaNAxis_KeepsNonFinitePropagation()
        {
            CxDMatrix4X4 m = CxDMatrix4X4.RotationAxis(new CxDVector3D(double.NaN, 0.0, 0.0), 60.0);

            Assert.True(double.IsNaN(m.Data[0]), "element mixing the NaN component stays NaN");
            Assert.True(double.IsNaN(m.Data[9]), "element mixing the NaN component stays NaN");
            Assert.False(double.IsNaN(m.Data[5]), "element free of the NaN component stays finite");
            Assert.False(double.IsNaN(m.Data[10]), "element free of the NaN component stays finite");
            AssertNear(0.5, m.Data[5], "finite element tyy + cos60");
            AssertNear(0.5, m.Data[10], "finite element tzz + cos60");
            AssertNear(1.0, m.Data[15], "homogeneous corner");
            AssertNear(0.0, m.Data[12], "last row [3,0]");
        }

        /// <summary>
        /// The infinite axis takes the other legacy branch (its length exceeds the threshold), which
        /// again leaves the elements that avoid the infinite component finite. Same classification
        /// caveat as the NaN case.
        /// </summary>
        [Fact]
        public void RotationAxis_InfiniteAxis_KeepsNonFinitePropagation()
        {
            CxDMatrix4X4 m = CxDMatrix4X4.RotationAxis(new CxDVector3D(double.PositiveInfinity, 0.0, 0.0), 60.0);

            Assert.True(double.IsNaN(m.Data[0]), "element mixing the infinite component stays NaN");
            Assert.True(double.IsNaN(m.Data[9]), "element mixing the infinite component stays NaN");
            Assert.False(double.IsNaN(m.Data[5]), "element free of the infinite component stays finite");
            Assert.False(double.IsNaN(m.Data[10]), "element free of the infinite component stays finite");
            AssertNear(0.5, m.Data[5], "finite element tyy + cos60");
            AssertNear(0.5, m.Data[10], "finite element tzz + cos60");
            AssertNear(1.0, m.Data[15], "homogeneous corner");
            AssertNear(0.0, m.Data[12], "last row [3,0]");
        }

        // ── CxDMatrix3X3.Inverse ────────────────────────────────────────────────

        /// <summary>
        /// A uniform scale whose determinant is far below the old absolute threshold (1e-7 gives
        /// 1e-14) must still invert to the analytic reciprocal. The reciprocal is asserted
        /// scale-free so that a wrong result cannot hide behind the tolerance floor.
        /// </summary>
        [Theory]
        [InlineData(1e-7)]
        [InlineData(1e-3)]
        [InlineData(1.0)]
        [InlineData(1e30)]
        public void Matrix3X3_Inverse_UniformScale_MatchesAnalyticReciprocal(double scale)
        {
            CxDMatrix3X3 m = CxDMatrix3X3.Scale(scale, scale);
            CxDMatrix3X3 inverse = m.Inverse();
            string what = $"Scale({scale:G3}) inverse";

            AssertAllFinite(inverse, what);
            AssertNear(1.0, inverse.Data[0] * scale, what + " [0,0] · scale");
            AssertNear(1.0, inverse.Data[4] * scale, what + " [1,1] · scale");
            AssertNear(1.0, inverse.Data[8], what + " [2,2]");
            AssertIdentity3x3(MultiplyBlock3x3(m.Data, inverse.Data), what + " A·A⁻¹");
        }

        /// <summary>
        /// An ordinary asymmetric matrix with determinant 25 inverts to the analytic adjugate
        /// <c>[[0.48, -0.16, 0.04], [0.04, 0.32, -0.08], [-0.12, 0.04, 0.24]]</c>.
        /// </summary>
        [Fact]
        public void Matrix3X3_Inverse_OrdinaryAsymmetric_MatchesAnalyticAdjugate()
        {
            var m = new CxDMatrix3X3(new double[] { 2, 1, 0, 0, 3, 1, 1, 0, 4 });
            CxDMatrix3X3 inverse = m.Inverse();
            const string what = "inverse of [[2,1,0],[0,3,1],[1,0,4]]";

            AssertAllFinite(inverse, what);
            AssertNear(0.48, inverse.Data[0], what + " [0,0]");
            AssertNear(-0.16, inverse.Data[1], what + " [0,1]");
            AssertNear(0.04, inverse.Data[2], what + " [0,2]");
            AssertNear(0.04, inverse.Data[3], what + " [1,0]");
            AssertNear(0.32, inverse.Data[4], what + " [1,1]");
            AssertNear(-0.08, inverse.Data[5], what + " [1,2]");
            AssertNear(-0.12, inverse.Data[6], what + " [2,0]");
            AssertNear(0.04, inverse.Data[7], what + " [2,1]");
            AssertNear(0.24, inverse.Data[8], what + " [2,2]");
            AssertIdentity3x3(MultiplyBlock3x3(m.Data, inverse.Data), what + " A·A⁻¹");
        }

        /// <summary>
        /// A homogeneous row whose translation dwarfs its coefficients: the row scale is 1e6 while
        /// the pivot in that row is 1e-7, so a test that measured the pivot against the whole row
        /// would call this invertible matrix singular. The analytic inverse
        /// <c>[[1e7, 0, -1e13], [0, 1e7, 2e13], [0, 0, 1]]</c> is recovered, and a point round trip
        /// is deliberately not asserted: the 1e13-magnitude entries cancel against coordinates of
        /// the same size, so only the elementwise comparison is meaningful here.
        /// </summary>
        [Fact]
        public void Matrix3X3_Inverse_SmallScaleWithLargeTranslation_MatchesAnalyticInverse()
        {
            CxDMatrix3X3 m = CxDMatrix3X3.Translation(1e6, -2e6) * CxDMatrix3X3.Scale(1e-7, 1e-7);
            CxDMatrix3X3 inverse = m.Inverse();
            const string what = "inverse of T(1e6,-2e6) · Scale(1e-7)";

            AssertAllFinite(inverse, what);
            AssertNear(1e7, inverse.Data[0], what + " [0,0]");
            AssertNear(0.0, inverse.Data[1], what + " [0,1]");
            AssertNear(-1e13, inverse.Data[2], what + " [0,2]");
            AssertNear(0.0, inverse.Data[3], what + " [1,0]");
            AssertNear(1e7, inverse.Data[4], what + " [1,1]");
            AssertNear(2e13, inverse.Data[5], what + " [1,2]");
            AssertNear(0.0, inverse.Data[6], what + " [2,0]");
            AssertNear(0.0, inverse.Data[7], what + " [2,1]");
            AssertNear(1.0, inverse.Data[8], what + " [2,2]");
        }

        /// <summary>
        /// Genuinely singular matrices stay rejected: a row that is a multiple of another, a row
        /// of zeros, and the same rank deficiency after a uniform scaling down by 1e-13.
        /// </summary>
        [Fact]
        public void Matrix3X3_Inverse_SingularMatrices_Throw()
        {
            var dependentRows = new CxDMatrix3X3(new double[] { 1, 2, 3, 2, 4, 6, 0, 0, 1 });
            var zeroRow = new CxDMatrix3X3(new double[] { 1, 2, 3, 0, 0, 0, 0, 0, 1 });
            var scaledDependentRows = new CxDMatrix3X3(new double[]
            {
                1e-13, 2e-13, 3e-13,
                2e-13, 4e-13, 6e-13,
                0, 0, 1e-13,
            });

            Assert.Throws<InvalidOperationException>(() => { dependentRows.Inverse(); });
            Assert.Throws<InvalidOperationException>(() => { zeroRow.Inverse(); });
            Assert.Throws<InvalidOperationException>(() => { scaledDependentRows.Inverse(); });
        }

        // ── CxDMatrix3X3.Solve2x2 ───────────────────────────────────────────────

        /// <summary>An ordinary asymmetric 2×2 system solves to the prescribed x = (5, -2).</summary>
        [Fact]
        public void Solve2x2_OrdinaryAsymmetric_ReturnsPrescribedSolution()
        {
            CxDMatrix3X3 m = Affine2x2(3, 2, 1, 4);
            var b = new CxDVector2D(3 * 5 + 2 * -2, 1 * 5 + 4 * -2);

            CxDVector2D x = m.Solve2x2(b);

            AssertAllFinite(new double[] { x.X, x.Y }, "Solve2x2 solution");
            AssertNear(5.0, x.X, "solution X");
            AssertNear(-2.0, x.Y, "solution Y");
        }

        /// <summary>The first column has no usable pivot, so the solve has to swap rows.</summary>
        [Fact]
        public void Solve2x2_RowSwapRequired_ReturnsPrescribedSolution()
        {
            CxDMatrix3X3 m = Affine2x2(0, 2, 3, 0);
            var b = new CxDVector2D(0 * 5 + 2 * -2, 3 * 5 + 0 * -2);

            CxDVector2D x = m.Solve2x2(b);

            AssertAllFinite(new double[] { x.X, x.Y }, "Solve2x2 row swap");
            AssertNear(5.0, x.X, "solution X");
            AssertNear(-2.0, x.Y, "solution Y");
        }

        /// <summary>
        /// A uniformly small system is well conditioned: 1e-7 · I has determinant 1e-14, far below
        /// the old absolute threshold, yet its solution is exact.
        /// </summary>
        [Theory]
        [InlineData(1e-4)]
        [InlineData(1e-7)]
        [InlineData(1e-9)]
        public void Solve2x2_SmallUniformScale_ReturnsPrescribedSolution(double scale)
        {
            CxDMatrix3X3 m = Affine2x2(scale, 0, 0, scale);
            var b = new CxDVector2D(scale * 5, scale * -2);

            CxDVector2D x = m.Solve2x2(b);

            AssertAllFinite(new double[] { x.X, x.Y }, $"Solve2x2 at scale {scale:G3}");
            AssertNear(5.0, x.X, $"solution X at scale {scale:G3}");
            AssertNear(-2.0, x.Y, $"solution Y at scale {scale:G3}");
        }

        /// <summary>
        /// A diagonal system whose two rows differ by twelve orders of magnitude: the small row
        /// must not be treated as a zero row by the singularity test.
        /// </summary>
        [Fact]
        public void Solve2x2_MixedRowScales_ReturnsPrescribedSolution()
        {
            CxDMatrix3X3 m = Affine2x2(1e-8, 0, 0, 1e4);
            var b = new CxDVector2D(1e-8 * 5, 1e4 * -2);

            CxDVector2D x = m.Solve2x2(b);

            AssertAllFinite(new double[] { x.X, x.Y }, "Solve2x2 mixed row scales");
            AssertNear(5.0, x.X, "solution X");
            AssertNear(-2.0, x.Y, "solution Y");
        }

        /// <summary>
        /// Two rows coupled across a scale gap of 1e400: without equilibrating the equations the
        /// elimination factor is 1e400, which overflows to infinity and turns the whole solution into
        /// NaN. Scaling the rows - and the right-hand side with them - recovers the prescribed x.
        /// </summary>
        [Fact]
        public void Solve2x2_WidelyScaledCoupledRows_ReturnsPrescribedSolution()
        {
            CxDMatrix3X3 m = Affine2x2(1e-200, 1e-200, 1e200, 2e200);
            var b = new CxDVector2D(1e-200 * 1 + 1e-200 * 2, 1e200 * 1 + 2e200 * 2);

            CxDVector2D x = m.Solve2x2(b);

            AssertAllFinite(new double[] { x.X, x.Y }, "Solve2x2 coupled rows");
            AssertNear(1.0, x.X, "solution X");
            AssertNear(2.0, x.Y, "solution Y");
        }

        /// <summary>
        /// Boundary of the conditional equilibration: a row-scale ratio of 9 or 10 leaves the system
        /// untouched (the threshold is exceeded only above 10, so the equality case is unscaled) while
        /// 11 scales the rows. Both branches must return the prescribed solution - with scaled partial
        /// pivoting every elimination factor is bounded by the row-scale ratio, so the unscaled branch
        /// is just as accurate at these ratios.
        /// </summary>
        [Theory]
        [InlineData(9.0)]
        [InlineData(10.0)]
        [InlineData(11.0)]
        public void Solve2x2_RowScaleRatioAtEquilibrationThreshold_ReturnsPrescribedSolution(double ratio)
        {
            CxDMatrix3X3 m = Affine2x2(ratio, 0, 0, 1);
            var b = new CxDVector2D(ratio * 1, 1 * 2);

            CxDVector2D x = m.Solve2x2(b);

            AssertAllFinite(new double[] { x.X, x.Y }, $"Solve2x2 at row ratio {ratio:G3}");
            AssertNear(1.0, x.X, $"solution X at row ratio {ratio:G3}");
            AssertNear(2.0, x.Y, $"solution Y at row ratio {ratio:G3}");
        }

        /// <summary>The translation column and the third row must not take part in the solve.</summary>
        [Fact]
        public void Solve2x2_IgnoresTranslationAndThirdRow()
        {
            var m = new CxDMatrix3X3(new double[] { 3, 2, 7, 1, 4, -3, 5, 6, 2 });
            var b = new CxDVector2D(3 * 5 + 2 * -2, 1 * 5 + 4 * -2);

            CxDVector2D x = m.Solve2x2(b);

            AssertAllFinite(new double[] { x.X, x.Y }, "Solve2x2 with translation");
            AssertNear(5.0, x.X, "solution X");
            AssertNear(-2.0, x.Y, "solution Y");
        }

        /// <summary>A rank-deficient and an all-zero 2×2 submatrix must both be rejected.</summary>
        [Fact]
        public void Solve2x2_SingularSubmatrix_Throws()
        {
            CxDMatrix3X3 dependent = Affine2x2(2, 4, 1, 2);
            CxDMatrix3X3 zero = Affine2x2(0, 0, 0, 0);

            Assert.Throws<InvalidOperationException>(() => { dependent.Solve2x2(new CxDVector2D(1, 1)); });
            Assert.Throws<InvalidOperationException>(() => { zero.Solve2x2(new CxDVector2D(1, 1)); });
        }

        // ── CxDMatrix3X3.TransformPoint2D ───────────────────────────────────────

        /// <summary>
        /// <c>alpha · I</c> divides by a small positive or negative w, so the point comes back
        /// unchanged instead of being returned multiplied by alpha.
        /// </summary>
        [Theory]
        [InlineData(1e-13)]
        [InlineData(-1e-13)]
        [InlineData(1e-15)]
        public void TransformPoint2D_SmallUniformScale_DividesBySmallW(double alpha)
        {
            var m = new CxDMatrix3X3(new double[] { alpha, 0, 0, 0, alpha, 0, 0, 0, alpha });
            CxDPoint2D p = m.TransformPoint2D(new CxDPoint2D(3, 4));

            AssertAllFinite(new double[] { p.X, p.Y }, $"TransformPoint2D at alpha={alpha:G3}");
            AssertNear(3.0, p.X, $"X at alpha={alpha:G3}");
            AssertNear(4.0, p.Y, $"Y at alpha={alpha:G3}");
        }

        /// <summary>
        /// Only the homogeneous w shrinks: the divide must still happen, giving the affine result
        /// scaled by 1/w (3e13 rather than 3).
        /// </summary>
        [Theory]
        [InlineData(1e-13)]
        [InlineData(-1e-13)]
        public void TransformPoint2D_OnlyWShrinks_DividesBySmallW(double alpha)
        {
            var m = new CxDMatrix3X3(new double[] { 1, 0, 0, 0, 1, 0, 0, 0, alpha });
            CxDPoint2D p = m.TransformPoint2D(new CxDPoint2D(3, 4));

            AssertAllFinite(new double[] { p.X, p.Y }, $"TransformPoint2D at w={alpha:G3}");
            AssertNear(3.0 / alpha, p.X, $"X at w={alpha:G3}");
            AssertNear(4.0 / alpha, p.Y, $"Y at w={alpha:G3}");
        }

        /// <summary>An ordinary affine transform keeps working: translation and scale in one matrix.</summary>
        [Fact]
        public void TransformPoint2D_StandardAffine_Regression()
        {
            var m = new CxDMatrix3X3(new double[] { 2, 0, 5, 0, 3, -2, 0, 0, 1 });
            CxDPoint2D p = m.TransformPoint2D(new CxDPoint2D(3, 4));

            AssertAllFinite(new double[] { p.X, p.Y }, "TransformPoint2D affine");
            AssertNear(11.0, p.X, "X of (2·3 + 5, 3·4 − 2)");
            AssertNear(10.0, p.Y, "Y of (2·3 + 5, 3·4 − 2)");
        }

        /// <summary>
        /// The preserved policy for degenerate w: w == 0 and NaN leave the point undivided, while
        /// an infinite w still divides (which gives zero).
        /// </summary>
        [Fact]
        public void TransformPoint2D_DegenerateW_KeepsUndividedResult()
        {
            var zeroW = new CxDMatrix3X3(new double[] { 1, 0, 3, 0, 1, 4, 0, 0, 0 });
            var nanW = new CxDMatrix3X3(new double[] { 1, 0, 3, 0, 1, 4, double.NaN, 0, 0 });
            var infiniteW = new CxDMatrix3X3(new double[] { 1, 0, 3, 0, 1, 4, double.PositiveInfinity, 0, 0 });

            CxDPoint2D zero = zeroW.TransformPoint2D(new CxDPoint2D(1, 1));
            AssertNear(4.0, zero.X, "w = 0 keeps the undivided X");
            AssertNear(5.0, zero.Y, "w = 0 keeps the undivided Y");

            CxDPoint2D nan = nanW.TransformPoint2D(new CxDPoint2D(1, 1));
            AssertNear(4.0, nan.X, "w = NaN keeps the undivided X");
            AssertNear(5.0, nan.Y, "w = NaN keeps the undivided Y");

            CxDPoint2D infinite = infiniteW.TransformPoint2D(new CxDPoint2D(1, 1));
            AssertNear(0.0, infinite.X, "w = +∞ divides X to 0");
            AssertNear(0.0, infinite.Y, "w = +∞ divides Y to 0");
        }

        // ── CxDMatrix4X4.TransformPoint3D ───────────────────────────────────────

        /// <summary>
        /// <c>alpha · I</c> divides by a small positive or negative w, so the point comes back
        /// unchanged instead of being returned multiplied by alpha.
        /// </summary>
        [Theory]
        [InlineData(1e-13)]
        [InlineData(-1e-13)]
        [InlineData(1e-15)]
        public void TransformPoint3D_SmallUniformScale_DividesBySmallW(double alpha)
        {
            CxDMatrix4X4 m = ScaledIdentity4x4(alpha);
            CxDPoint3D p = m.TransformPoint3D(new CxDPoint3D(3, 4, 5));

            AssertAllFinite(new double[] { p.X, p.Y, p.Z }, $"TransformPoint3D at alpha={alpha:G3}");
            AssertNear(3.0, p.X, $"X at alpha={alpha:G3}");
            AssertNear(4.0, p.Y, $"Y at alpha={alpha:G3}");
            AssertNear(5.0, p.Z, $"Z at alpha={alpha:G3}");
        }

        /// <summary>
        /// Only the homogeneous w shrinks: the divide must still happen, giving the affine result
        /// scaled by 1/w (3e13 rather than 3).
        /// </summary>
        [Theory]
        [InlineData(1e-13)]
        [InlineData(-1e-13)]
        public void TransformPoint3D_OnlyWShrinks_DividesBySmallW(double alpha)
        {
            var m = new CxDMatrix4X4(new double[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, alpha,
            });
            CxDPoint3D p = m.TransformPoint3D(new CxDPoint3D(3, 4, 5));

            AssertAllFinite(new double[] { p.X, p.Y, p.Z }, $"TransformPoint3D at w={alpha:G3}");
            AssertNear(3.0 / alpha, p.X, $"X at w={alpha:G3}");
            AssertNear(4.0 / alpha, p.Y, $"Y at w={alpha:G3}");
            AssertNear(5.0 / alpha, p.Z, $"Z at w={alpha:G3}");
        }

        /// <summary>
        /// Ordinary perspective regression: with <c>w = 1 - z</c> the point (2, 3, 4) has
        /// <c>w = -3</c> and comes back divided by it.
        /// </summary>
        [Fact]
        public void TransformPoint3D_StandardPerspective_Regression()
        {
            var m = new CxDMatrix4X4(new double[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, -1, 1,
            });
            CxDPoint3D p = m.TransformPoint3D(new CxDPoint3D(2, 3, 4));

            AssertAllFinite(new double[] { p.X, p.Y, p.Z }, "TransformPoint3D perspective");
            AssertNear(2.0 / -3.0, p.X, "X of (2, 3, 4) with w = -3");
            AssertNear(3.0 / -3.0, p.Y, "Y of (2, 3, 4) with w = -3");
            AssertNear(4.0 / -3.0, p.Z, "Z of (2, 3, 4) with w = -3");
        }

        /// <summary>
        /// The preserved policy for degenerate w: w == 0 and NaN leave the point undivided, while
        /// an infinite w still divides (which gives zero).
        /// </summary>
        [Fact]
        public void TransformPoint3D_DegenerateW_KeepsUndividedResult()
        {
            var zeroW = new CxDMatrix4X4(new double[]
            {
                1, 0, 0, 3,
                0, 1, 0, 4,
                0, 0, 1, 5,
                0, 0, 0, 0,
            });
            var nanW = new CxDMatrix4X4(new double[]
            {
                1, 0, 0, 3,
                0, 1, 0, 4,
                0, 0, 1, 5,
                0, 0, 0, double.NaN,
            });
            var infiniteW = new CxDMatrix4X4(new double[]
            {
                1, 0, 0, 3,
                0, 1, 0, 4,
                0, 0, 1, 5,
                0, 0, 0, double.PositiveInfinity,
            });

            CxDPoint3D zero = zeroW.TransformPoint3D(new CxDPoint3D(1, 1, 1));
            AssertNear(4.0, zero.X, "w = 0 keeps the undivided X");
            AssertNear(5.0, zero.Y, "w = 0 keeps the undivided Y");
            AssertNear(6.0, zero.Z, "w = 0 keeps the undivided Z");

            CxDPoint3D nan = nanW.TransformPoint3D(new CxDPoint3D(1, 1, 1));
            AssertNear(4.0, nan.X, "w = NaN keeps the undivided X");
            AssertNear(5.0, nan.Y, "w = NaN keeps the undivided Y");
            AssertNear(6.0, nan.Z, "w = NaN keeps the undivided Z");

            CxDPoint3D infinite = infiniteW.TransformPoint3D(new CxDPoint3D(1, 1, 1));
            AssertNear(0.0, infinite.X, "w = +∞ divides X to 0");
            AssertNear(0.0, infinite.Y, "w = +∞ divides Y to 0");
            AssertNear(0.0, infinite.Z, "w = +∞ divides Z to 0");
        }

        // ── CxDMatrix4X4.Solve3x3 ───────────────────────────────────────────────

        /// <summary>
        /// A uniformly scaled system with the prescribed solution x = (1, 2, 3): the determinant
        /// of 1e-5 · I is 1e-15, below the old absolute threshold, yet the system is perfectly
        /// conditioned.
        /// </summary>
        [Theory]
        [InlineData(1e-5)]
        [InlineData(1e-9)]
        [InlineData(1e6)]
        public void Solve3x3_UniformScale_ReturnsPrescribedSolution(double scale)
        {
            var a = new double[] { scale, 0, 0, 0, scale, 0, 0, 0, scale };
            double[] b = ApplyBlock3x3(a, 1, 2, 3);

            CxDVector3D x = FromBlock3x3(a).Solve3x3(new CxDVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new double[] { x.X, x.Y, x.Z }, $"Solve3x3 at scale {scale:G3}");
            AssertNear(1.0, x.X, $"solution X at scale {scale:G3}");
            AssertNear(2.0, x.Y, $"solution Y at scale {scale:G3}");
            AssertNear(3.0, x.Z, $"solution Z at scale {scale:G3}");
        }

        /// <summary>The first column has no usable pivot, so the solve has to swap rows.</summary>
        [Fact]
        public void Solve3x3_RowSwapRequired_ReturnsPrescribedSolution()
        {
            var a = new double[] { 0, 2, 0, 3, 0, 0, 0, 0, 4 };
            double[] b = ApplyBlock3x3(a, 1, 2, 3);

            CxDVector3D x = FromBlock3x3(a).Solve3x3(new CxDVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new double[] { x.X, x.Y, x.Z }, "Solve3x3 row swap");
            AssertNear(1.0, x.X, "solution X");
            AssertNear(2.0, x.Y, "solution Y");
            AssertNear(3.0, x.Z, "solution Z");
        }

        /// <summary>An ordinary asymmetric system (determinant 25) solves to x = (1, 2, 3).</summary>
        [Fact]
        public void Solve3x3_OrdinaryAsymmetric_ReturnsPrescribedSolution()
        {
            var a = new double[] { 2, 1, 0, 0, 3, 1, 1, 0, 4 };
            double[] b = ApplyBlock3x3(a, 1, 2, 3);

            CxDVector3D x = FromBlock3x3(a).Solve3x3(new CxDVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new double[] { x.X, x.Y, x.Z }, "Solve3x3 asymmetric");
            AssertNear(1.0, x.X, "solution X");
            AssertNear(2.0, x.Y, "solution Y");
            AssertNear(3.0, x.Z, "solution Z");
        }

        /// <summary>
        /// A moderately ill-conditioned but perfectly invertible system (condition number
        /// ≈ 3/δ = 3e7, determinant 1e-14 — below the old absolute threshold) must be solved, not
        /// rejected. The tolerance follows the conditioning bound (cond · eps ≈ 7e-9) rather than
        /// the accuracy of the algorithm.
        /// </summary>
        [Fact]
        public void Solve3x3_IllConditionedButInvertible_IsSolvedWithinConditioningBound()
        {
            const double delta = 1e-7;
            var a = new double[] { 1, 1, 1, 1, 1 + delta, 1, 1, 1, 1 + delta };
            double[] b = ApplyBlock3x3(a, 1, 2, 3);

            CxDVector3D x = FromBlock3x3(a).Solve3x3(new CxDVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new double[] { x.X, x.Y, x.Z }, "Solve3x3 ill-conditioned");
            AssertNear(1.0, x.X, "solution X", 1e-7);
            AssertNear(2.0, x.Y, "solution Y", 1e-7);
            AssertNear(3.0, x.Z, "solution Z", 1e-7);
        }

        /// <summary>
        /// A diagonal system whose rows span fourteen orders of magnitude: every pivot is compared
        /// against the scale of its own row and column, so the small rows are solved rather than
        /// rejected.
        /// </summary>
        [Fact]
        public void Solve3x3_MixedRowScales_ReturnsPrescribedSolution()
        {
            var a = new double[] { 1e-9, 0, 0, 0, 1e-3, 0, 0, 0, 1e5 };
            double[] b = ApplyBlock3x3(a, 1, 2, 3);

            CxDVector3D x = FromBlock3x3(a).Solve3x3(new CxDVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new double[] { x.X, x.Y, x.Z }, "Solve3x3 mixed row scales");
            AssertNear(1.0, x.X, "solution X");
            AssertNear(2.0, x.Y, "solution Y");
            AssertNear(3.0, x.Z, "solution Z");
        }

        /// <summary>
        /// The 3D counterpart of the coupled-row case: the same 1e400 elimination factor would
        /// overflow to infinity and poison the solution with NaN.
        /// </summary>
        [Fact]
        public void Solve3x3_WidelyScaledCoupledRows_ReturnsPrescribedSolution()
        {
            var a = new double[] { 1e-200, 1e-200, 0, 1e200, 2e200, 0, 0, 0, 1 };
            double[] b = ApplyBlock3x3(a, 1, 2, 3);

            CxDVector3D x = FromBlock3x3(a).Solve3x3(new CxDVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new double[] { x.X, x.Y, x.Z }, "Solve3x3 coupled rows");
            AssertNear(1.0, x.X, "solution X");
            AssertNear(2.0, x.Y, "solution Y");
            AssertNear(3.0, x.Z, "solution Z");
        }

        /// <summary>
        /// A singular 3×3 submatrix is rejected even though the surrounding 4×4 matrix is
        /// invertible, and a uniformly scaled singular submatrix stays rejected as well.
        /// </summary>
        [Fact]
        public void Solve3x3_SingularSubmatrix_Throws()
        {
            var singular = new double[] { 1, 2, 3, 2, 4, 6, 1, 0, 1 };
            var scaledSingular = new double[]
            {
                1e-6, 2e-6, 3e-6,
                2e-6, 4e-6, 6e-6,
                0, 0, 1e-6,
            };

            Assert.Throws<InvalidOperationException>(
                () => { FromBlock3x3(singular).Solve3x3(new CxDVector3D(1, 2, 3)); });
            Assert.Throws<InvalidOperationException>(
                () => { FromBlock3x3(scaledSingular).Solve3x3(new CxDVector3D(1, 2, 3)); });
        }

        /// <summary>The translation column and the fourth row must not take part in the solve.</summary>
        [Fact]
        public void Solve3x3_IgnoresFourthRowAndColumn()
        {
            var a = new double[] { 2, 1, 0, 0, 3, 1, 1, 0, 4 };
            double[] b = ApplyBlock3x3(a, 1, 2, 3);
            var m = new CxDMatrix4X4(new double[]
            {
                2, 1, 0, 7,
                0, 3, 1, -3,
                1, 0, 4, 9,
                5, 6, 7, 8,
            });

            CxDVector3D x = m.Solve3x3(new CxDVector3D(b[0], b[1], b[2]));

            AssertAllFinite(new double[] { x.X, x.Y, x.Z }, "Solve3x3 with translation");
            AssertNear(1.0, x.X, "solution X");
            AssertNear(2.0, x.Y, "solution Y");
            AssertNear(3.0, x.Z, "solution Z");
        }
    }
}
