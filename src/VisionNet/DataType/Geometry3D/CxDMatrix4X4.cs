using System;
using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 4×4 double-precision floating-point matrix stored in <b>row-major</b> order.
    /// Element at row <c>i</c>, column <c>j</c> is accessed as <c>Data[i * 4 + j]</c>.
    /// Double-precision counterpart of <see cref="CxMatrix4X4"/>.
    /// <para>
    /// Point/vector transforms use the row-major convention <c>M · v</c>
    /// (see <see cref="TransformPoint3D"/>). When uploading <see cref="Data"/> to OpenGL/GLSL
    /// shaders, pass <c>transpose = true</c> so GLSL receives the equivalent column-major matrix.
    /// </para>
    /// </summary>
    public class CxDMatrix4X4
    {
        /// <summary>Numerical tolerance used for perspective divide and singularity checks.</summary>
        private const double Epsilon = 1e-12;

        /// <summary>Initialises a 4×4 zero matrix.</summary>
        public CxDMatrix4X4() { }

        /// <summary>
        /// Initialises the matrix from a 16-element row-major array.
        /// </summary>
        /// <param name="data">16-element array in row-major order.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="data"/> does not contain exactly 16 elements.
        /// </exception>
        public CxDMatrix4X4(double[] data)
        {
            if (data.Length != 16)
                throw new ArgumentException("Matrix data must contain exactly 16 elements.", nameof(data));
            Data = data;
        }

        /// <summary>
        /// Initialises the matrix from a 4x4 two-dimensional array in row-major order.
        /// </summary>
        /// <param name="data">A 4x4 two-dimensional array in row-major order.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="data"/> is not a 4x4 two-dimensional array.
        /// </exception>
        public CxDMatrix4X4(double[,] data)
        {
            if (data.Rank != 2 || data.GetLength(0) != 4 || data.GetLength(1) != 4)
                throw new ArgumentException("Matrix data must be a 4x4 two-dimensional array.", nameof(data));
            Data = new double[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    Data[i * 4 + j] = data[i, j];
        }

        /// <summary>
        /// Gets or sets the 16 matrix elements in row-major order
        /// (<c>Data[i * 4 + j]</c> = element at row <c>i</c>, column <c>j</c>).
        /// </summary>
        public double[] Data { get; set; } = new double[16];

        /// <summary>
        /// Gets the 16 matrix elements as a 4x4 two-dimensional array in row-major order.
        /// </summary>
        public double[,] Data2D
        {
            get
            {
                var result = new double[4, 4];
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        result[i, j] = Data[i * 4 + j];
                return result;
            }
        }

        // ── Factory methods ──────────────────────────────────────────────────────

        /// <summary>Returns a new 4×4 identity matrix.</summary>
        public static CxDMatrix4X4 Identity() =>
            new CxDMatrix4X4(new double[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1,
            });

        /// <summary>
        /// Creates a translation matrix that moves points by (<paramref name="x"/>,
        /// <paramref name="y"/>, <paramref name="z"/>).
        /// </summary>
        public static CxDMatrix4X4 Translation(double x, double y, double z) =>
            new CxDMatrix4X4(new double[]
            {
                1, 0, 0, x,
                0, 1, 0, y,
                0, 0, 1, z,
                0, 0, 0, 1,
            });

        /// <summary>
        /// Creates a rotation matrix around the X axis by <paramref name="angle"/> degrees.
        /// </summary>
        public static CxDMatrix4X4 RotationX(double angle)
        {
            double c = Math.Cos(angle * Math.PI / 180.0);
            double s = Math.Sin(angle * Math.PI / 180.0);
            return new CxDMatrix4X4(new double[]
            {
                1,  0, 0, 0,
                0,  c,-s, 0,
                0,  s, c, 0,
                0,  0, 0, 1,
            });
        }

        /// <summary>
        /// Creates a rotation matrix around the Y axis by <paramref name="angle"/> degrees.
        /// </summary>
        public static CxDMatrix4X4 RotationY(double angle)
        {
            double c = Math.Cos(angle * Math.PI / 180.0);
            double s = Math.Sin(angle * Math.PI / 180.0);
            return new CxDMatrix4X4(new double[]
            {
                 c, 0, s, 0,
                 0, 1, 0, 0,
                -s, 0, c, 0,
                 0, 0, 0, 1,
            });
        }

        /// <summary>
        /// Creates a rotation matrix around the Z axis by <paramref name="angle"/> degrees.
        /// </summary>
        public static CxDMatrix4X4 RotationZ(double angle)
        {
            double c = Math.Cos(angle * Math.PI / 180.0);
            double s = Math.Sin(angle * Math.PI / 180.0);
            return new CxDMatrix4X4(new double[]
            {
                c, -s, 0, 0,
                s,  c, 0, 0,
                0,  0, 1, 0,
                0,  0, 0, 1,
            });
        }

        /// <summary>
        /// Creates a rotation matrix around an arbitrary unit axis by <paramref name="angle"/> degrees
        /// using Rodrigues' rotation formula.
        /// </summary>
        /// <param name="axis">Rotation axis (will be normalized to unit length).</param>
        /// <param name="angle">Rotation angle in degrees.</param>
        public static CxDMatrix4X4 RotationAxis(CxDVector3D axis, double angle)
        {
            double kx = axis.X, ky = axis.Y, kz = axis.Z;
            double len = Math.Sqrt(kx * kx + ky * ky + kz * kz);
            if (len > Epsilon) { kx /= len; ky /= len; kz /= len; }

            double rad = angle * Math.PI / 180.0;
            double c = Math.Cos(rad);
            double s = Math.Sin(rad);
            double t = 1.0 - c;

            double txx = t * kx * kx;
            double txy = t * kx * ky;
            double txz = t * kx * kz;
            double tyy = t * ky * ky;
            double tyz = t * ky * kz;
            double tzz = t * kz * kz;

            double sxz = s * kz;
            double sxy = s * ky;
            double syx = s * kx;

            return new CxDMatrix4X4(new double[]
            {
                txx + c,    txy - sxz,  txz + sxy,  0,
                txy + sxz,  tyy + c,    tyz - syx,  0,
                txz - sxy,  tyz + syx,  tzz + c,    0,
                0,          0,          0,          1,
            });
        }

        /// <summary>
        /// Creates a non-uniform scale matrix with independent factors along each axis.
        /// </summary>
        public static CxDMatrix4X4 Scale(double x, double y, double z) =>
            new CxDMatrix4X4(new double[]
            {
                x, 0, 0, 0,
                0, y, 0, 0,
                0, 0, z, 0,
                0, 0, 0, 1,
            });

        /// <summary>
        /// Creates a view matrix that positions the camera at <paramref name="eye"/>,
        /// looking toward <paramref name="center"/>, with <paramref name="up"/> as the
        /// world up-direction.
        /// </summary>
        /// <param name="eye">Camera position in world space.</param>
        /// <param name="center">Target point the camera looks at.</param>
        /// <param name="up">World up-direction vector.</param>
        public static CxDMatrix4X4 LookAt(CxDPoint3D eye, CxDPoint3D center, CxDVector3D up)
        {
            var f = new CxDVector3D(center.X - eye.X, center.Y - eye.Y, center.Z - eye.Z).Normalize();
            var s = f.Cross(up).Normalize();
            var u = s.Cross(f);
            var eyeVec = new CxDVector3D(eye.X, eye.Y, eye.Z);
            return new CxDMatrix4X4(new double[]
            {
                 s.X,  u.X, -f.X, 0,
                 s.Y,  u.Y, -f.Y, 0,
                 s.Z,  u.Z, -f.Z, 0,
                -s.Dot(eyeVec), -u.Dot(eyeVec), f.Dot(eyeVec), 1,
            });
        }

        // ── Operators & transformations ──────────────────────────────────────────

        /// <summary>
        /// Returns the matrix product <c>m1 × m2</c> (standard row-major matrix multiplication).
        /// </summary>
        public static CxDMatrix4X4 operator *(CxDMatrix4X4 m1, CxDMatrix4X4 m2)
        {
            var r = new double[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    r[i * 4 + j] = m1.Data[i * 4 + 0] * m2.Data[0 * 4 + j]
                                  + m1.Data[i * 4 + 1] * m2.Data[1 * 4 + j]
                                  + m1.Data[i * 4 + 2] * m2.Data[2 * 4 + j]
                                  + m1.Data[i * 4 + 3] * m2.Data[3 * 4 + j];
            return new CxDMatrix4X4(r);
        }

        /// <summary>
        /// Returns the transpose of this matrix (rows become columns).
        /// </summary>
        public CxDMatrix4X4 Transpose()
        {
            var r = new double[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    r[j * 4 + i] = Data[i * 4 + j];
            return new CxDMatrix4X4(r);
        }

        /// <summary>
        /// Computes the inverse of this matrix using Gauss–Jordan elimination
        /// with partial pivoting.
        /// </summary>
        /// <returns>The inverse matrix.</returns>
        /// <remarks>
        /// The computation operates on a copy of <see cref="Data"/>; the original matrix
        /// is not modified. For singular (non-invertible) matrices the result is undefined.
        /// </remarks>
        public CxDMatrix4X4 Inverse()
        {
            double[] m   = new double[16];
            double[] inv = new double[16];
            Array.Copy(Data, m, 16);

            // Start with the identity as the augmented right-hand side.
            for (int i = 0; i < 4; i++) inv[i * 4 + i] = 1.0;

            // Forward elimination with partial pivoting.
            for (int i = 0; i < 4; i++)
            {
                // Find the pivot row.
                double maxVal = Math.Abs(m[i * 4 + i]);
                int    maxRow = i;
                for (int j = i + 1; j < 4; j++)
                {
                    if (Math.Abs(m[j * 4 + i]) > maxVal)
                    {
                        maxVal = Math.Abs(m[j * 4 + i]);
                        maxRow = j;
                    }
                }

                // Swap rows i and maxRow in both m and inv.
                if (maxRow != i)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        double tmp        = m[i * 4 + j];
                        m[i * 4 + j]      = m[maxRow * 4 + j];
                        m[maxRow * 4 + j] = tmp;

                        tmp                 = inv[i * 4 + j];
                        inv[i * 4 + j]      = inv[maxRow * 4 + j];
                        inv[maxRow * 4 + j] = tmp;
                    }
                }

                // Eliminate column i below the pivot.
                for (int j = i + 1; j < 4; j++)
                {
                    double factor = m[j * 4 + i] / m[i * 4 + i];
                    for (int k = i; k < 4; k++) m[j * 4 + k]   -= factor * m[i * 4 + k];
                    for (int k = 0; k < 4; k++) inv[j * 4 + k] -= factor * inv[i * 4 + k];
                }
            }

            // Back substitution.
            for (int i = 3; i >= 0; i--)
            {
                for (int j = 0; j < 4; j++)
                {
                    double sum = 0;
                    for (int k = i + 1; k < 4; k++) sum += m[i * 4 + k] * inv[k * 4 + j];
                    inv[i * 4 + j] = (inv[i * 4 + j] - sum) / m[i * 4 + i];
                }
            }

            return new CxDMatrix4X4(inv);
        }

        /// <summary>
        /// Transforms a 3D point by this matrix using row-major convention (column vector on the right).
        /// Performs perspective divide when the homogeneous <c>w</c> component is non-zero.
        /// </summary>
        /// <param name="point">The 3D point to transform.</param>
        /// <returns>The transformed 3D point.</returns>
        public CxDPoint3D TransformPoint3D(CxDPoint3D point)
        {
            double x = point.X, y = point.Y, z = point.Z;
            double tx = Data[0] * x + Data[1] * y + Data[2]  * z + Data[3];
            double ty = Data[4] * x + Data[5] * y + Data[6]  * z + Data[7];
            double tz = Data[8] * x + Data[9] * y + Data[10] * z + Data[11];
            double tw = Data[12]* x + Data[13]* y + Data[14] * z + Data[15];
            if (Math.Abs(tw) > Epsilon) { tx /= tw; ty /= tw; tz /= tw; }
            return new CxDPoint3D(tx, ty, tz);
        }

        /// <summary>
        /// Transforms a 3D vector by the upper-left 3x3 submatrix (rotation + scale).
        /// Vectors are not affected by translation or perspective.
        /// </summary>
        /// <param name="vector">The 3D vector to transform.</param>
        /// <returns>The transformed 3D vector.</returns>
        public CxDVector3D TransformVector3D(CxDVector3D vector)
        {
            double x = vector.X, y = vector.Y, z = vector.Z;
            return new CxDVector3D(
                Data[0] * x + Data[1] * y + Data[2]  * z,
                Data[4] * x + Data[5] * y + Data[6]  * z,
                Data[8] * x + Data[9] * y + Data[10] * z);
        }

        /// <summary>
        /// Solves the 3×3 linear system <c>A·x = b</c> using Cramer's rule,
        /// where <c>A</c> is the upper-left 3×3 submatrix of this 4×4 matrix.
        /// </summary>
        /// <param name="b">The right-hand side vector.</param>
        /// <returns>The solution vector <c>x</c>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the determinant of the 3×3 submatrix is zero (within a tolerance of 1e-12).
        /// </exception>
        public CxDVector3D Solve3x3(CxDVector3D b)
        {
            double a00 = Data[0], a01 = Data[1], a02 = Data[2];
            double a10 = Data[4], a11 = Data[5], a12 = Data[6];
            double a20 = Data[8], a21 = Data[9], a22 = Data[10];
            double b0 = b.X, b1 = b.Y, b2 = b.Z;

            double c11a22 = a11 * a22, c12a21 = a12 * a21;
            double c10a22 = a10 * a22, c12a20 = a12 * a20;
            double c10a21 = a10 * a21, c11a20 = a11 * a20;

            double det = a00 * (c11a22 - c12a21)
                       - a01 * (c10a22 - c12a20)
                       + a02 * (c10a21 - c11a20);

            if (Math.Abs(det) < Epsilon)
                throw new InvalidOperationException(
                    "The 3×3 submatrix is singular; Cramer's rule cannot be applied.");

            double detX = b0 * (c11a22 - c12a21)
                        - a01 * (b1 * a22 - a12 * b2)
                        + a02 * (b1 * a21 - a11 * b2);

            double detY = a00 * (b1 * a22 - a12 * b2)
                        - b0 * (c10a22 - c12a20)
                        + a02 * (a10 * b2 - b1 * a20);

            double detZ = a00 * (a11 * b2 - b1 * a21)
                        - a01 * (a10 * b2 - b1 * a20)
                        + b0 * (c10a21 - c11a20);

            return new CxDVector3D(detX / det, detY / det, detZ / det);
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDMatrix4X4([{0:G17}, {1:G17}, {2:G17}, {3:G17}, {4:G17}, {5:G17}, {6:G17}, {7:G17}, {8:G17}, {9:G17}, {10:G17}, {11:G17}, {12:G17}, {13:G17}, {14:G17}, {15:G17}])",
                Data[0], Data[1], Data[2], Data[3], Data[4], Data[5], Data[6], Data[7],
                Data[8], Data[9], Data[10], Data[11], Data[12], Data[13], Data[14], Data[15]);

        /// <summary>Converts a single-precision matrix to double precision (lossless).</summary>
        public static implicit operator CxDMatrix4X4(CxMatrix4X4 m)
        {
            if (m == null) return null;
            var d = new double[16];
            for (int i = 0; i < 16; i++) d[i] = m.Data[i];
            return new CxDMatrix4X4(d);
        }

        /// <summary>Converts this double-precision matrix to single precision (may lose precision).</summary>
        public static explicit operator CxMatrix4X4(CxDMatrix4X4 m)
        {
            if (m == null) return null;
            var f = new float[16];
            for (int i = 0; i < 16; i++) f[i] = (float)m.Data[i];
            return new CxMatrix4X4(f);
        }
    }
}
