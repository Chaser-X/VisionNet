using System;
using System.Globalization;

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
        /// <summary>
        /// Relative tolerance below which a pivot counts as zero while solving a linear system.
        /// It is compared against the largest element of the pivot's own row in the equilibrated
        /// system, so a system whose equations are uniformly small (or large) is not rejected.
        /// Roughly eight times the single-precision machine epsilon, i.e. only systems without
        /// significant digits left.
        /// </summary>
        private const float SingularTolerance = 1e-6f;

        /// <summary>
        /// Factor by which the row (or column) scales of a system may differ before its equations are
        /// equilibrated. A nearly uniform system is left untouched, so no rounding is introduced
        /// where the elimination is already well behaved. The value mirrors the row/column condition
        /// threshold of a standard equilibration (LAPACK <c>dgeequ</c>).
        /// </summary>
        private const float EquilibrationThreshold = 10f;

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
        /// Computes the inverse of this matrix by solving <c>A · X = I</c> with Gaussian
        /// elimination and scaled partial pivoting.
        /// </summary>
        /// <returns>The inverse matrix.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the matrix is singular (a pivot is negligible relative to the scale of its
        /// row in the equilibrated system).
        /// </exception>
        public CxMatrix3X3 Inverse()
        {
            var a = (float[])Data.Clone();
            var inv = new float[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
            };
            if (!TrySolveInPlace(a, inv, 3, 3))
                throw new InvalidOperationException("Matrix is singular and cannot be inverted.");

            return new CxMatrix3X3(inv);
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
            // Every usable w divides, however small it is: a magnitude threshold silently drops
            // the perspective divide for a valid small w. w == 0 and NaN keep the previous result.
            if (tw != 0f && !float.IsNaN(tw)) { tx /= tw; ty /= tw; }
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
        /// Solves the 2×2 linear system <c>A·x = b</c> with Gaussian elimination and scaled
        /// partial pivoting, where <c>A</c> is the upper-left 2×2 submatrix of this 3×3 matrix.
        /// </summary>
        /// <param name="b">The right-hand side vector.</param>
        /// <returns>The solution vector <c>x</c>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the 2×2 submatrix is singular (a pivot is negligible relative to the scale
        /// of its row in the equilibrated system).
        /// </exception>
        public CxVector2D Solve2x2(CxVector2D b)
        {
            var a = new float[] { Data[0], Data[1], Data[3], Data[4] };
            var rhs = new float[] { b.X, b.Y };
            if (!TrySolveInPlace(a, rhs, 2, 1))
                throw new InvalidOperationException(
                    "The 2×2 submatrix is singular; the system cannot be solved.");

            return new CxVector2D(rhs[0], rhs[1]);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Solves <c>A · X = B</c> in place for a small square system, using Gaussian elimination
        /// with scaled partial pivoting.
        /// </summary>
        /// <remarks>
        /// The equations are equilibrated first (rows then columns, only when their scales span more
        /// than <see cref="EquilibrationThreshold"/>), and the system is reported as singular when a
        /// pivot of the equilibrated matrix is negligible compared with the largest element of its own
        /// row. The decision is therefore relative to the scale of the matrix rather than to an
        /// absolute determinant threshold, and a uniformly small or large system is solved as it is.
        /// </remarks>
        /// <param name="a"><paramref name="n"/>×<paramref name="n"/> coefficient matrix in row-major order.</param>
        /// <param name="b">
        /// <paramref name="nrhs"/> right-hand sides of <paramref name="n"/> components each, in
        /// row-major order (right-hand side <c>k</c> of row <c>i</c> at <c>b[i * nrhs + k]</c>);
        /// overwritten with the solution.
        /// </param>
        /// <param name="n">Order of the system.</param>
        /// <param name="nrhs">Number of right-hand sides.</param>
        /// <returns><c>false</c> when the system is singular; the array contents are then unspecified.</returns>
        private static bool TrySolveInPlace(float[] a, float[] b, int n, int nrhs)
        {
            // Equilibrate rows (including right-hand sides), then columns to stabilise elimination.
            // Leave similar scales untouched; undo column scaling on the solution below.
            var rowScale = new float[n];
            var colScale = new float[n];
            float rowMin = float.MaxValue, rowMaxAll = 0f;
            for (int i = 0; i < n; i++)
            {
                float rowMax = 0f;
                for (int j = 0; j < n; j++)
                {
                    float value = Math.Abs(a[i * n + j]);
                    if (value > rowMax) rowMax = value;
                }
                if (rowMax == 0f) return false;
                rowScale[i] = rowMax;
                if (rowMax < rowMin) rowMin = rowMax;
                if (rowMax > rowMaxAll) rowMaxAll = rowMax;
            }
            if (rowMaxAll > rowMin * EquilibrationThreshold)
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++) a[i * n + j] /= rowScale[i];
                    for (int k = 0; k < nrhs; k++) b[i * nrhs + k] /= rowScale[i];
                }
            }

            float colMin = float.MaxValue, colMaxAll = 0f;
            for (int j = 0; j < n; j++)
            {
                float colMax = 0f;
                for (int i = 0; i < n; i++)
                {
                    float value = Math.Abs(a[i * n + j]);
                    if (value > colMax) colMax = value;
                }
                if (colMax == 0f) return false;
                colScale[j] = colMax;
                if (colMax < colMin) colMin = colMax;
                if (colMax > colMaxAll) colMaxAll = colMax;
            }
            if (colMaxAll > colMin * EquilibrationThreshold)
            {
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++) a[i * n + j] /= colScale[j];
            }
            else
            {
                for (int j = 0; j < n; j++) colScale[j] = 1f;
            }

            // Row scales of the equilibrated matrix: used to pick each pivot and to judge it.
            for (int i = 0; i < n; i++)
            {
                float rowMax = 0f;
                for (int j = 0; j < n; j++)
                {
                    float value = Math.Abs(a[i * n + j]);
                    if (value > rowMax) rowMax = value;
                }
                if (rowMax == 0f) return false;
                rowScale[i] = rowMax;
            }

            for (int col = 0; col < n; col++)
            {
                // Scaled partial pivoting: take the row whose entry in this column is largest
                // relative to its own scale.
                int pivot = col;
                float best = Math.Abs(a[col * n + col]) / rowScale[col];
                for (int row = col + 1; row < n; row++)
                {
                    float candidate = Math.Abs(a[row * n + col]) / rowScale[row];
                    if (candidate > best) { best = candidate; pivot = row; }
                }

                if (pivot != col)
                {
                    for (int j = 0; j < n; j++)
                    {
                        float tmp = a[col * n + j];
                        a[col * n + j] = a[pivot * n + j];
                        a[pivot * n + j] = tmp;
                    }
                    float tmpScale = rowScale[col];
                    rowScale[col] = rowScale[pivot];
                    rowScale[pivot] = tmpScale;
                    for (int k = 0; k < nrhs; k++)
                    {
                        float tmpB = b[col * nrhs + k];
                        b[col * nrhs + k] = b[pivot * nrhs + k];
                        b[pivot * nrhs + k] = tmpB;
                    }
                }

                if (Math.Abs(a[col * n + col]) <= SingularTolerance * rowScale[col]) return false;

                for (int row = col + 1; row < n; row++)
                {
                    float factor = a[row * n + col] / a[col * n + col];
                    if (factor == 0f) continue;
                    for (int j = col; j < n; j++) a[row * n + j] -= factor * a[col * n + j];
                    for (int k = 0; k < nrhs; k++) b[row * nrhs + k] -= factor * b[col * nrhs + k];
                }
            }

            // Back substitution.
            for (int row = n - 1; row >= 0; row--)
            {
                for (int k = 0; k < nrhs; k++)
                {
                    float sum = 0f;
                    for (int j = row + 1; j < n; j++) sum += a[row * n + j] * b[j * nrhs + k];
                    b[row * nrhs + k] = (b[row * nrhs + k] - sum) / a[row * n + row];
                }
            }

            // Undo the column equilibration: component i of the solution was scaled by colScale[i].
            for (int i = 0; i < n; i++)
                for (int k = 0; k < nrhs; k++) b[i * nrhs + k] /= colScale[i];

            return true;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxMatrix3X3([{0:G9}, {1:G9}, {2:G9}, {3:G9}, {4:G9}, {5:G9}, {6:G9}, {7:G9}, {8:G9}])",
                Data[0], Data[1], Data[2], Data[3], Data[4], Data[5], Data[6], Data[7], Data[8]);
    }
}
