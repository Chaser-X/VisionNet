using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    using VisionNet;

    /// <summary>
    /// A 2D image container. Pixel data is stored as a native typed array
    /// (byte[], short[], int[], or float[]) corresponding to <see cref="Type"/>.
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
        /// <param name="type">Pixel element data type (default: <see cref="PlainType.UInt8"/>).</param>
        /// <param name="channel">Number of channels per pixel (default: 1).</param>
        public CxImage(int width, int height, PlainType type = PlainType.UInt8, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = type;
            _data   = AllocateArray(type, width * height * channel);
        }

        /// <summary>Initializes an image from an existing <see cref="byte"/> array. Type is set to <see cref="PlainType.UInt8"/>.</summary>
        public CxImage(int width, int height, byte[] data, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = PlainType.UInt8;
            _data   = data;
        }

        /// <summary>Initializes an image from an existing <see cref="short"/> array. Type is set to <see cref="PlainType.Int16"/>.</summary>
        public CxImage(int width, int height, short[] data, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = PlainType.Int16;
            _data   = data;
        }

        /// <summary>Initializes an image from an existing <see cref="int"/> array. Type is set to <see cref="PlainType.Int32"/>.</summary>
        public CxImage(int width, int height, int[] data, int channel = 1)
        {
            Width   = width;
            Height  = height;
            Channel = channel;
            Type    = PlainType.Int32;
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
        public PlainType Type { get; private set; } = PlainType.UInt8;

        /// <summary>
        /// Gets the pixel data as a read-only array reference.
        /// Cast to the appropriate typed array based on <see cref="Type"/>:
        /// <c>(byte[])Data</c>, <c>(short[])Data</c>, <c>(ushort[])Data</c>, or <c>(float[])Data</c>.
        /// </summary>
        public Array Data => _data;

        // ── SetData overloads ─────────────────────────────────────────────────────

        /// <summary>Assigns a <see cref="byte"/> array and updates all image properties. Type is set to <see cref="PlainType.UInt8"/>.</summary>
        public void SetData(int width, int height, byte[] data, int channel = 1)
        {
            Width = width; Height = height; Channel = channel;
            Type  = PlainType.UInt8;
            _data = data;
        }

        /// <summary>Assigns a <see cref="short"/> array and updates all image properties. Type is set to <see cref="PlainType.Int16"/>.</summary>
        public void SetData(int width, int height, short[] data, int channel = 1)
        {
            Width = width; Height = height; Channel = channel;
            Type  = PlainType.Int16;
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
                case PlainType.UInt8:
                {
                    var arr = new byte[n];
                    Marshal.Copy(ptr, arr, 0, n);
                    _data = arr;
                    break;
                }
                case PlainType.Int16:
                {
                    var arr = new short[n];
                    Marshal.Copy(ptr, arr, 0, n);
                    _data = arr;
                    break;
                }
                case PlainType.Int32:
                {
                    var arr = new int[n];
                    Marshal.Copy(ptr, arr, 0, n);
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
                case PlainType.UInt8:  return 1;
                case PlainType.Int16:  return 2;
                case PlainType.Int32:  return 4;
                case PlainType.Real:   return 4;
                default:               return 1;
            }
        }

        private static Array AllocateArray(PlainType type, int elementCount)
        {
            switch (type)
            {
                case PlainType.UInt8:  return new byte[elementCount];
                case PlainType.Int16:  return new short[elementCount];
                case PlainType.Int32:  return new int[elementCount];
                case PlainType.Real:   return new float[elementCount];
                default:               return new byte[elementCount];
            }
        }

        /// <summary>
        /// Creates a thumbnail that fits within the specified dimensions while preserving the aspect ratio.
        /// If the image is already within the bounds, returns the current instance.
        /// </summary>
        /// <param name="maxWidth">Maximum width of the thumbnail in pixels.</param>
        /// <param name="maxHeight">Maximum height of the thumbnail in pixels.</param>
        /// <returns>A new <see cref="CxImage"/> instance, or the current instance if no resize is needed.</returns>
        public CxImage GetThumbnail(int maxWidth, int maxHeight)
        {
            return VisionOperator.ResizeImage(this, maxWidth, maxHeight);
        }

        /// <summary>
        /// Converts this image to a <see cref="Bitmap"/> with <see cref="PixelFormat.Format32bppArgb"/>.
        /// </summary>
        public Bitmap ToBitmap()
        {
            return VisionOperator.ToBitmap(this);
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
