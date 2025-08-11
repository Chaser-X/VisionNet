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

    //3D坐标系
    public struct CxCoordination3D
    {
        public CxPoint3D Origin;
        public CxVector3D XAxis;
        public CxVector3D YAxis;
        public CxVector3D ZAxis;
        public CxCoordination3D(CxPoint3D origin, CxVector3D xAxis, CxVector3D yAxis, CxVector3D zAxis)
        {
            Origin = origin;
            XAxis = xAxis;
            YAxis = yAxis;
            ZAxis = zAxis;
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

        public void Dispose()
        {
            Width = 0;
            Length = 0;
            Data = null;
            Intensity = null;
            XOffset = 0;
            YOffset = 0;
            ZOffset = 0;
            XScale = 1;
            YScale = 1;
            ZScale = 1;
            Type = SurfaceType.Surface; // Reset to default
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
        public void Dispose()
        {
            Width = 0;
            Height = 0;
            Data = null;
        }
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
        //纹理大小
        public int TextureWidth { get; set; } = 0;
        public int TextureHeight { get; set; } = 0;

        //UV坐标
        public CxPoint2D[] UVs { get; set; }

        public void Disopse()
        {
            Vertexs = null;
            Indices = null;
            Intensity = null;
            UVs = null;
        }
    }
    public class CxMatrix4X4
    {
        public CxMatrix4X4() { }
        public CxMatrix4X4(float[] data)
        {
            if (data.Length != 16)
                throw new ArgumentException("Matrix data must contain 16 elements.");
            Data = data;
        }
        public float[] Data { get; set; }
        = new float[16];

        public static CxMatrix4X4 Identity()
        {
            return new CxMatrix4X4(new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            });
        }

        public static CxMatrix4X4 operator *(CxMatrix4X4 m1, CxMatrix4X4 m2)
        {
            var result = new float[16];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i * 4 + j] = m1.Data[i * 4 + 0] * m2.Data[0 * 4 + j] +
                                         m1.Data[i * 4 + 1] * m2.Data[1 * 4 + j] +
                                         m1.Data[i * 4 + 2] * m2.Data[2 * 4 + j] +
                                         m1.Data[i * 4 + 3] * m2.Data[3 * 4 + j];
                }
            }
            return new CxMatrix4X4(result);
        }

        //inverse function
        public CxMatrix4X4 Inverse()
        {
            //高斯消元法计算矩阵的逆
            float[] inv = new float[16];
            float[] m = new float[16];
            Array.Copy(Data, m, 16);
            for (int i = 0; i < 4; i++)
            {
                // 单位矩阵
                inv[i * 4 + i] = 1;
            }
            for (int i = 0; i < 4; i++)
            {
                // 寻找主元
                float maxVal = Math.Abs(m[i * 4 + i]);
                int maxRow = i;
                for (int j = i + 1; j < 4; j++)
                {
                    if (Math.Abs(m[j * 4 + i]) > maxVal)
                    {
                        maxVal = Math.Abs(m[j * 4 + i]);
                        maxRow = j;
                    }
                }
                if (maxRow != i)
                {
                    // 交换行
                    for (int j = 0; j < 4; j++)
                    {
                        float temp = m[i * 4 + j];
                        m[i * 4 + j] = m[maxRow * 4 + j];
                        m[maxRow * 4 + j] = temp;
                        temp = inv[i * 4 + j];
                        inv[i * 4 + j] = inv[maxRow * 4 + j];
                        inv[maxRow * 4 + j] = temp;
                    }
                }
                // 消元
                for (int j = i + 1; j < 4; j++)
                {
                    float factor = m[j * 4 + i] / m[i * 4 + i];
                    for (int k = i; k < 4; k++)
                    {
                        m[j * 4 + k] -= factor * m[i * 4 + k];
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        inv[j * 4 + k] -= factor * inv[i * 4 + k];
                    }
                }
            }
            // 回代
            for (int i = 3; i >= 0; i--)
            {
                for (int j = 0; j < 4; j++)
                {
                    float sum = 0;
                    for (int k = i + 1; k < 4; k++)
                    {
                        sum += m[i * 4 + k] * inv[k * 4 + j];
                    }
                    inv[i * 4 + j] = (inv[i * 4 + j] - sum) / m[i * 4 + i];
                }
            }
            return new CxMatrix4X4(inv);
        }
        //计算矩阵转置
        public CxMatrix4X4 Transpose()
        {
            var result = new float[16];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[j * 4 + i] = this.Data[i * 4 + j];
                }
            }
            return new CxMatrix4X4(result);
        }
        public static CxMatrix4X4 Translation(float x, float y, float z)
        {
            return new CxMatrix4X4(new float[]
            {
                1, 0, 0, x,
                0, 1, 0, y,
                0, 0, 1, z,
                0, 0, 0, 1
            });
        }
        public static CxMatrix4X4 RotationX(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new CxMatrix4X4(new float[]
            {
                1, 0, 0, 0,
                0, c, -s, 0,
                0, s, c, 0,
                0, 0, 0, 1
            });
        }
        public static CxMatrix4X4 RotationY(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new CxMatrix4X4(new float[]
            {
                c, 0, s, 0,
                0, 1, 0, 0,
                -s, 0, c, 0,
                0, 0, 0, 1
            });
        }
        public static CxMatrix4X4 RotationZ(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new CxMatrix4X4(new float[]
            {
                c, -s, 0, 0,
                s, c, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            });
        }
        public static CxMatrix4X4 Scale(float x, float y, float z)
        {
            return new CxMatrix4X4(new float[]
            {
                x, 0, 0, 0,
                0, y, 0, 0,
                0, 0, z, 0,
                0, 0, 0, 1
            });
        }
        public static CxMatrix4X4 LookAt(CxPoint3D eye, CxPoint3D center, CxVector3D up)
        {
            CxVector3D f = new CxVector3D(center.X - eye.X, center.Y - eye.Y, center.Z - eye.Z).Normalize();
            CxVector3D s = f.Cross(up).Normalize();
            CxVector3D u = s.Cross(f);
            return new CxMatrix4X4(new float[]
            {
                s.X, u.X, -f.X, 0,
                s.Y, u.Y, -f.Y, 0,
                s.Z, u.Z, -f.Z, 0,
                -s.Dot(new CxVector3D(eye.X, eye.Y, eye.Z)),
                -u.Dot(new CxVector3D(eye.X, eye.Y, eye.Z)),
                f.Dot(new CxVector3D(eye.X, eye.Y, eye.Z)), 1
            });
        }
    }
}