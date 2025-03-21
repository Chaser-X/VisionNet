using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VisionNet.DataType
{
    [StructLayout(LayoutKind.Explicit)]
    public struct CxPoint3D
    {
        public CxPoint3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        [FieldOffset(0)]
        public float X;
        [FieldOffset(4)]
        public float Y;
        [FieldOffset(8)]
        public float Z;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct CxPoint3DI
    {
        public CxPoint3DI(float x, float y, float z, float i)
        {
            X = x;
            Y = y;
            Z = z;
            Intensity = i;
        }
        public float X;
        public float Y;
        public float Z;
        public float Intensity;
    }

    public struct Segment
    {
        public CxPoint3D Start;
        public CxPoint3D End;

        public Segment(CxPoint3D start, CxPoint3D end)
        {
            Start = start;
            End = end;
        }
    }
    public struct TextInfo
    {
        public CxPoint3D Location;
        public string Text;

        public TextInfo(CxPoint3D location, string text)
        {
            Location = location;
            Text = text;
        }
    }
    public enum SurfaceType
    {
        //点云
        PointCloud,
        //网格
        Surface,
        //曲线
    }
    public class CxSuface
    {
        private object lockObj = new object();
        public CxSuface() { }
        public CxSuface(int width, int length, SurfaceType type = SurfaceType.Surface)
        {
            Width = width;
            Length = length;
            Data = new short[width * length];
            //Intensity = new byte[width * length];
            Type = type;
        }
        public CxSuface(int width, int length, short[] data, byte[] intensity, float xOffset, float yOffset, float zOffset, float xScale, float yScale, float zScale, SurfaceType type = SurfaceType.Surface) : this(width, length, type)
        {
            Data = data;
            Intensity = intensity;
            XOffset = xOffset;
            YOffset = yOffset;
            ZOffset = zOffset;
            XScale = xScale;
            YScale = yScale;
            ZScale = zScale;
            Type = type;
        }
        public SurfaceType Type { get; set; }
        //点云的宽
        public int Width { get; set; }
        //点云的长
        public int Length { get; set; }
        //点云的位置高度指针
        public short[] Data { get; set; }
        //点云的亮度信息数组
        public byte[] Intensity { get; set; }
        //点云的x偏移
        public float XOffset { get; set; }
        //点云的y偏移
        public float YOffset { get; set; }
        //点云的z偏移
        public float ZOffset { get; set; }
        //点云的x缩放
        public float XScale { get; set; }
        //点云的y缩放
        public float YScale { get; set; }
        //点云的z缩放
        public float ZScale { get; set; }
        public void SetData(IntPtr dataPtr)
        {
            lock (lockObj)
            {
                var size = Width * Length;
                if (Type == SurfaceType.PointCloud)
                    size *= 3;
                Data = new short[size];
                Marshal.Copy(dataPtr, Data, 0, size);
            }
        }
        public void SetInetnsity(IntPtr intensityPtr)
        {
            lock (lockObj)
            {
                var size = Width * Length;
                Intensity = new byte[size];
                Marshal.Copy(intensityPtr, Intensity, 0, size);
            }
        }
        //输出3D点集合
        public CxPoint3D[] ToPoints()
        {
            lock (lockObj)
            {
                var points = new CxPoint3D[Width * Length];
                for (int i = 0; i < Length; i++)
                {
                    for (int j = 0; j < Width; j++)
                    {
                        var index = i * Width + j;
                        if (Type == SurfaceType.Surface)
                        {
                            points[index] = new CxPoint3D
                            {
                                X = XOffset + j * XScale,
                                Y = YOffset + i * YScale,
                                Z = Data[index] == -32768 ? float.NegativeInfinity : ZOffset + Data[index] * ZScale
                            };
                        }
                        else if (Type == SurfaceType.PointCloud)
                        {
                            points[index] = new CxPoint3D
                            {
                                X = XOffset + Data[index * 3] * XScale,
                                Y = YOffset + Data[index * 3 + 1] * YScale,
                                Z = ZOffset + Data[index * 3 + 2] * ZScale
                            };
                        }
                    }
                }
                return points;
            }
        }
    }
}
