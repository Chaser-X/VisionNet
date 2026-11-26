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
        /// <summary>
        /// Relative tolerance below which a pivot counts as zero while solving a linear system.
        /// It is compared against the largest element of the pivot's own row in the equilibrated
        /// system, so a system whose equations are uniformly small (or large) is not rejected.
        /// </summary>
        private const double SingularTolerance = 1e-12;

        /// <summary>
        /// Factor by which the row (or column) scales of a system may differ before its equations are
        /// equilibrated. A nearly uniform system is left untouched, so no rounding is introduced
        /// where the elimination is already well behaved. The value mirrors the row/column condition
        /// threshold of a standard equilibration (LAPACK <c>dgeequ</c>).
        /// </summary>
        private const double EquilibrationThreshold = 10.0;

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
            // Normalise finite non-zero axes at any scale; preserve zero and non-finite behaviour.
            bool finiteAxis = !double.IsNaN(kx) && !double.IsInfinity(kx)
                && !double.IsNaN(ky) && !double.IsInfinity(ky)
                && !double.IsNaN(kz) && !double.IsInfinity(kz);
            if (finiteAxis && (kx != 0.0 || ky != 0.0 || kz != 0.0))
            {
                var unit = axis.Normalize();
                kx = unit.X; ky = unit.Y; kz = unit.Z;
            }
            else if (!finiteAxis)
            {
                double len = Math.Sqrt(kx * kx + ky * ky + kz * kz);
                if (len > 1e-12) { kx /= len; ky /= len; kz /= len; }
            }

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
        /// <remarks>
        /// The result follows the same row-major <c>M · v</c> convention as
        /// <see cref="TransformPoint3D"/>: the rotation rows are the camera basis
        /// (<c>s</c>, <c>u</c>, <c>-f</c>), the translation is <c>-R · eye</c>, and the last row is
        /// <c>(0, 0, 0, 1)</c>. <paramref name="eye"/> therefore maps to the origin and
        /// <paramref name="center"/> to a point on the negative Z axis at the eye distance.
        /// </remarks>
        public static CxDMatrix4X4 LookAt(CxDPoint3D eye, CxDPoint3D center, CxDVector3D up)
        {
            var f = new CxDVector3D(center.X - eye.X, center.Y - eye.Y, center.Z - eye.Z).Normalize();
            var s = f.Cross(up).Normalize();
            var u = s.Cross(f);
            var eyeVec = new CxDVector3D(eye.X, eye.Y, eye.Z);
            return new CxDMatrix4X4(new double[]
            {
                 s.X,  s.Y,  s.Z, -s.Dot(eyeVec),
                 u.X,  u.Y,  u.Z, -u.Dot(eyeVec),
                -f.X, -f.Y, -f.Z,  f.Dot(eyeVec),
                 0,    0,    0,   1,
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
            // Every usable w divides, however small it is: a magnitude threshold silently drops
            // the perspective divide for a valid small w. w == 0 and NaN keep the previous result.
            if (tw != 0.0 && !double.IsNaN(tw)) { tx /= tw; ty /= tw; tz /= tw; }
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
        /// Solves the 3×3 linear system <c>A·x = b</c> with Gaussian elimination and scaled
        /// partial pivoting, where <c>A</c> is the upper-left 3×3 submatrix of this 4×4 matrix.
        /// </summary>
        /// <param name="b">The right-hand side vector.</param>
        /// <returns>The solution vector <c>x</c>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the 3×3 submatrix is singular (a pivot is negligible relative to the scale
        /// of its row in the equilibrated system).
        /// </exception>
        public CxDVector3D Solve3x3(CxDVector3D b)
        {
            var a = new double[]
            {
                Data[0], Data[1], Data[2],
                Data[4], Data[5], Data[6],
                Data[8], Data[9], Data[10],
            };
            var rhs = new double[] { b.X, b.Y, b.Z };

            // Equilibrate rows (including the right-hand side), then columns to stabilise elimination.
            // Leave similar scales untouched; undo column scaling on the solution below.
            var rowScale = new double[3];
            var colScale = new double[3];
            double rowMin = double.MaxValue, rowMaxAll = 0.0;
            for (int i = 0; i < 3; i++)
            {
                double rowMax = 0.0;
                for (int j = 0; j < 3; j++)
                {
                    double value = Math.Abs(a[i * 3 + j]);
                    if (value > rowMax) rowMax = value;
                }
                if (rowMax == 0.0)
                    throw new InvalidOperationException(
                        "The 3×3 submatrix is singular; the system cannot be solved.");
                rowScale[i] = rowMax;
                if (rowMax < rowMin) rowMin = rowMax;
                if (rowMax > rowMaxAll) rowMaxAll = rowMax;
            }
            if (rowMaxAll > rowMin * EquilibrationThreshold)
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++) a[i * 3 + j] /= rowScale[i];
                    rhs[i] /= rowScale[i];
                }
            }

            double colMin = double.MaxValue, colMaxAll = 0.0;
            for (int j = 0; j < 3; j++)
            {
                double colMax = 0.0;
                for (int i = 0; i < 3; i++)
                {
                    double value = Math.Abs(a[i * 3 + j]);
                    if (value > colMax) colMax = value;
                }
                if (colMax == 0.0)
                    throw new InvalidOperationException(
                        "The 3×3 submatrix is singular; the system cannot be solved.");
                colScale[j] = colMax;
                if (colMax < colMin) colMin = colMax;
                if (colMax > colMaxAll) colMaxAll = colMax;
            }
            if (colMaxAll > colMin * EquilibrationThreshold)
            {
                for (int j = 0; j < 3; j++)
                    for (int i = 0; i < 3; i++) a[i * 3 + j] /= colScale[j];
            }
            else
            {
                for (int j = 0; j < 3; j++) colScale[j] = 1.0;
            }

            // Row scales of the equilibrated matrix: used to pick each pivot and to judge it.
            for (int i = 0; i < 3; i++)
            {
                double rowMax = 0.0;
                for (int j = 0; j < 3; j++)
                {
                    double value = Math.Abs(a[i * 3 + j]);
                    if (value > rowMax) rowMax = value;
                }
                if (rowMax == 0.0)
                    throw new InvalidOperationException(
                        "The 3×3 submatrix is singular; the system cannot be solved.");
                rowScale[i] = rowMax;
            }

            for (int col = 0; col < 3; col++)
            {
                // Scaled partial pivoting: take the row whose entry in this column is largest
                // relative to its own scale.
                int pivot = col;
                double best = Math.Abs(a[col * 3 + col]) / rowScale[col];
                for (int row = col + 1; row < 3; row++)
                {
                    double candidate = Math.Abs(a[row * 3 + col]) / rowScale[row];
                    if (candidate > best) { best = candidate; pivot = row; }
                }

                if (pivot != col)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        double tmp = a[col * 3 + j];
                        a[col * 3 + j] = a[pivot * 3 + j];
                        a[pivot * 3 + j] = tmp;
                    }
                    double tmpScale = rowScale[col];
                    rowScale[col] = rowScale[pivot];
                    rowScale[pivot] = tmpScale;
                    double tmpRhs = rhs[col];
                    rhs[col] = rhs[pivot];
                    rhs[pivot] = tmpRhs;
                }

                if (Math.Abs(a[col * 3 + col]) <= SingularTolerance * rowScale[col])
                    throw new InvalidOperationException(
                        "The 3×3 submatrix is singular; the system cannot be solved.");

                for (int row = col + 1; row < 3; row++)
                {
                    double factor = a[row * 3 + col] / a[col * 3 + col];
                    if (factor == 0.0) continue;
                    for (int j = col; j < 3; j++) a[row * 3 + j] -= factor * a[col * 3 + j];
                    rhs[row] -= factor * rhs[col];
                }
            }

            // Back substitution.
            var x = new double[3];
            for (int row = 2; row >= 0; row--)
            {
                double sum = 0.0;
                for (int j = row + 1; j < 3; j++) sum += a[row * 3 + j] * x[j];
                x[row] = (rhs[row] - sum) / a[row * 3 + row];
            }

            // Undo the column equilibration: component i of the solution was scaled by colScale[i].
            for (int i = 0; i < 3; i++) x[i] /= colScale[i];

            return new CxDVector3D(x[0], x[1], x[2]);
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
