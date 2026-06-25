using System;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 4×4 single-precision floating-point matrix stored in <b>row-major</b> order.
    /// Element at row <c>i</c>, column <c>j</c> is accessed as <c>Data[i * 4 + j]</c>.
    /// <para>
    /// Note: OpenGL and <see cref="VisionOperator.TransformPoint3D"/> use <b>column-major</b>
    /// convention. When passing a <see cref="CxMatrix4X4"/> to those APIs, transpose first
    /// or construct the matrix accordingly.
    /// </para>
    /// </summary>
    public class CxMatrix4X4
    {
        /// <summary>Initialises a 4×4 zero matrix.</summary>
        public CxMatrix4X4() { }

        /// <summary>
        /// Initialises the matrix from a 16-element row-major array.
        /// </summary>
        /// <param name="data">16-element array in row-major order.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="data"/> does not contain exactly 16 elements.
        /// </exception>
        public CxMatrix4X4(float[] data)
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
        public CxMatrix4X4(float[,] data)
        {
            if (data.Rank != 2 || data.GetLength(0) != 4 || data.GetLength(1) != 4)
                throw new ArgumentException("Matrix data must be a 4x4 two-dimensional array.", nameof(data));
            Data = new float[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    Data[i * 4 + j] = data[i, j];
        }

        /// <summary>
        /// Gets or sets the 16 matrix elements in row-major order
        /// (<c>Data[i * 4 + j]</c> = element at row <c>i</c>, column <c>j</c>).
        /// </summary>
        public float[] Data { get; set; } = new float[16];

        /// <summary>
        /// Gets the 16 matrix elements as a 4x4 two-dimensional array in row-major order.
        /// </summary>
        public float[,] Data2D
        {
            get
            {
                var result = new float[4, 4];
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        result[i, j] = Data[i * 4 + j];
                return result;
            }
        }

        // ── Factory methods ──────────────────────────────────────────────────────

        /// <summary>Returns a new 4×4 identity matrix.</summary>
        public static CxMatrix4X4 Identity() =>
            new CxMatrix4X4(new float[]
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
        public static CxMatrix4X4 Translation(float x, float y, float z) =>
            new CxMatrix4X4(new float[]
            {
                1, 0, 0, x,
                0, 1, 0, y,
                0, 0, 1, z,
                0, 0, 0, 1,
            });

        /// <summary>
        /// Creates a rotation matrix around the X axis by <paramref name="angle"/> radians.
        /// </summary>
        public static CxMatrix4X4 RotationX(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new CxMatrix4X4(new float[]
            {
                1,  0, 0, 0,
                0,  c,-s, 0,
                0,  s, c, 0,
                0,  0, 0, 1,
            });
        }

        /// <summary>
        /// Creates a rotation matrix around the Y axis by <paramref name="angle"/> radians.
        /// </summary>
        public static CxMatrix4X4 RotationY(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new CxMatrix4X4(new float[]
            {
                 c, 0, s, 0,
                 0, 1, 0, 0,
                -s, 0, c, 0,
                 0, 0, 0, 1,
            });
        }

        /// <summary>
        /// Creates a rotation matrix around the Z axis by <paramref name="angle"/> radians.
        /// </summary>
        public static CxMatrix4X4 RotationZ(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new CxMatrix4X4(new float[]
            {
                c, -s, 0, 0,
                s,  c, 0, 0,
                0,  0, 1, 0,
                0,  0, 0, 1,
            });
        }

        /// <summary>
        /// Creates a rotation matrix around an arbitrary unit axis by <paramref name="angle"/> radians
        /// using Rodrigues' rotation formula.
        /// </summary>
        /// <param name="axis">Rotation axis (will be normalized to unit length).</param>
        /// <param name="angle">Rotation angle in radians.</param>
        public static CxMatrix4X4 RotationAxis(CxVector3D axis, float angle)
        {
            float kx = axis.X, ky = axis.Y, kz = axis.Z;
            float len = (float)Math.Sqrt(kx * kx + ky * ky + kz * kz);
            if (len > 1e-6f) { kx /= len; ky /= len; kz /= len; }

            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            float t = 1f - c;

            float txx = t * kx * kx;
            float txy = t * kx * ky;
            float txz = t * kx * kz;
            float tyy = t * ky * ky;
            float tyz = t * ky * kz;
            float tzz = t * kz * kz;

            float sxz = s * kz;
            float sxy = s * ky;
            float syx = s * kx;

            return new CxMatrix4X4(new float[]
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
        public static CxMatrix4X4 Scale(float x, float y, float z) =>
            new CxMatrix4X4(new float[]
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
        public static CxMatrix4X4 LookAt(CxPoint3D eye, CxPoint3D center, CxVector3D up)
        {
            var f = new CxVector3D(center.X - eye.X, center.Y - eye.Y, center.Z - eye.Z).Normalize();
            var s = f.Cross(up).Normalize();
            var u = s.Cross(f);
            var eyeVec = new CxVector3D(eye.X, eye.Y, eye.Z);
            return new CxMatrix4X4(new float[]
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
        public static CxMatrix4X4 operator *(CxMatrix4X4 m1, CxMatrix4X4 m2)
        {
            var r = new float[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    r[i * 4 + j] = m1.Data[i * 4 + 0] * m2.Data[0 * 4 + j]
                                  + m1.Data[i * 4 + 1] * m2.Data[1 * 4 + j]
                                  + m1.Data[i * 4 + 2] * m2.Data[2 * 4 + j]
                                  + m1.Data[i * 4 + 3] * m2.Data[3 * 4 + j];
            return new CxMatrix4X4(r);
        }

        /// <summary>
        /// Returns the transpose of this matrix (rows become columns).
        /// </summary>
        public CxMatrix4X4 Transpose()
        {
            var r = new float[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    r[j * 4 + i] = Data[i * 4 + j];
            return new CxMatrix4X4(r);
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
        public CxMatrix4X4 Inverse()
        {
            float[] m   = new float[16];
            float[] inv = new float[16];
            Array.Copy(Data, m, 16);

            // Start with the identity as the augmented right-hand side.
            for (int i = 0; i < 4; i++) inv[i * 4 + i] = 1f;

            // Forward elimination with partial pivoting.
            for (int i = 0; i < 4; i++)
            {
                // Find the pivot row.
                float maxVal = Math.Abs(m[i * 4 + i]);
                int   maxRow = i;
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
                        float tmp       = m[i * 4 + j];
                        m[i * 4 + j]   = m[maxRow * 4 + j];
                        m[maxRow * 4 + j] = tmp;

                        tmp                 = inv[i * 4 + j];
                        inv[i * 4 + j]      = inv[maxRow * 4 + j];
                        inv[maxRow * 4 + j] = tmp;
                    }
                }

                // Eliminate column i below the pivot.
                for (int j = i + 1; j < 4; j++)
                {
                    float factor = m[j * 4 + i] / m[i * 4 + i];
                    for (int k = i; k < 4; k++) m[j * 4 + k]   -= factor * m[i * 4 + k];
                    for (int k = 0; k < 4; k++) inv[j * 4 + k] -= factor * inv[i * 4 + k];
                }
            }

            // Back substitution.
            for (int i = 3; i >= 0; i--)
            {
                for (int j = 0; j < 4; j++)
                {
                    float sum = 0;
                    for (int k = i + 1; k < 4; k++) sum += m[i * 4 + k] * inv[k * 4 + j];
                    inv[i * 4 + j] = (inv[i * 4 + j] - sum) / m[i * 4 + i];
                }
            }

            return new CxMatrix4X4(inv);
        }

        /// <summary>
        /// Transforms a 3D point by this matrix using row-major convention (column vector on the right).
        /// Performs perspective divide when the homogeneous <c>w</c> component is non-zero.
        /// </summary>
        /// <param name="point">The 3D point to transform.</param>
        /// <returns>The transformed 3D point.</returns>
        public CxPoint3D TransformPoint3D(CxPoint3D point)
        {
            float x = point.X, y = point.Y, z = point.Z;
            float tx = Data[0] * x + Data[1] * y + Data[2]  * z + Data[3];
            float ty = Data[4] * x + Data[5] * y + Data[6]  * z + Data[7];
            float tz = Data[8] * x + Data[9] * y + Data[10] * z + Data[11];
            float tw = Data[12]* x + Data[13]* y + Data[14] * z + Data[15];
            if (Math.Abs(tw) > 1e-6f) { tx /= tw; ty /= tw; tz /= tw; }
            return new CxPoint3D(tx, ty, tz);
        }

        /// <summary>
        /// Transforms a 3D vector by the upper-left 3x3 submatrix (rotation + scale).
        /// Vectors are not affected by translation or perspective.
        /// </summary>
        /// <param name="vector">The 3D vector to transform.</param>
        /// <returns>The transformed 3D vector.</returns>
        public CxVector3D TransformVector3D(CxVector3D vector)
        {
            float x = vector.X, y = vector.Y, z = vector.Z;
            return new CxVector3D(
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
        /// Thrown when the determinant of the 3×3 submatrix is zero (within a tolerance of 1e-6).
        /// </exception>
        public CxVector3D Solve3x3(CxVector3D b)
        {
            float a00 = Data[0], a01 = Data[1], a02 = Data[2];
            float a10 = Data[4], a11 = Data[5], a12 = Data[6];
            float a20 = Data[8], a21 = Data[9], a22 = Data[10];
            float b0 = b.X, b1 = b.Y, b2 = b.Z;

            float c11a22 = a11 * a22, c12a21 = a12 * a21;
            float c10a22 = a10 * a22, c12a20 = a12 * a20;
            float c10a21 = a10 * a21, c11a20 = a11 * a20;

            float det = a00 * (c11a22 - c12a21)
                      - a01 * (c10a22 - c12a20)
                      + a02 * (c10a21 - c11a20);

            if (Math.Abs(det) < 1e-6f)
                throw new InvalidOperationException(
                    "The 3×3 submatrix is singular; Cramer's rule cannot be applied.");

            float detX = b0 * (c11a22 - c12a21)
                       - a01 * (b1 * a22 - a12 * b2)
                       + a02 * (b1 * a21 - a11 * b2);

            float detY = a00 * (b1 * a22 - a12 * b2)
                       - b0 * (c10a22 - c12a20)
                       + a02 * (a10 * b2 - b1 * a20);

            float detZ = a00 * (a11 * b2 - b1 * a21)
                       - a01 * (a10 * b2 - b1 * a20)
                       + b0 * (c10a21 - c11a20);

            return new CxVector3D(detX / det, detY / det, detZ / det);
        }
    }
}
