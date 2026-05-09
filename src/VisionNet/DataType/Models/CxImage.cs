namespace VisionNet.DataType
{
    /// <summary>
    /// A generic 2D image container storing a flat row-major pixel array of type <typeparamref name="T"/>.
    /// Suitable for intensity maps, depth images, or any single-channel / multi-channel raster data.
    /// </summary>
    /// <typeparam name="T">
    /// Pixel element type (e.g. <see cref="byte"/>, <see cref="float"/>, <see cref="CxPoint3D"/>).
    /// </typeparam>
    public class CxImage<T>
    {
        /// <summary>Initializes an empty image with no allocated data.</summary>
        public CxImage() { }

        /// <summary>Initializes an image with the given dimensions and allocates the data array.</summary>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        public CxImage(int width, int height)
        {
            Width  = width;
            Height = height;
            Data   = new T[width * height];
        }

        /// <summary>Gets or sets the image width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the image height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the pixel data in row-major order (<c>Data[row * Width + col]</c>).
        /// </summary>
        public T[] Data { get; set; }

        /// <summary>Releases the pixel data array and resets the dimensions to zero.</summary>
        public void Dispose()
        {
            Width  = 0;
            Height = 0;
            Data   = null;
        }
    }
}
