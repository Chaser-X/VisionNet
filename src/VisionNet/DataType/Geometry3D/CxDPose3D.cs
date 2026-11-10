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
        /// using the Z-Y-X convention with gimbal-lock handling, in degrees.
        /// </summary>
        public static CxDPose3D FromMatrix(CxDMatrix4X4 matrix)
        {
            double tx = matrix.Data[3];
            double ty = matrix.Data[7];
            double tz = matrix.Data[11];

            double a20 = matrix.Data[8];
            double ry = Math.Asin(-a20);

            double rx, rz;
            if (Math.Abs(a20) < 1.0 - 1e-12)
            {
                rx = Math.Atan2(matrix.Data[9],  matrix.Data[10]);
                rz = Math.Atan2(matrix.Data[4],  matrix.Data[0]);
            }
            else
            {
                rx = 0;
                rz = Math.Atan2(-matrix.Data[1], matrix.Data[5]);
            }

            const double rad2deg = 180.0 / Math.PI;
            return new CxDPose3D(tx, ty, tz, rx * rad2deg, ry * rad2deg, rz * rad2deg);
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
