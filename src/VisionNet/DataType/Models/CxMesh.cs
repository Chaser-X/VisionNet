namespace VisionNet.DataType
{
    public class CxMesh
    {
        public CxMesh() { }
        public CxPoint3D[] Vertexs { get; set; }
        public uint[] Indices { get; set; }
        public byte[] Intensity { get; set; }
        public int TextureWidth { get; set; } = 0;
        public int TextureHeight { get; set; } = 0;
        public CxPoint2D[] UVs { get; set; }
        public void Dispose() { Vertexs = null; Indices = null; Intensity = null; UVs = null; }
    }
}
