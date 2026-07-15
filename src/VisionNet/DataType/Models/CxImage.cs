using System;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    /// <summary>
    /// A 2D image container. Pixel data is stored as a native typed array
    /// (byte[], short[], ushort[], or float[]) corresponding to <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Memory layout (row-major, interleaved channels):</b><br/>
    /// Element at (col, row, ch) = <c>Data[((row * Width + col) * Channel + ch)]</c>
    /// (cast Data to the appropriate typed array first).</para>
    /// <para><b>Channel layouts:</b> 1 = grayscale, 3 = RGB, 4 = BGRA
    /// (Format32bppArgb memory order).</para>
    /// </remarks>
    public class CxImage
    {
        private Array _data;

        // ── Constructors ──────────────────────────────────────────────────────────

        /// <summary>Initializes an empty image with no allocated data.</summary>
        public CxImage() { }

        /// <summary>Initializes an image with the given dimensions and allocates an empty data array.</summary>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="type">Pixel element data type (default: <see cref="PlainType.Byte"/>).</param>
        /// <param name="channel">Number of channels per pixel (default: 1).</param>
        public CxImage(int width, int height, PlainType type = PlainType.Byte, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = type;
            _data   = AllocateArray(type, width * height * channel);
        }

        /// <summary>Initializes an image from an existing <see cref="byte"/> array. Type is set to <see cref="PlainType.Byte"/>.</summary>
        public CxImage(int width, int height, byte[] data, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = PlainType.Byte;
            _data   = data;
        }

        /// <summary>Initializes an image from an existing <see cref="short"/> array. Type is set to <see cref="PlainType.Short"/>.</summary>
        public CxImage(int width, int height, short[] data, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = PlainType.Short;
            _data   = data;
        }

        /// <summary>Initializes an image from an existing <see cref="ushort"/> array. Type is set to <see cref="PlainType.UShort"/>.</summary>
        public CxImage(int width, int height, ushort[] data, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = PlainType.UShort;
            _data   = data;
        }

        /// <summary>Initializes an image from an existing <see cref="float"/> array. Type is set to <see cref="PlainType.Real"/>.</summary>
        public CxImage(int width, int height, float[] data, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = PlainType.Real;
            _data   = data;
        }

        // ── Properties ────────────────────────────────────────────────────────────

        /// <summary>Gets or sets the image width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the image height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the number of channels per pixel.</summary>
        public int Channel { get; set; } = 1;

        /// <summary>Gets the pixel element data type. Set automatically by constructors and <see cref="SetData"/> overloads.</summary>
        public PlainType Type { get; private set; } = PlainType.Byte;

        /// <summary>
        /// Gets the pixel data as a read-only array reference.
        /// Cast to the appropriate typed array based on <see cref="Type"/>:
        /// <c>(byte[])Data</c>, <c>(short[])Data</c>, <c>(ushort[])Data</c>, or <c>(float[])Data</c>.
        /// </summary>
        public Array Data => _data;

        // ── SetData overloads ─────────────────────────────────────────────────────

        /// <summary>Assigns a <see cref="byte"/> array and updates all image properties. Type is set to <see cref="PlainType.Byte"/>.</summary>
        public void SetData(int width, int height, byte[] data, int channel = 1)
        {
            Width = width; Height = height; Channel = channel;
            Type  = PlainType.Byte;
            _data = data;
        }

        /// <summary>Assigns a <see cref="short"/> array and updates all image properties. Type is set to <see cref="PlainType.Short"/>.</summary>
        public void SetData(int width, int height, short[] data, int channel = 1)
        {
            Width = width; Height = height; Channel = channel;
            Type  = PlainType.Short;
            _data = data;
        }

        /// <summary>Assigns a <see cref="ushort"/> array and updates all image properties. Type is set to <see cref="PlainType.UShort"/>.</summary>
        public void SetData(int width, int height, ushort[] data, int channel = 1)
        {
            Width = width; Height = height; Channel = channel;
            Type  = PlainType.UShort;
            _data = data;
        }

        /// <summary>Assigns a <see cref="float"/> array and updates all image properties. Type is set to <see cref="PlainType.Real"/>.</summary>
        public void SetData(int width, int height, float[] data, int channel = 1)
        {
            Width = width; Height = height; Channel = channel;
            Type  = PlainType.Real;
            _data = data;
        }

        /// <summary>
        /// Copies data from an unmanaged memory block and updates all image properties.
        /// The element type must be specified explicitly via <paramref name="type"/>;
        /// total byte count is calculated as <c>width × height × channel × <see cref="BytesPerElement"/>(type)</c>.
        /// </summary>
        /// <param name="ptr">Source pointer to unmanaged memory.</param>
        /// <param name="type">Pixel element data type — required because it cannot be inferred from an IntPtr.</param>
        public void SetData(int width, int height, IntPtr ptr, PlainType type, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = type;

            int n = width * height * channel;

            switch (type)
            {
                case PlainType.Byte:
                {
                    var arr = new byte[n];
                    Marshal.Copy(ptr, arr, 0, n);
                    _data = arr;
                    break;
                }
                case PlainType.Int16:
                case PlainType.Short:
                {
                    var arr = new short[n];
                    Marshal.Copy(ptr, arr, 0, n);
                    _data = arr;
                    break;
                }
                case PlainType.UShort:
                {
                    // Marshal.Copy has no ushort[] overload; copy via byte[] + Buffer.BlockCopy
                    int byteCount = n * 2;
                    var tmp = new byte[byteCount];
                    Marshal.Copy(ptr, tmp, 0, byteCount);
                    var arr = new ushort[n];
                    Buffer.BlockCopy(tmp, 0, arr, 0, byteCount);
                    _data = arr;
                    break;
                }
                case PlainType.Real:
                {
                    var arr = new float[n];
                    Marshal.Copy(ptr, arr, 0, n);
                    _data = arr;
                    break;
                }
                default:
                {
                    var arr = new byte[n];
                    Marshal.Copy(ptr, arr, 0, n);
                    _data = arr;
                    break;
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Returns the number of bytes occupied by a single element of the given type.</summary>
        public static int BytesPerElement(PlainType type)
        {
            switch (type)
            {
                case PlainType.Byte:   return 1;
                case PlainType.Int16:
                case PlainType.Short:  return 2;
                case PlainType.UShort: return 2;
                case PlainType.Real:   return 4;
                default:               return 1;
            }
        }

        private static Array AllocateArray(PlainType type, int elementCount)
        {
            switch (type)
            {
                case PlainType.Byte:   return new byte[elementCount];
                case PlainType.Int16:
                case PlainType.Short:  return new short[elementCount];
                case PlainType.UShort: return new ushort[elementCount];
                case PlainType.Real:   return new float[elementCount];
                default:               return new byte[elementCount];
            }
        }

        /// <summary>Releases the pixel data and resets the dimensions to zero.</summary>
        public void Dispose()
        {
            Width   = 0;
            Height  = 0;
            _data   = null;
        }
    }
}
