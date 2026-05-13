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
        /// Gets or sets the 16 matrix elements in row-major order
        /// (<c>Data[i * 4 + j]</c> = element at row <c>i</c>, column <c>j</c>).
        /// </summary>
        public float[] Data { get; set; } = new float[16];

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
    }
}
