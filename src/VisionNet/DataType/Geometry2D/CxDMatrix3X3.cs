using System;
using System.Globalization;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 3×3 double-precision floating-point matrix stored in <b>row-major</b> order.
    /// Element at row <c>i</c>, column <c>j</c> is accessed as <c>Data[i * 3 + j]</c>.
    /// Designed for 2D affine transformations (rotation, scale, translation) using
    /// homogeneous coordinates. Double-precision counterpart of <see cref="CxMatrix3X3"/>.
    /// </summary>
    public class CxDMatrix3X3
    {
        /// <summary>Numerical tolerance used for perspective divide and singularity checks.</summary>
        private const double Epsilon = 1e-12;

        /// <summary>Initialises a 3×3 zero matrix.</summary>
        public CxDMatrix3X3() { }

        /// <summary>
        /// Initialises the matrix from a 9-element row-major array.
        /// </summary>
        /// <param name="data">9-element array in row-major order.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="data"/> does not contain exactly 9 elements.
        /// </exception>
        public CxDMatrix3X3(double[] data)
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
        public CxDMatrix3X3(double[,] data)
        {
            if (data.Rank != 2 || data.GetLength(0) != 3 || data.GetLength(1) != 3)
                throw new ArgumentException("Matrix data must be a 3x3 two-dimensional array.", nameof(data));
            Data = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Data[i * 3 + j] = data[i, j];
        }

        /// <summary>
        /// Gets or sets the 9 matrix elements in row-major order
        /// (<c>Data[i * 3 + j]</c> = element at row <c>i</c>, column <c>j</c>).
        /// </summary>
        public double[] Data { get; set; } = new double[9];

        /// <summary>
        /// Gets the 9 matrix elements as a 3x3 two-dimensional array in row-major order.
        /// </summary>
        public double[,] Data2D
        {
            get
            {
                var result = new double[3, 3];
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        result[i, j] = Data[i * 3 + j];
                return result;
            }
        }

        // ── Factory methods ──────────────────────────────────────────────────────

        /// <summary>Returns a new 3×3 identity matrix.</summary>
        public static CxDMatrix3X3 Identity() =>
            new CxDMatrix3X3(new double[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
            });

        /// <summary>
        /// Creates a 2D rotation matrix (counter-clockwise) by the given angle in degrees.
        /// </summary>
        public static CxDMatrix3X3 Rotation(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double c = Math.Cos(rad);
            double s = Math.Sin(rad);
            return new CxDMatrix3X3(new double[]
            {
                 c, -s, 0,
                 s,  c, 0,
                 0,  0, 1,
            });
        }

        /// <summary>
        /// Creates a non-uniform 2D scale matrix with independent factors along each axis.
        /// </summary>
        public static CxDMatrix3X3 Scale(double x, double y) =>
            new CxDMatrix3X3(new double[]
            {
                x, 0, 0,
                0, y, 0,
                0, 0, 1,
            });

        /// <summary>
        /// Creates a 2D translation matrix.
        /// </summary>
        public static CxDMatrix3X3 Translation(double x, double y) =>
            new CxDMatrix3X3(new double[]
            {
                1, 0, x,
                0, 1, y,
                0, 0, 1,
            });

        // ── Operators & transformations ──────────────────────────────────────────

        /// <summary>
        /// Returns the matrix product <c>m1 × m2</c> (standard row-major matrix multiplication).
        /// </summary>
        public static CxDMatrix3X3 operator *(CxDMatrix3X3 m1, CxDMatrix3X3 m2)
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = m1.Data[i * 3 + 0] * m2.Data[0 * 3 + j]
                                  + m1.Data[i * 3 + 1] * m2.Data[1 * 3 + j]
                                  + m1.Data[i * 3 + 2] * m2.Data[2 * 3 + j];
            return new CxDMatrix3X3(r);
        }

        /// <summary>
        /// Returns the transpose of this matrix (rows become columns).
        /// </summary>
        public CxDMatrix3X3 Transpose()
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[j * 3 + i] = Data[i * 3 + j];
            return new CxDMatrix3X3(r);
        }

        /// <summary>
        /// Gets the determinant of this 3×3 matrix.
        /// </summary>
        public double Determinant
        {
            get
            {
                double a = Data[0], b = Data[1], c = Data[2];
                double d = Data[3], e = Data[4], f = Data[5];
                double g = Data[6], h = Data[7], i = Data[8];
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
        /// Thrown when the matrix is singular (determinant is zero within a tolerance of 1e-12).
        /// </exception>
        public CxDMatrix3X3 Inverse()
        {
            double det = Determinant;
            if (Math.Abs(det) < Epsilon)
                throw new InvalidOperationException("Matrix is singular and cannot be inverted.");

            double a = Data[0], b = Data[1], c = Data[2];
            double d = Data[3], e = Data[4], f = Data[5];
            double g = Data[6], h = Data[7], i = Data[8];

            double invDet = 1.0 / det;
            return new CxDMatrix3X3(new double[]
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
        public CxDPoint2D TransformPoint2D(CxDPoint2D point)
        {
            double x = point.X, y = point.Y;
            double tx = Data[0] * x + Data[1] * y + Data[2];
            double ty = Data[3] * x + Data[4] * y + Data[5];
            double tw = Data[6] * x + Data[7] * y + Data[8];
            if (Math.Abs(tw) > Epsilon) { tx /= tw; ty /= tw; }
            return new CxDPoint2D(tx, ty);
        }

        /// <summary>
        /// Transforms a 2D vector by the upper-left 2x2 submatrix (rotation + scale).
        /// Vectors are not affected by translation or perspective.
        /// </summary>
        /// <param name="vector">The 2D vector to transform.</param>
        /// <returns>The transformed 2D vector.</returns>
        public CxDVector2D TransformVector2D(CxDVector2D vector)
        {
            double x = vector.X, y = vector.Y;
            return new CxDVector2D(
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
        /// Thrown when the determinant of the 2×2 submatrix is zero (within a tolerance of 1e-12).
        /// </exception>
        public CxDVector2D Solve2x2(CxDVector2D b)
        {
            double a00 = Data[0], a01 = Data[1];
            double a10 = Data[3], a11 = Data[4];
            double b0 = b.X, b1 = b.Y;

            double det = a00 * a11 - a01 * a10;
            if (Math.Abs(det) < Epsilon)
                throw new InvalidOperationException(
                    "The 2×2 submatrix is singular; Cramer's rule cannot be applied.");

            double detX = b0 * a11 - a01 * b1;
            double detY = a00 * b1 - b0 * a10;
            return new CxDVector2D(detX / det, detY / det);
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "CxDMatrix3X3([{0:G17}, {1:G17}, {2:G17}, {3:G17}, {4:G17}, {5:G17}, {6:G17}, {7:G17}, {8:G17}])",
                Data[0], Data[1], Data[2], Data[3], Data[4], Data[5], Data[6], Data[7], Data[8]);

        /// <summary>Converts a single-precision matrix to double precision (lossless).</summary>
        public static implicit operator CxDMatrix3X3(CxMatrix3X3 m)
        {
            if (m == null) return null;
            var d = new double[9];
            for (int i = 0; i < 9; i++) d[i] = m.Data[i];
            return new CxDMatrix3X3(d);
        }

        /// <summary>Converts this double-precision matrix to single precision (may lose precision).</summary>
        public static explicit operator CxMatrix3X3(CxDMatrix3X3 m)
        {
            if (m == null) return null;
            var f = new float[9];
            for (int i = 0; i < 9; i++) f[i] = (float)m.Data[i];
            return new CxMatrix3X3(f);
        }
    }
}
