using System;
using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 6-DOF pose: translation (X, Y, Z) and rotation (Rx, Ry, Rz) in degrees,
    /// in double precision. Rotation uses the extrinsic Z-Y-X convention: R = Rz * Ry * Rx.
    /// Double-precision counterpart of <see cref="CxPose3D"/>.
    /// </summary>
    public struct CxDPose3D
    {
        public double X;
        public double Y;
        public double Z;

        /// <summary>Rotation around the X axis, in degrees.</summary>
        public double Rx;

        /// <summary>Rotation around the Y axis, in degrees.</summary>
        public double Ry;

        /// <summary>Rotation around the Z axis, in degrees.</summary>
        public double Rz;

        public CxDPose3D(double x, double y, double z, double rx, double ry, double rz)
        {
            X = x; Y = y; Z = z;
            Rx = rx; Ry = ry; Rz = rz;
        }

        public CxDPose3D(CxDPoint3D position, double rx, double ry, double rz)
        {
            X = position.X; Y = position.Y; Z = position.Z;
            Rx = rx; Ry = ry; Rz = rz;
        }

        // ── Factory methods ─────────────────────────────────────────────────────

        /// <summary>Creates a pose from translation only (zero rotation).</summary>
        public static CxDPose3D FromTranslation(double x, double y, double z) =>
            new CxDPose3D(x, y, z, 0, 0, 0);

        /// <summary>Creates a pose from rotation only (zero translation).</summary>
        public static CxDPose3D FromRotation(double rx, double ry, double rz) =>
            new CxDPose3D(0, 0, 0, rx, ry, rz);

        // ── Conversion to / from CxDMatrix4X4 ───────────────────────────────────

        /// <summary>
        /// Converts this pose to a 4×4 transformation matrix.
        /// M = Translation(X, Y, Z) * RotationZ(Rz) * RotationY(Ry) * RotationX(Rx)
        /// </summary>
        public CxDMatrix4X4 ToMatrix() =>
            CxDMatrix4X4.Translation(X, Y, Z)
          * CxDMatrix4X4.RotationZ(Rz)
          * CxDMatrix4X4.RotationY(Ry)
          * CxDMatrix4X4.RotationX(Rx);

        /// <summary>
        /// Extracts a 6-DOF pose from a 4×4 transformation matrix.
        /// Translation is taken from the rightmost column;
        /// rotation (Rx, Ry, Rz) is extracted from the upper-left 3×3 submatrix
        /// using the Z-Y-X convention, in degrees.
        /// <para>
        /// The gimbal-lock branch is taken only when <c>|cos(Ry)| ≤ 1e-9</c>, where
        /// <c>Rx</c> is genuinely unobservable and is pinned to 0. Poses that are merely
        /// <i>close</i> to lock keep all three angles.
        /// </para>
        /// </summary>
        public static CxDPose3D FromMatrix(CxDMatrix4X4 matrix)
        {
            double[] d = matrix.Data;

            // Row-major; translation lives in the last column.
            double x = d[3], y = d[7], z = d[11];

            // |cos(Ry)| straight from the first column: near the lock d[8] has already
            // saturated to ±1, so sqrt(1 - d[8]^2) would cancel catastrophically, while the
            // first column keeps full resolution.
            double cy = Math.Sqrt(d[0] * d[0] + d[4] * d[4]);

            // Avoids Asin amplifying quantisation error as its input approaches ±1.
            double ry = Math.Atan2(-d[8], cy);
            double rx, rz;

            // True gimbal lock is cos(Ry) == 0, tested on the cosine scale. Double's balance
            // point sits about four orders of magnitude lower than float's, hence 1e-9.
            if (cy > 1e-9)
            {
                // Z-Y-X: R = Rz * Ry * Rx.
                rx = Math.Atan2(d[9], d[10]);
                rz = Math.Atan2(d[4], d[0]);
            }
            else
            {
                // Right at the lock, fall back to the equivalent representation with Rx = 0.
                rx = 0.0;
                rz = Math.Atan2(-d[1], d[5]);
            }

            const double RadToDeg = 180.0 / Math.PI;
            return new CxDPose3D(
                x, y, z,
                rx * RadToDeg,
                ry * RadToDeg,
                rz * RadToDeg);
        }

        // ── Accessors ───────────────────────────────────────────────────────────

        /// <summary>Gets the translation component as a point.</summary>
        public CxDPoint3D Position => new CxDPoint3D(X, Y, Z);

        /// <summary>Gets the rotation components as a vector (Rx, Ry, Rz).</summary>
        public CxDVector3D Rotation => new CxDVector3D(Rx, Ry, Rz);

        /// <summary>Returns the identity pose (origin, zero rotation).</summary>
        public static CxDPose3D Identity => new CxDPose3D(0, 0, 0, 0, 0, 0);

        // ── Object overrides ────────────────────────────────────────────────────

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDPose3D(X={0:G17}, Y={1:G17}, Z={2:G17}, Rx={3:G17}, Ry={4:G17}, Rz={5:G17})",
                X, Y, Z, Rx, Ry, Rz);

        /// <summary>Converts a single-precision pose to double precision (lossless).</summary>
        public static implicit operator CxDPose3D(CxPose3D p) =>
            new CxDPose3D(p.X, p.Y, p.Z, p.Rx, p.Ry, p.Rz);

        /// <summary>Converts this double-precision pose to single precision (may lose precision).</summary>
        public static explicit operator CxPose3D(CxDPose3D p) =>
            new CxPose3D((float)p.X, (float)p.Y, (float)p.Z, (float)p.Rx, (float)p.Ry, (float)p.Rz);
    }
}
