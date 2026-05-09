namespace VisionNet.DataType
{
    public class CxImage<T>
    {
        public CxImage() { }
        public CxImage(int width, int height) { Width = width; Height = height; Data = new T[width * height]; }
        public int Width { get; set; }
        public int Height { get; set; }
        public T[] Data { get; set; }
        public void Dispose() { Width = 0; Height = 0; Data = null; }
    }
}
