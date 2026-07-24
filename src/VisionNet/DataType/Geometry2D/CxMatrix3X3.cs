using System;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 3×3 single-precision floating-point matrix stored in <b>row-major</b> order.
    /// Element at row <c>i</c>, column <c>j</c> is accessed as <c>Data[i * 3 + j]</c>.
    /// Designed for 2D affine transformations (rotation, scale, translation) using
    /// homogeneous coordinates.
    /// </summary>
    public class CxMatrix3X3
    {
        /// <summary>Initialises a 3×3 zero matrix.</summary>
        public CxMatrix3X3() { }

        /// <summary>
        /// Initialises the matrix from a 9-element row-major array.
        /// </summary>
        /// <param name="data">9-element array in row-major order.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="data"/> does not contain exactly 9 elements.
        /// </exception>
        public CxMatrix3X3(float[] data)
        {
            if (data.Length != 9)
                throw new ArgumentException("Matrix data must contain exactly 9 elements.", nameof(data));
            Data = data;
        }

        /// <summary>
        /// Initialises the matrix from a 3x3 two-dimensional array in row-major order.
        /// </summary>
        /// <param name="data">A 3x3 two-dimensional array in row-major order.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="data"/> is not a 3x3 two-dimensional array.
        /// </exception>
        public CxMatrix3X3(float[,] data)
        {
            if (data.Rank != 2 || data.GetLength(0) != 3 || data.GetLength(1) != 3)
                throw new ArgumentException("Matrix data must be a 3x3 two-dimensional array.", nameof(data));
            Data = new float[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Data[i * 3 + j] = data[i, j];
        }

        /// <summary>
        /// Gets or sets the 9 matrix elements in row-major order
        /// (<c>Data[i * 3 + j]</c> = element at row <c>i</c>, column <c>j</c>).
        /// </summary>
        public float[] Data { get; set; } = new float[9];

        /// <summary>
        /// Gets the 9 matrix elements as a 3x3 two-dimensional array in row-major order.
        /// </summary>
        public float[,] Data2D
        {
            get
            {
                var result = new float[3, 3];
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        result[i, j] = Data[i * 3 + j];
                return result;
            }
        }

        // ── Factory methods ──────────────────────────────────────────────────────

        /// <summary>Returns a new 3×3 identity matrix.</summary>
        public static CxMatrix3X3 Identity() =>
            new CxMatrix3X3(new float[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
            });

        /// <summary>
        /// Creates a 2D rotation matrix (counter-clockwise) by the given angle in degrees.
        /// </summary>
        public static CxMatrix3X3 Rotation(float angleDeg)
        {
            float rad = angleDeg * (float)Math.PI / 180f;
            float c = (float)Math.Cos(rad);
            float s = (float)Math.Sin(rad);
            return new CxMatrix3X3(new float[]
            {
                 c, -s, 0,
                 s,  c, 0,
                 0,  0, 1,
            });
        }

        /// <summary>
        /// Creates a non-uniform 2D scale matrix with independent factors along each axis.
        /// </summary>
        public static CxMatrix3X3 Scale(float x, float y) =>
            new CxMatrix3X3(new float[]
            {
                x, 0, 0,
                0, y, 0,
                0, 0, 1,
            });

        /// <summary>
        /// Creates a 2D translation matrix.
        /// </summary>
        public static CxMatrix3X3 Translation(float x, float y) =>
            new CxMatrix3X3(new float[]
            {
                1, 0, x,
                0, 1, y,
                0, 0, 1,
            });

        // ── Operators & transformations ──────────────────────────────────────────

        /// <summary>
        /// Returns the matrix product <c>m1 × m2</c> (standard row-major matrix multiplication).
        /// </summary>
        public static CxMatrix3X3 operator *(CxMatrix3X3 m1, CxMatrix3X3 m2)
        {
            var r = new float[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = m1.Data[i * 3 + 0] * m2.Data[0 * 3 + j]
                                  + m1.Data[i * 3 + 1] * m2.Data[1 * 3 + j]
                                  + m1.Data[i * 3 + 2] * m2.Data[2 * 3 + j];
            return new CxMatrix3X3(r);
        }

        /// <summary>
        /// Returns the transpose of this matrix (rows become columns).
        /// </summary>
        public CxMatrix3X3 Transpose()
        {
            var r = new float[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[j * 3 + i] = Data[i * 3 + j];
            return new CxMatrix3X3(r);
        }

        /// <summary>
        /// Gets the determinant of this 3×3 matrix.
        /// </summary>
        public float Determinant
        {
            get
            {
                float a = Data[0], b = Data[1], c = Data[2];
                float d = Data[3], e = Data[4], f = Data[5];
                float g = Data[6], h = Data[7], i = Data[8];
                return a * (e * i - f * h)
                     - b * (d * i - f * g)
                     + c * (d * h - e * g);
            }
        }

        /// <summary>
        /// Computes the inverse of this matrix using the adjugate formula.
        /// </summary>
        /// <returns>The inverse matrix.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the matrix is singular (determinant is zero within a tolerance of 1e-6).
        /// </exception>
        public CxMatrix3X3 Inverse()
        {
            float det = Determinant;
            if (Math.Abs(det) < 1e-6f)
                throw new InvalidOperationException("Matrix is singular and cannot be inverted.");

            float a = Data[0], b = Data[1], c = Data[2];
            float d = Data[3], e = Data[4], f = Data[5];
            float g = Data[6], h = Data[7], i = Data[8];

            float invDet = 1f / det;
            return new CxMatrix3X3(new float[]
            {
                (e * i - f * h) * invDet,
                (c * h - b * i) * invDet,
                (b * f - c * e) * invDet,
                (f * g - d * i) * invDet,
                (a * i - c * g) * invDet,
                (c * d - a * f) * invDet,
                (d * h - e * g) * invDet,
                (b * g - a * h) * invDet,
                (a * e - b * d) * invDet,
            });
        }

        /// <summary>
        /// Transforms a 2D point by this matrix using homogeneous coordinates.
        /// </summary>
        /// <param name="point">The 2D point to transform.</param>
        /// <returns>The transformed 2D point.</returns>
        public CxPoint2D TransformPoint2D(CxPoint2D point)
        {
            float x = point.X, y = point.Y;
            float tx = Data[0] * x + Data[1] * y + Data[2];
            float ty = Data[3] * x + Data[4] * y + Data[5];
            float tw = Data[6] * x + Data[7] * y + Data[8];
            if (Math.Abs(tw) > 1e-6f) { tx /= tw; ty /= tw; }
            return new CxPoint2D(tx, ty);
        }

        /// <summary>
        /// Transforms a 2D vector by the upper-left 2x2 submatrix (rotation + scale).
        /// Vectors are not affected by translation or perspective.
        /// </summary>
        /// <param name="vector">The 2D vector to transform.</param>
        /// <returns>The transformed 2D vector.</returns>
        public CxVector2D TransformVector2D(CxVector2D vector)
        {
            float x = vector.X, y = vector.Y;
            return new CxVector2D(
                Data[0] * x + Data[1] * y,
                Data[3] * x + Data[4] * y);
        }

        /// <summary>
        /// Solves the 2×2 linear system <c>A·x = b</c> using Cramer's rule,
        /// where <c>A</c> is the upper-left 2×2 submatrix of this 3×3 matrix.
        /// </summary>
        /// <param name="b">The right-hand side vector.</param>
        /// <returns>The solution vector <c>x</c>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the determinant of the 2×2 submatrix is zero (within a tolerance of 1e-6).
        /// </exception>
        public CxVector2D Solve2x2(CxVector2D b)
        {
            float a00 = Data[0], a01 = Data[1];
            float a10 = Data[3], a11 = Data[4];
            float b0 = b.X, b1 = b.Y;

            float det = a00 * a11 - a01 * a10;
            if (Math.Abs(det) < 1e-6f)
                throw new InvalidOperationException(
                    "The 2×2 submatrix is singular; Cramer's rule cannot be applied.");

            float detX = b0 * a11 - a01 * b1;
            float detY = a00 * b1 - b0 * a10;
            return new CxVector2D(detX / det, detY / det);
        }
    }
}
