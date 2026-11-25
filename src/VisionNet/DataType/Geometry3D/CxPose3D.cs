using System;
using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 6-DOF pose: translation (X, Y, Z) and rotation (Rx, Ry, Rz) in degrees.
    /// Rotation uses the extrinsic Z-Y-X convention: R = Rz * Ry * Rx.
    /// </summary>
    public struct CxPose3D
    {
        public float X;
        public float Y;
        public float Z;

        /// <summary>Rotation around the X axis, in degrees.</summary>
        public float Rx;

        /// <summary>Rotation around the Y axis, in degrees.</summary>
        public float Ry;

        /// <summary>Rotation around the Z axis, in degrees.</summary>
        public float Rz;

        public CxPose3D(float x, float y, float z, float rx, float ry, float rz)
        {
            X = x; Y = y; Z = z;
            Rx = rx; Ry = ry; Rz = rz;
        }

        public CxPose3D(CxPoint3D position, float rx, float ry, float rz)
        {
            X = position.X; Y = position.Y; Z = position.Z;
            Rx = rx; Ry = ry; Rz = rz;
        }

        // ── Factory methods ─────────────────────────────────────────────────────

        /// <summary>Creates a pose from translation only (zero rotation).</summary>
        public static CxPose3D FromTranslation(float x, float y, float z) =>
            new CxPose3D(x, y, z, 0, 0, 0);

        /// <summary>Creates a pose from rotation only (zero translation).</summary>
        public static CxPose3D FromRotation(float rx, float ry, float rz) =>
            new CxPose3D(0, 0, 0, rx, ry, rz);

        // ── Conversion to / from CxMatrix4X4 ────────────────────────────────────

        /// <summary>
        /// Converts this pose to a 4×4 transformation matrix.
        /// M = Translation(X, Y, Z) * RotationZ(Rz) * RotationY(Ry) * RotationX(Rx)
        /// </summary>
        public CxMatrix4X4 ToMatrix() =>
            CxMatrix4X4.Translation(X, Y, Z)
          * CxMatrix4X4.RotationZ(Rz)
          * CxMatrix4X4.RotationY(Ry)
          * CxMatrix4X4.RotationX(Rx);

        /// <summary>
        /// Extracts a 6-DOF pose from a 4×4 transformation matrix.
        /// Translation is taken from the rightmost column;
        /// rotation (Rx, Ry, Rz) is extracted from the upper-left 3×3 submatrix
        /// using the Z-Y-X convention, in degrees.
        /// <para>
        /// The gimbal-lock branch is taken only when <c>|cos(Ry)| ≤ 1e-6</c>, where
        /// <c>Rx</c> is genuinely unobservable and is pinned to 0. Poses that are merely
        /// <i>close</i> to lock keep all three angles.
        /// </para>
        /// </summary>
        public static CxPose3D FromMatrix(CxMatrix4X4 matrix)
        {
            float[] d = matrix.Data;

            // Row-major; translation lives in the last column.
            float x = d[3], y = d[7], z = d[11];

            // Compute in double and take |cos(Ry)| straight from the first column.
            // Not sqrt(1 - a20^2): near the lock a20 has already saturated to ±1, so that
            // subtraction cancels catastrophically, while the first column still carries
            // cos(Ry) at full resolution.
            double cy = Math.Sqrt(
                (double)d[0] * d[0] +
                (double)d[4] * d[4]);

            // Avoids Asin amplifying float quantisation error as its input approaches ±1.
            double ry = Math.Atan2(-(double)d[8], cy);
            double rx, rz;

            // True gimbal lock is cos(Ry) == 0, so the threshold must be tested on the cosine
            // scale: cy > 1e-6 degenerates only within 5.7e-5 degrees of 90. The old
            // |a20| >= 0.999999 misclassified every pose with Ry >= 89.92 deg as locked.
            if (cy > 1e-6)
            {
                // Z-Y-X: R = Rz * Ry * Rx.
                rx = Math.Atan2(d[9], d[10]);
                rz = Math.Atan2(d[4], d[0]);
            }
            else
            {
                // Right at the lock, fall back to the equivalent representation with Rx = 0.
                rx = 0.0;
                rz = Math.Atan2(-(double)d[1], d[5]);
            }

            const double RadToDeg = 180.0 / Math.PI;
            return new CxPose3D(
                x, y, z,
                (float)(rx * RadToDeg),
                (float)(ry * RadToDeg),
                (float)(rz * RadToDeg));
        }

        // ── Accessors ───────────────────────────────────────────────────────────

        /// <summary>Gets the translation component as a point.</summary>
        public CxPoint3D Position => new CxPoint3D(X, Y, Z);

        /// <summary>Gets the rotation components as a vector (Rx, Ry, Rz).</summary>
        public CxVector3D Rotation => new CxVector3D(Rx, Ry, Rz);

        /// <summary>Returns the identity pose (origin, zero rotation).</summary>
        public static CxPose3D Identity => new CxPose3D(0, 0, 0, 0, 0, 0);

        // ── Object overrides ────────────────────────────────────────────────────

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxPose3D(X={0:G9}, Y={1:G9}, Z={2:G9}, Rx={3:G9}, Ry={4:G9}, Rz={5:G9})",
                X, Y, Z, Rx, Ry, Rz);
    }
}
