using System;
using System.Runtime.InteropServices;

namespace VisionNet.DataType
{
    public enum SurfaceType { PointCloud, Surface }

    public class CxSurface
    {
        private object lockObj = new object();
        public CxSurface() { }
        public CxSurface(int width, int length, SurfaceType type = SurfaceType.Surface) { Width = width; Length = length; Data = new short[width * length]; Type = type; }
        public CxSurface(int width, int length, short[] data, byte[] intensity, float xOffset, float yOffset, float zOffset, float xScale, float yScale, float zScale, SurfaceType type = SurfaceType.Surface) : this(width, length, type) { Data = data; Intensity = intensity; XOffset = xOffset; YOffset = yOffset; ZOffset = zOffset; XScale = xScale; YScale = yScale; ZScale = zScale; Type = type; }
        public SurfaceType Type { get; set; }
        public int Width { get; set; }
        public int Length { get; set; }
        public short[] Data { get; set; }
        public byte[] Intensity { get; set; }
        public float XOffset { get; set; }
        public float YOffset { get; set; }
        public float ZOffset { get; set; }
        public float XScale { get; set; }
        public float YScale { get; set; }
        public float ZScale { get; set; }
        public void SetData(IntPtr dataPtr) { lock (lockObj) { var size = Width * Length; if (Type == SurfaceType.PointCloud) size *= 3; Data = new short[size]; Marshal.Copy(dataPtr, Data, 0, size); } }
        public void SetInetnsity(IntPtr intensityPtr) { lock (lockObj) { var size = Width * Length; Intensity = new byte[size]; Marshal.Copy(intensityPtr, Intensity, 0, size); } }
        public CxPoint3D[] ToPoints() { lock (lockObj) { var points = new CxPoint3D[Width * Length]; for (int i = 0; i < Length; i++) { for (int j = 0; j < Width; j++) { var index = i * Width + j; if (Type == SurfaceType.Surface) { points[index] = new CxPoint3D { X = XOffset + j * XScale, Y = YOffset + i * YScale, Z = Data[index] == -32768 ? float.NegativeInfinity : ZOffset + Data[index] * ZScale }; } else if (Type == SurfaceType.PointCloud) { points[index] = new CxPoint3D { X = Data[index * 3] == -32768 ? float.NegativeInfinity : XOffset + Data[index * 3] * XScale, Y = Data[index * 3 + 1] == -32768 ? float.NegativeInfinity : YOffset + Data[index * 3 + 1] * YScale, Z = Data[index * 3 + 2] == -32768 ? float.NegativeInfinity : ZOffset + Data[index * 3 + 2] * ZScale }; } } } return points; } }
        public void Dispose() { Width = 0; Length = 0; Data = null; Intensity = null; XOffset = 0; YOffset = 0; ZOffset = 0; XScale = 1; YScale = 1; ZScale = 1; Type = SurfaceType.Surface; }
    }
}
