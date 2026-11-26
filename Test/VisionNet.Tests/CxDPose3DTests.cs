using System;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Tests
{
    /// <summary>
    /// Double-precision counterpart of <see cref="CxPose3DTests"/>; the two types are meant to
    /// stay API- and behaviour-aligned, so the same scenarios are exercised with tighter tolerances.
    /// </summary>
    public class CxDPose3DTests
    {
        private const double DegTol = 1e-9;

        /// <summary>Builds <c>Rz(rz) · Ry(ry) · Rx(rx)</c>, matching <see cref="CxDPose3D.ToMatrix"/>.</summary>
        private static CxDMatrix4X4 PoseMatrix(double rx, double ry, double rz) =>
            CxDMatrix4X4.RotationZ(rz) * CxDMatrix4X4.RotationY(ry) * CxDMatrix4X4.RotationX(rx);

        private static double PointError(CxDMatrix4X4 a, CxDMatrix4X4 b, double x, double y, double z)
        {
            CxDPoint3D pa = a.TransformPoint3D(new CxDPoint3D(x, y, z));
            CxDPoint3D pb = b.TransformPoint3D(new CxDPoint3D(x, y, z));
            double dx = pa.X - pb.X, dy = pa.Y - pb.Y, dz = pa.Z - pb.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double MaxElementError(CxDMatrix4X4 a, CxDMatrix4X4 b)
        {
            double max = 0.0;
            for (int i = 0; i < 16; i++)
            {
                double e = Math.Abs(a.Data[i] - b.Data[i]);
                if (e > max) max = e;
            }
            return max;
        }

        private static void AssertNear(double expected, double actual, double tol, string what) =>
            Assert.True(Math.Abs(expected - actual) <= tol,
                $"{what}: expected {expected:G17}, got {actual:G17} (tolerance {tol:G3})");

        [Theory]
        [InlineData(90.0)]
        [InlineData(89.99999999)]
        [InlineData(89.999999)]
        [InlineData(89.9999)]
        [InlineData(89.99)]
        [InlineData(89.9)]
        [InlineData(-89.99)]
        [InlineData(-90.0)]
        public void FromMatrix_NearGimbalLockSweep_ReconstructsPoint(double ry)
        {
            CxDMatrix4X4 m = PoseMatrix(30.0, ry, 0.08);
            CxDMatrix4X4 roundTrip = CxDPose3D.FromMatrix(m).ToMatrix();

            double err = PointError(m, roundTrip, 500.0, 0.0, 0.0);
            Assert.True(err < 1e-6, $"Ry={ry}: point error {err:G6} mm at a 500 mm radius");
        }

        [Fact]
        public void FromMatrix_ReportedComposite_RecoversAllThreeAngles()
        {
            CxDMatrix4X4 m = CxDMatrix4X4.RotationY(90.0) * CxDMatrix4X4.RotationZ(0.08);

            CxDPose3D pose = CxDPose3D.FromMatrix(m);

            AssertNear(90.0, pose.Rx, 1e-9, "Rx");
            AssertNear(89.92, pose.Ry, 1e-9, "Ry");
            AssertNear(90.0, pose.Rz, 1e-9, "Rz");
            Assert.True(PointError(m, pose.ToMatrix(), 500.0, 0.0, 0.0) < 1e-6,
                "point error at a 500 mm radius");
        }

        [Theory]
        [InlineData(90.0)]
        [InlineData(-90.0)]
        public void FromMatrix_ExactGimbalLock_ReconstructsMatrix(double ry)
        {
            CxDMatrix4X4 m = PoseMatrix(10.0, ry, 30.0);
            CxDMatrix4X4 roundTrip = CxDPose3D.FromMatrix(m).ToMatrix();

            AssertNear(ry, CxDPose3D.FromMatrix(m).Ry, DegTol, "Ry");
            Assert.True(MaxElementError(m, roundTrip) < 1e-12,
                $"Ry={ry}: max element error {MaxElementError(m, roundTrip):G6}");
        }

        [Theory]
        [InlineData(1.0, 2.0, 3.0, 10.0, 20.0, 30.0)]
        [InlineData(0.0, 0.0, 0.0, 0.0, 0.0, 0.0)]
        [InlineData(-5.0, 7.0, 2.0, -45.0, 60.0, -120.0)]
        [InlineData(100.0, 0.0, 0.0, 0.0, 0.0, 0.0)]
        [InlineData(0.0, 0.0, 0.0, 90.0, 45.0, -90.0)]
        [InlineData(0.0, 0.0, 0.0, -30.0, -89.0, 15.0)]
        public void FromMatrix_GenericPoses_RoundTripsToPose(
            double x, double y, double z, double rx, double ry, double rz)
        {
            var pose = new CxDPose3D(x, y, z, rx, ry, rz);

            CxDPose3D back = CxDPose3D.FromMatrix(pose.ToMatrix());

            AssertNear(x, back.X, 1e-9, "X");
            AssertNear(y, back.Y, 1e-9, "Y");
            AssertNear(z, back.Z, 1e-9, "Z");
            AssertNear(rx, back.Rx, DegTol, "Rx");
            AssertNear(ry, back.Ry, DegTol, "Ry");
            AssertNear(rz, back.Rz, DegTol, "Rz");
        }

        [Fact]
        public void FromMatrix_A20OutOfRange_DoesNotProduceNaN()
        {
            CxDMatrix4X4 m = PoseMatrix(0.0, 89.99, 0.0);
            m.Data[8] = -1.0000000000000002;

            CxDPose3D pose = CxDPose3D.FromMatrix(m);

            Assert.False(
                double.IsNaN(pose.X) || double.IsNaN(pose.Y) || double.IsNaN(pose.Z) ||
                double.IsNaN(pose.Rx) || double.IsNaN(pose.Ry) || double.IsNaN(pose.Rz),
                "extraction produced NaN");
            AssertNear(89.99, pose.Ry, 1e-6, "Ry");
        }
    }
}
