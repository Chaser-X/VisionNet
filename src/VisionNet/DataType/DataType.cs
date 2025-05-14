using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VisionNet.DataType
{
    #region 3d datatype
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
    //向量
    [StructLayout(LayoutKind.Sequential)]
    public struct CxVector3D
    {
        public CxVector3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        public float X;
        public float Y;
        public float Z;
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public static CxVector3D operator +(CxVector3D v1, CxVector3D v2)
        {
            return new CxVector3D(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }
        public static CxVector3D operator -(CxVector3D v1, CxVector3D v2)
        {
            return new CxVector3D(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }
        public static CxVector3D operator *(CxVector3D v, float scale)
        {
            return new CxVector3D(v.X * scale, v.Y * scale, v.Z * scale);
        }
        public static CxVector3D operator /(CxVector3D v, float scale)
        {
            return new CxVector3D(v.X / scale, v.Y / scale, v.Z / scale);
        }
        //向量点乘
        public float Dot(CxVector3D v2)
        {
            return X * v2.X + Y * v2.Y + Z * v2.Z;
        }
        //向量叉乘
        public CxVector3D Cross(CxVector3D v2)
        {
            return new CxVector3D(Y * v2.Z - Z * v2.Y, Z * v2.X - X * v2.Z, X * v2.Y - Y * v2.X);
        }
        //向量归一化
        public CxVector3D Normalize()
        {
            return new CxVector3D(X / Length, Y / Length, Z / Length);
        }
    }
    //尺寸
    [StructLayout(LayoutKind.Sequential)]
    public struct CxSize3D
    {
        public CxSize3D(float width, float height, float depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }
        public float Width;
        public float Height;
        public float Depth;
    }
    //线段
    public struct Segment3D
    {
        public CxPoint3D Start;
        public CxPoint3D End;

        public Segment3D(CxPoint3D start, CxPoint3D end)
        {
            Start = start;
            End = end;
        }
    }
    //文本信息
    public struct TextInfo
    {
        public CxPoint3D Location;
        public string Text;
        public float Size;
        public TextInfo(CxPoint3D location, string text, float size = 15f)
        {
            Location = location;
            Text = text;
            Size = size;
        }
    }
    //球体
    public struct Sphere
    {
        public CxPoint3D Center;
        public float Radius;
        public Sphere(CxPoint3D center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }
    //多边形
    public struct Polygon3D
    {
        public CxPoint3D[] Points;
        public bool IsClosed;
        public Polygon3D(CxPoint3D[] points, bool isClosed = true)
        {
            Points = points;
            IsClosed = isClosed;
        }
    }
    //圆形
    public struct Circle3D
    {
        public CxPoint3D Center;
        public float Radius;
        public CxVector3D Normal;
        public Circle3D(CxPoint3D center, CxVector3D normal, float radius)
        {
            Center = center;
            Radius = radius;
            Normal = normal;
        }
    }
    //平面
    public struct Plane3D
    {
        public CxPoint3D Point;
        public CxVector3D Normal;
        public Plane3D(CxPoint3D point, CxVector3D normal)
        {
            Point = point;
            Normal = normal;
        }
    }
    //Box3D
    public struct Box3D
    {
        public CxPoint3D Center;
        public CxSize3D Size;
        public Box3D(CxPoint3D center, CxSize3D size)
        {
            Center = center;
            Size = size;
        }
    }
    #endregion

    #region 2d datatype
    //2D点
    public struct CxPoint2D
    {
        public CxPoint2D(float x, float y)
        {
            X = x;
            Y = y;
        }
        public float X;
        public float Y;
    }
    //2D向量
    public struct CxVector2D
    {
        public CxVector2D(float x, float y)
        {
            X = x;
            Y = y;
        }
        public float X;
        public float Y;
    }
    //2D线段
    public struct Segment2D
    {
        public CxPoint2D Start;
        public CxPoint2D End;
        public Segment2D(CxPoint2D start, CxPoint2D end)
        {
            Start = start;
            End = end;
        }
    }
    //2D多边形
    public struct Polygon2D
    {
        public CxPoint2D[] Points;
        public bool IsClosed;
        public Polygon2D(CxPoint2D[] points, bool isClosed = true)
        {
            Points = points;
            IsClosed = isClosed;
        }
    }
    //2D圆形
    public struct Circle2D
    {
        public CxPoint2D Center;
        public float Radius;
        public Circle2D(CxPoint2D center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }
    //2D文本
    public struct Text2D
    {
        public CxPoint2D Location;
        public string Text;
        public int FontSize;
        public Text2D(CxPoint2D location, string text, int fontSize = 12)
        {
            Location = location;
            Text = text;
            FontSize = fontSize;
        }
    }
    #endregion
    public enum SurfaceType
    {
        //点云
        PointCloud,
        //网格
        Surface,
        //曲线
    }
    public class CxSurface
    {
        private object lockObj = new object();
        public CxSurface() { }
        public CxSurface(int width, int length, SurfaceType type = SurfaceType.Surface)
        {
            Width = width;
            Length = length;
            Data = new short[width * length];
            //Intensity = new byte[width * length];
            Type = type;
        }
        public CxSurface(int width, int length, short[] data, byte[] intensity, float xOffset, float yOffset, float zOffset, float xScale, float yScale, float zScale, SurfaceType type = SurfaceType.Surface) : this(width, length, type)
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
                                X = Data[index * 3] == -32768 ? float.NegativeInfinity : XOffset + Data[index * 3] * XScale,
                                Y = Data[index * 3 + 1] == -32768 ? float.NegativeInfinity : YOffset + Data[index * 3 + 1] * YScale,
                                Z = Data[index * 3 + 2] == -32768 ? float.NegativeInfinity : ZOffset + Data[index * 3 + 2] * ZScale
                            };
                        }
                    }
                }
                return points;
            }
        }
    }

    //2D图像，数据类型可选byte,short,float
    public class CxImage<T>
    {
        public CxImage() { }
        public CxImage(int width, int height)
        {
            Width = width;
            Height = height;
            Data = new T[width * height];
        }
        public int Width { get; set; }
        public int Height { get; set; }
        public T[] Data { get; set; }
        //public void SetData(IntPtr dataPtr)
        //{
        //    var size = Width * Height;
        //    Data = new T[size];
        //    Marshal.Copy(dataPtr, Data, 0, size);
        //}
    }
    //Mesh网格对象
    public class CxMesh
    {
        public CxMesh() { }
        //网格点
        public CxPoint3D[] Vertexs { get; set; }
        //网格面
        public uint[] Indices { get; set; }
        //亮度纹理
        public byte[] Intensity { get; set; }
    }
}