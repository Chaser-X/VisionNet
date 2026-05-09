using System;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 4×4 single-precision floating-point matrix stored in column-major order
    /// (OpenGL convention). The <see cref="Data"/> array has 16 elements indexed
    /// as <c>column * 4 + row</c>.
    /// </summary>
    public class CxMatrix4X4
    {
        /// <summary>Initializes an identity matrix.</summary>
        public CxMatrix4X4() { }

        /// <summary>Initializes the matrix from a 16-element array.</summary>
        /// <param name="data">Column-major matrix data. Must contain exactly 16 elements.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> does not have 16 elements.</exception>
        public CxMatrix4X4(float[] data)
        {
            if (data.Length != 16)
                throw new ArgumentException("Matrix data must contain exactly 16 elements.", nameof(data));
            Data = data;
        }

        /// <summary>Gets or sets the 16 matrix elements in column-major order.</summary>
        public float[] Data { get; set; } = new float[16];
    }
}
