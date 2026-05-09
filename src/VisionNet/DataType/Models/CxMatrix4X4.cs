using System;

namespace VisionNet.DataType
{
    public class CxMatrix4X4
    {
        public CxMatrix4X4() { }
        public CxMatrix4X4(float[] data) { if (data.Length != 16) throw new ArgumentException("Matrix data must contain 16 elements."); Data = data; }
        public float[] Data { get; set; } = new float[16];
    }
}
