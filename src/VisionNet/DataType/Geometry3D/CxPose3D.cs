using System;
using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// Represents a 6-DOF pose: translation (X, Y, Z) and rotation (Roll, Pitch, Yaw) in radians.
    /// Rotation uses the intrinsic Z-Y-X convention: R = Rz(Yaw) * Ry(Pitch) * Rx(Roll).
    /// </summary>
    public struct CxPose3D
    {
        public float X;
        public float Y;
        public float Z;

        /// <summary>Rotation around the X axis, in radians.</summary>
        public float Roll;

        /// <summary>Rotation around the Y axis, in radians.</summary>
        public float Pitch;

        /// <summary>Rotation around the Z axis, in radians.</summary>
        public float Yaw;

        public CxPose3D(float x, float y, float z, float roll, float pitch, float yaw)
        {
            X = x; Y = y; Z = z;
            Roll = roll; Pitch = pitch; Yaw = yaw;
        }

        public CxPose3D(CxPoint3D position, float roll, float pitch, float yaw)
        {
            X = position.X; Y = position.Y; Z = position.Z;
            Roll = roll; Pitch = pitch; Yaw = yaw;
        }

        // ── Factory methods ─────────────────────────────────────────────────────

        /// <summary>Creates a pose from translation only (zero rotation).</summary>
        public static CxPose3D FromTranslation(float x, float y, float z) =>
            new CxPose3D(x, y, z, 0, 0, 0);

        /// <summary>Creates a pose from rotation only (zero translation).</summary>
        public static CxPose3D FromRotation(float roll, float pitch, float yaw) =>
            new CxPose3D(0, 0, 0, roll, pitch, yaw);

        // ── Conversion to / from CxMatrix4X4 ────────────────────────────────────

        /// <summary>
        /// Converts this pose to a 4×4 transformation matrix.
        /// M = Translation(X, Y, Z) * RotationZ(Yaw) * RotationY(Pitch) * RotationX(Roll)
        /// </summary>
        public CxMatrix4X4 ToMatrix() =>
            CxMatrix4X4.Translation(X, Y, Z)
          * CxMatrix4X4.RotationZ(Yaw)
          * CxMatrix4X4.RotationY(Pitch)
          * CxMatrix4X4.RotationX(Roll);

        /// <summary>
        /// Extracts a 6-DOF pose from a 4×4 transformation matrix.
        /// Translation is taken from the rightmost column;
        /// rotation (Roll, Pitch, Yaw) is extracted from the upper-left 3×3 submatrix
        /// using the Z-Y-X convention with gimbal-lock handling.
        /// </summary>
        public static CxPose3D FromMatrix(CxMatrix4X4 matrix)
        {
            float tx = matrix.Data[3];
            float ty = matrix.Data[7];
            float tz = matrix.Data[11];

            float a20 = matrix.Data[8];
            float pitch = (float)Math.Asin(-a20);

            float roll, yaw;
            if (Math.Abs(a20) < 1.0f - 1e-6f)
            {
                roll = (float)Math.Atan2(matrix.Data[9],  matrix.Data[10]);
                yaw  = (float)Math.Atan2(matrix.Data[4],  matrix.Data[0]);
            }
            else
            {
                roll = 0f;
                yaw  = (float)Math.Atan2(-matrix.Data[1], matrix.Data[5]);
            }

            return new CxPose3D(tx, ty, tz, roll, pitch, yaw);
        }

        // ── Accessors ───────────────────────────────────────────────────────────

        /// <summary>Gets the translation component as a point.</summary>
        public CxPoint3D Position => new CxPoint3D(X, Y, Z);

        /// <summary>Gets the rotation components as a vector (Roll, Pitch, Yaw).</summary>
        public CxVector3D Rotation => new CxVector3D(Roll, Pitch, Yaw);

        /// <summary>Returns the identity pose (origin, zero rotation).</summary>
        public static CxPose3D Identity => new CxPose3D(0, 0, 0, 0, 0, 0);

        // ── Object overrides ────────────────────────────────────────────────────

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxPose3D(X={0:G9}, Y={1:G9}, Z={2:G9}, Roll={3:G9}, Pitch={4:G9}, Yaw={5:G9})",
                X, Y, Z, Roll, Pitch, Yaw);
    }
}
