using System;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// Stores a structured height map as a compact short-integer array.
    /// <see cref="Data"/> has <c>Width × Length</c> elements representing Z heights.
    /// A value of <c>-32768</c> marks an invalid / no-data point.
    /// </summary>
    public class CxSurface
    {
        private readonly object _lock = new object();

        /// <summary>Initializes an empty surface.</summary>
        public CxSurface() { }

        /// <summary>Initializes a surface with the given dimensions and allocates a zeroed data array.</summary>
        public CxSurface(int width, int length)
        {
            Width = width;
            Length = length;
            Data = new short[width * length];
        }

        /// <summary>Initializes a surface with all fields provided.</summary>
        public CxSurface(int width, int length, short[] data, byte[] intensity,
            float xOffset, float yOffset, float zOffset,
            float xScale, float yScale, float zScale)
            : this(width, length)
        {
            Data = data;
            Intensity = intensity;
            XOffset = xOffset;
            YOffset = yOffset;
            ZOffset = zOffset;
            XScale = xScale;
            YScale = yScale;
            ZScale = zScale;
        }

        /// <summary>Gets or sets the number of columns (X axis).</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the number of rows (Y axis).</summary>
        public int Length { get; set; }

        /// <summary>
        /// Gets or sets the raw data array. <c>Width × Length</c> Z heights. <c>-32768</c> = invalid.
        /// </summary>
        public short[] Data { get; set; }

        /// <summary>Gets or sets per-point intensity values (0–255), or <c>null</c> if not available.</summary>
        public byte[] Intensity { get; set; }

        /// <summary>Gets or sets the world-space X origin of the grid.</summary>
        public float XOffset { get; set; }

        /// <summary>Gets or sets the world-space Y origin of the grid.</summary>
        public float YOffset { get; set; }

        /// <summary>Gets or sets the world-space Z origin (added to each height value).</summary>
        public float ZOffset { get; set; }

        /// <summary>Gets or sets the grid spacing along X.</summary>
        public float XScale { get; set; }

        /// <summary>Gets or sets the grid spacing along Y.</summary>
        public float YScale { get; set; }

        /// <summary>Gets or sets the scale factor applied to each raw Z value.</summary>
        public float ZScale { get; set; }

        /// <summary>
        /// Copies height data from an unmanaged memory block into <see cref="Data"/>.
        /// Thread-safe.
        /// </summary>
        public void SetData(IntPtr dataPtr)
        {
            lock (_lock)
            {
                int size = Width * Length;
                Data = new short[size];
                Marshal.Copy(dataPtr, Data, 0, size);
            }
        }

        public void SetData(short[] data)
        {
            lock (_lock)
            {
                Data = data;
            }
        }

        /// <summary>
        /// Copies intensity data from an unmanaged memory block into <see cref="Intensity"/>.
        /// Thread-safe.
        /// </summary>
        public void SetIntensity(IntPtr intensityPtr)
        {
            lock (_lock)
            {
                int size = Width * Length;
                Intensity = new byte[size];
                Marshal.Copy(intensityPtr, Intensity, 0, size);
            }
        }

        /// <summary>
        /// Converts the internal data array to an array of world-space 3D points.
        /// Invalid entries (raw value <c>-32768</c>) are returned as <see cref="float.NegativeInfinity"/>.
        /// Thread-safe.
        /// </summary>
        public CxPoint3D[] ToPoints()
        {
            lock (_lock)
            {
                var points = new CxPoint3D[Width * Length];
                for (int row = 0; row < Length; row++)
                {
                    for (int col = 0; col < Width; col++)
                    {
                        int index = row * Width + col;
                        points[index] = new CxPoint3D
                        {
                            X = XOffset + col * XScale,
                            Y = YOffset + row * YScale,
                            Z = Data[index] == -32768
                                ? float.NegativeInfinity
                                : ZOffset + Data[index] * ZScale,
                        };
                    }
                }
                return points;
            }
        }

        /// <summary>Releases all managed arrays and resets the surface to its default state.</summary>
        public void Dispose()
        {
            Width = 0;
            Length = 0;
            Data = null;
            Intensity = null;
            XOffset = 0;
            YOffset = 0;
            ZOffset = 0;
            XScale = 1;
            YScale = 1;
            ZScale = 1;
        }
    }
}
