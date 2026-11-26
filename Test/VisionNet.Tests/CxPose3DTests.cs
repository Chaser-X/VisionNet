using System;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Tests
{
    /// <summary>
    /// Regression tests for the Z-Y-X Euler extraction in <see cref="CxPose3D.FromMatrix"/>.
    /// <para>
    /// Two defects were fixed together:
    /// (1) the gimbal-lock branch triggered at <c>|a20| ≥ 1 - 1e-6</c> — i.e. <c>Ry ≥ 89.92°</c> —
    /// pinning <c>Rx</c> to 0 for poses that are not actually locked;
    /// (2) <c>Ry</c> was recovered with <c>Asin</c>, which saturates near the lock because
    /// <c>sin(Ry)</c> rounds to exactly 1 in float, while <c>cos(Ry)</c> keeps full resolution.
    /// </para>
    /// </summary>
    public class CxPose3DTests
    {
        private const float DegTol = 1e-4f;

        /// <summary>Builds <c>Rz(rz) · Ry(ry) · Rx(rx)</c>, matching <see cref="CxPose3D.ToMatrix"/>.</summary>
        private static CxMatrix4X4 PoseMatrix(float rx, float ry, float rz) =>
            CxMatrix4X4.RotationZ(rz) * CxMatrix4X4.RotationY(ry) * CxMatrix4X4.RotationX(rx);

        /// <summary>Distance, in mm, between the images of one probe point under two matrices.</summary>
        private static float PointError(CxMatrix4X4 a, CxMatrix4X4 b, float x, float y, float z)
        {
            CxPoint3D pa = a.TransformPoint3D(new CxPoint3D(x, y, z));
            CxPoint3D pb = b.TransformPoint3D(new CxPoint3D(x, y, z));
            float dx = pa.X - pb.X, dy = pa.Y - pb.Y, dz = pa.Z - pb.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static float MaxElementError(CxMatrix4X4 a, CxMatrix4X4 b)
        {
            float max = 0f;
            for (int i = 0; i < 16; i++)
            {
                float e = Math.Abs(a.Data[i] - b.Data[i]);
                if (e > max) max = e;
            }
            return max;
        }

        private static void AssertNear(float expected, float actual, float tol, string what) =>
            Assert.True(Math.Abs(expected - actual) <= tol,
                $"{what}: expected {expected:G9}, got {actual:G9} (tolerance {tol:G3})");

        // ── Near gimbal lock ────────────────────────────────────────────────────

        /// <summary>
        /// The core regression: a 500 mm probe point must survive a matrix → pose → matrix
        /// round trip. The old threshold gave up to 0.98 mm of error here; tightening the
        /// threshold alone still left ~0.09 mm because <c>Asin</c> saturates.
        /// </summary>
        [Theory]
        [InlineData(90f)]
        [InlineData(89.9999f)]
        [InlineData(89.999f)]
        [InlineData(89.99f)]
        [InlineData(89.95f)]
        [InlineData(89.9f)]
        [InlineData(-89.99f)]
        [InlineData(-90f)]
        public void FromMatrix_NearGimbalLockSweep_ReconstructsPoint(float ry)
        {
            CxMatrix4X4 m = PoseMatrix(30f, ry, 0.08f);
            CxMatrix4X4 roundTrip = CxPose3D.FromMatrix(m).ToMatrix();

            Assert.True(PointError(m, roundTrip, 500f, 0f, 0f) < 1e-3f,
                $"Ry={ry}: point error {PointError(m, roundTrip, 500f, 0f, 0f):G6} mm at a 500 mm radius");
        }

        /// <summary>
        /// The composite that failed in the field: <c>P · A = RotationY(90°) · RotationZ(0.08°)</c>,
        /// whose <c>Data[8]</c> is <c>-0.999999046</c>. The old code took the lock branch and
        /// returned <c>Rx = Rz = 0</c>, which back-computed guidance points ~0.98 mm off.
        /// <para>
        /// <c>RotationY(90f)</c> evaluates <c>90f * (float)Math.PI / 180f</c> in float, so its
        /// cosine is <c>-4.32e-8</c> rather than 0 — the exact decomposition of this matrix is
        /// therefore <c>Rx = Rz = 90.0018°</c>, which is what the extraction returns.
        /// </para>
        /// </summary>
        [Fact]
        public void FromMatrix_ReportedComposite_RecoversAllThreeAngles()
        {
            CxMatrix4X4 m = CxMatrix4X4.RotationY(90f) * CxMatrix4X4.RotationZ(0.08f);

            CxPose3D pose = CxPose3D.FromMatrix(m);

            // The regression is Rx/Rz collapsing to 0, so ~0.01° is ample separation.
            AssertNear(90f, pose.Rx, 1e-2f, "Rx");
            AssertNear(89.92f, pose.Ry, 1e-3f, "Ry");
            AssertNear(90f, pose.Rz, 1e-2f, "Rz");
            Assert.True(PointError(m, pose.ToMatrix(), 500f, 0f, 0f) < 0.01f,
                "point error at a 500 mm radius");
        }

        // ── Exact gimbal lock ───────────────────────────────────────────────────

        /// <summary>
        /// At exact lock only <c>Rx - Rz</c> (or <c>Rx + Rz</c>, for <c>Ry = -90</c>) is
        /// observable, so the individual angles are not recoverable — but the matrix is.
        /// This guards the lock branch itself against the tightened threshold.
        /// </summary>
        [Theory]
        [InlineData(90f)]
        [InlineData(-90f)]
        public void FromMatrix_ExactGimbalLock_ReconstructsMatrix(float ry)
        {
            CxMatrix4X4 m = PoseMatrix(10f, ry, 30f);
            CxMatrix4X4 roundTrip = CxPose3D.FromMatrix(m).ToMatrix();

            AssertNear(ry, CxPose3D.FromMatrix(m).Ry, DegTol, "Ry");
            Assert.True(MaxElementError(m, roundTrip) < 1e-6f,
                $"Ry={ry}: max element error {MaxElementError(m, roundTrip):G6}");
        }

        // ── Ordinary poses ──────────────────────────────────────────────────────

        [Theory]
        [InlineData(1f, 2f, 3f, 10f, 20f, 30f)]
        [InlineData(0f, 0f, 0f, 0f, 0f, 0f)]
        [InlineData(-5f, 7f, 2f, -45f, 60f, -120f)]
        [InlineData(100f, 0f, 0f, 0f, 0f, 0f)]
        [InlineData(0f, 0f, 0f, 90f, 45f, -90f)]
        [InlineData(0f, 0f, 0f, -30f, -89f, 15f)]
        public void FromMatrix_GenericPoses_RoundTripsToPose(
            float x, float y, float z, float rx, float ry, float rz)
        {
            var pose = new CxPose3D(x, y, z, rx, ry, rz);

            CxPose3D back = CxPose3D.FromMatrix(pose.ToMatrix());

            AssertNear(x, back.X, 1e-3f, "X");
            AssertNear(y, back.Y, 1e-3f, "Y");
            AssertNear(z, back.Z, 1e-3f, "Z");
            AssertNear(rx, back.Rx, DegTol, "Rx");
            AssertNear(ry, back.Ry, DegTol, "Ry");
            AssertNear(rz, back.Rz, DegTol, "Rz");
        }

        // ── Robustness ──────────────────────────────────────────────────────────

        /// <summary>
        /// Composing a matrix in float can push <c>|a20|</c> marginally past 1. <c>Atan2</c> has
        /// no domain restriction, so the extraction must stay finite without an explicit clamp.
        /// </summary>
        [Fact]
        public void FromMatrix_A20OutOfRange_DoesNotProduceNaN()
        {
            CxMatrix4X4 m = PoseMatrix(0f, 89.99f, 0f);
            m.Data[8] = -1.0000001f;

            CxPose3D pose = CxPose3D.FromMatrix(m);

            Assert.False(
                float.IsNaN(pose.X) || float.IsNaN(pose.Y) || float.IsNaN(pose.Z) ||
                float.IsNaN(pose.Rx) || float.IsNaN(pose.Ry) || float.IsNaN(pose.Rz),
                "extraction produced NaN");
            AssertNear(89.99f, pose.Ry, 1e-2f, "Ry");
        }
    }
}
