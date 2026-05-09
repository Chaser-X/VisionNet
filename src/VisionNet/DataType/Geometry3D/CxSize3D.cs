using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CxSize3D
    {
        public CxSize3D(float width, float height, float depth) { Width = width; Height = height; Depth = depth; }
        public float Width; public float Height; public float Depth;
    }
}
