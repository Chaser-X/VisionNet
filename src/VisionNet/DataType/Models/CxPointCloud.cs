using System;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// Stores an ordered point cloud as a compact short-integer array.
    /// Data has <c>Width × Length × 3</c> elements representing (X, Y, Z) triples.
    /// A value of <c>-32768</c> marks an invalid / no-data point.
    /// </summary>
    public class CxPointCloud
    {
        private readonly object _lock = new object();

        /// <summary>Initializes an empty point cloud.</summary>
        public CxPointCloud() { }

        /// <summary>Initializes a point cloud with the given dimensions and allocates a zeroed data array.</summary>
        public CxPointCloud(int width, int length)
        {
            Width = width;
            Length = length;
            Data = new short[width * length * 3];
        }

        /// <summary>Initializes a point cloud with all fields provided.</summary>
        public CxPointCloud(int width, int length, short[] data, byte[] intensity,
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
        /// Gets or sets the raw data array. <c>Width × Length × 3</c> (X,Y,Z) triples. <c>-32768</c> = invalid.
        /// </summary>
        public short[] Data { get; set; }

        /// <summary>Gets or sets per-point intensity values (0–255), or <c>null</c> if not available.</summary>
        public byte[] Intensity { get; set; }

        /// <summary>Gets or sets the world-space X origin.</summary>
        public float XOffset { get; set; }

        /// <summary>Gets or sets the world-space Y origin.</summary>
        public float YOffset { get; set; }

        /// <summary>Gets or sets the world-space Z origin (added to each point).</summary>
        public float ZOffset { get; set; }

        /// <summary>Gets or sets the scale factor for X.</summary>
        public float XScale { get; set; }

        /// <summary>Gets or sets the scale factor for Y.</summary>
        public float YScale { get; set; }

        /// <summary>Gets or sets the scale factor for Z.</summary>
        public float ZScale { get; set; }

        /// <summary>Gets or sets the bounding box of all valid points.</summary>
        public Box3D? BoundingBox { get; set; }

        /// <summary>
        /// Copies point data from an unmanaged memory block into <see cref="Data"/>.
        /// Thread-safe.
        /// </summary>
        public void SetData(IntPtr dataPtr)
        {
            lock (_lock)
            {
                int size = Width * Length * 3;
                Data = new short[size];
                Marshal.Copy(dataPtr, Data, 0, size);
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
                int count = Width * Length;
                var pts = new CxPoint3D[count];
                for (int i = 0; i < count; i++)
                {
                    short sx = Data[i * 3];
                    short sy = Data[i * 3 + 1];
                    short sz = Data[i * 3 + 2];
                    pts[i] = new CxPoint3D
                    {
                        X = sx == -32768 ? float.NegativeInfinity : XOffset + sx * XScale,
                        Y = sy == -32768 ? float.NegativeInfinity : YOffset + sy * YScale,
                        Z = sz == -32768 ? float.NegativeInfinity : ZOffset + sz * ZScale,
                    };
                }
                return pts;
            }
        }

        /// <summary>Releases all managed arrays and resets to default state.</summary>
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
            BoundingBox = null;
        }
    }
}
