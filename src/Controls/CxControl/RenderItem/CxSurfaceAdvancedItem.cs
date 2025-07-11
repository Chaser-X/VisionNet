using SharpGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxSurfaceAdvancedItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public CxSurface Surface { get; private set; }
        private uint[] vboIds = new uint[2]; // 顶点、UV坐标
        private uint textureId = 0; // 纹理ID
        private bool vboInitialized = false;
        private bool textureInitialized = false;
        private bool pointCloudUpdated = false;
        private List<uint> meshIndexs = new List<uint>();
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }

        // 最大顶点数量限制
        public int MaxPointCount { get; set; } = int.MaxValue;

        // 采样因子 - 根据MaxPointCount动态计算
        private int samplingFactorX = 1;
        private int samplingFactorY = 1;

        private SurfaceMode surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get { return surfaceMode; }
            set
            {
                pointCloudUpdated = value != surfaceMode;
                surfaceMode = value;
            }
        }

        private SurfaceColorMode surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get { return surfaceColorMode; }
            set
            {
                pointCloudUpdated = value != surfaceColorMode;
                surfaceColorMode = value;
            }
        }

        public CxSurfaceAdvancedItem(CxSurface surface, SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color, int maxPointCount = int.MaxValue)
        {
            this.Surface = surface;
            this.SurfaceMode = surfaceMode;
            this.SurfaceColorMode = surfaceColorMode;
            this.MaxPointCount = maxPointCount;

            // 计算采样因子
            CalculateSamplingFactors();

            BoundingBox = GetBoundingBox();
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
            pointCloudUpdated = false;
        }

        /// <summary>
        /// 计算采样因子
        /// </summary>
        private void CalculateSamplingFactors()
        {
            if (Surface == null || Surface.Width <= 0 || Surface.Length <= 0)
                return;

            int totalPoints = Surface.Width * Surface.Length;

            // 如果总点数小于最大点数限制，不需要采样
            if (totalPoints <= MaxPointCount)
            {
                samplingFactorX = 1;
                samplingFactorY = 1;
                return;
            }

            // 计算需要的采样率
            double samplingRate = Math.Sqrt((double)MaxPointCount / totalPoints);

            // 计算X和Y方向的采样因子
            samplingFactorX = Math.Max(1, (int)(1.0 / samplingRate));
            samplingFactorY = Math.Max(1, (int)(1.0 / samplingRate));

            // 确保采样后的点数不会超过最大限制
            while ((Surface.Width / samplingFactorX) * (Surface.Length / samplingFactorY) > MaxPointCount)
            {
                if (samplingFactorX <= samplingFactorY)
                    samplingFactorX++;
                else
                    samplingFactorY++;
            }

            // 确保计算的采样尺寸是正确的
            int sampledWidth = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // 向上取整
            int sampledLength = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // 向上取整

            // 再次验证总点数
            if (sampledWidth * sampledLength > MaxPointCount)
            {
                // 如果仍然超出，增加采样因子
                double ratio = Math.Sqrt((double)(sampledWidth * sampledLength) / MaxPointCount);
                samplingFactorX = Math.Max(1, (int)(samplingFactorX * ratio));
                samplingFactorY = Math.Max(1, (int)(samplingFactorY * ratio));
            }
        }

        public void Draw(OpenGL gl)
        {
            if (IsDisposed)
            {
                IsDisposed = false; // 重置释放标记
                if (vboInitialized)
                {
                    gl.DeleteBuffers(3, vboIds);
                    vboInitialized = false;
                }
                if (textureInitialized)
                {
                    gl.DeleteTextures(1, new uint[] { textureId });
                    textureInitialized = false;
                }
                if (Surface != null)
                    Surface.Dispose();
                Surface = null;
                OnDisposed?.Invoke();
                IsDisposed = true;
                Debug.WriteLine("SurfaceItem disposed");
                return;
            }

            if (Surface == null || Surface.Data.Length == 0) return;

            if (vboInitialized && pointCloudUpdated)
            {
                gl.DeleteBuffers(2, vboIds);
                vboInitialized = false;

                if (textureInitialized)
                {
                    gl.DeleteTextures(1, new uint[] { textureId });
                    textureInitialized = false;
                }
            }

            if (!vboInitialized)
            {
                gl.GenBuffers(3, vboIds);
                vboInitialized = true;
                pointCloudUpdated = true;
            }

            if (!textureInitialized)
            {
                uint[] ids = new uint[1];
                gl.GenTextures(1, ids);
                textureId = ids[0];
                textureInitialized = true;
            }

            if (pointCloudUpdated)
            {
                // 重新计算采样因子
                CalculateSamplingFactors();

                // 计算采样后的网格尺寸
                int sampledWidth = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // 向上取整
                int sampledLength = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // 向上取整
                int totalVertices = sampledWidth * sampledLength;

                // 生成顶点和UV数据
                float[] vertices = new float[totalVertices * 3];
                float[] uvCoords = new float[totalVertices * 2];

                int vertexIndex = 0;

                for (int y = 0; y < Surface.Length ; y += samplingFactorY)
                {
                    for (int x = 0; x < Surface.Width ; x += samplingFactorX)
                    {
                        int surfaceIndex = y * Surface.Width + x;

                        // 获取顶点坐标
                        float xPos, yPos, zPos;

                        if (Surface.Type == SurfaceType.Surface)
                        {
                            xPos = Surface.XOffset + x * Surface.XScale;
                            yPos = Surface.YOffset + y * Surface.YScale;
                            zPos = Surface.Data[surfaceIndex] == -32768 ?
                                float.NegativeInfinity :
                                Surface.ZOffset + Surface.Data[surfaceIndex] * Surface.ZScale;
                        }
                        else // PointCloud
                        {
                            xPos = Surface.Data[surfaceIndex * 3] == -32768 ?
                                float.NegativeInfinity :
                                Surface.XOffset + Surface.Data[surfaceIndex * 3] * Surface.XScale;

                            yPos = Surface.Data[surfaceIndex * 3 + 1] == -32768 ?
                                float.NegativeInfinity :
                                Surface.YOffset + Surface.Data[surfaceIndex * 3 + 1] * Surface.YScale;

                            zPos = Surface.Data[surfaceIndex * 3 + 2] == -32768 ?
                                float.NegativeInfinity :
                                Surface.ZOffset + Surface.Data[surfaceIndex * 3 + 2] * Surface.ZScale;
                        }

                        // 设置顶点坐标
                        vertices[vertexIndex * 3] = xPos;
                        vertices[vertexIndex * 3 + 1] = yPos;
                        vertices[vertexIndex * 3 + 2] = zPos;

                        // 设置UV坐标 - 使用原始坐标系的比例
                        uvCoords[vertexIndex * 2] = (float)x / (Surface.Width - 1);
                        uvCoords[vertexIndex * 2 + 1] = (float)y / (Surface.Length - 1);


                        // 如果纹理被缩放，需要更新 UV 坐标的计算
                        if (scaleFactorX != 1.0f || scaleFactorY != 1.0f)
                        {
                            for (int i = 0; i < uvCoords.Length / 2; i++)
                            {
                                // 调整 UV 坐标以匹配缩放后的纹理
                                uvCoords[i * 2] = Math.Min(uvCoords[i * 2] * scaleFactorX, 1.0f);
                                uvCoords[i * 2 + 1] = Math.Min(uvCoords[i * 2 + 1] * scaleFactorY, 1.0f);
                            }
                        }


                        vertexIndex++;
                    }
                }

                // 生成纹理 - 使用原始分辨率
                GenerateTexture(gl);

                // 如果是网格模式，生成索引
                if (SurfaceMode == SurfaceMode.Mesh)
                {
                    meshIndexs = GenerateMeshIndexFromSampledPointCloud(sampledWidth, sampledLength);
                }

                // 绑定顶点数据
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices, OpenGL.GL_STATIC_DRAW);

                // 绑定UV坐标
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, uvCoords, OpenGL.GL_STATIC_DRAW);

                pointCloudUpdated = false;
            }

            // 启用纹理
            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

            // 启用顶点和纹理坐标数组
            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.EnableClientState(OpenGL.GL_TEXTURE_COORD_ARRAY);

            // 绑定顶点数据
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
            gl.VertexPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            // 绑定纹理坐标
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
            gl.TexCoordPointer(2, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            // 计算采样后的顶点数量
            int sampledWidth2 = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // 向上取整
            int sampledLength2 = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // 向上取整
            int totalVertices2 = sampledWidth2 * sampledLength2;


            // 根据渲染模式绘制
            if (SurfaceMode == SurfaceMode.PointCloud)
            {
                gl.DrawArrays(OpenGL.GL_POINTS, 0, totalVertices2);
            }
            else if (SurfaceMode == SurfaceMode.Mesh)
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, meshIndexs.Count, meshIndexs.ToArray());
            }

            // 禁用状态
            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.DisableClientState(OpenGL.GL_TEXTURE_COORD_ARRAY);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
        }
        float scaleFactorX = 1.0f;
        float scaleFactorY = 1.0f;
        /// <summary>
        /// 生成纹理 - 考虑最大纹理尺寸限制
        /// </summary>
        private void GenerateTexture(OpenGL gl)
        {
            // 查询最大纹理尺寸
            int[] maxSize = new int[1];
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, maxSize);
            int maxTextureSize = maxSize[0];

            // 确定纹理尺寸，考虑硬件限制
            int textureWidth = Surface.Width;
            int textureHeight = Surface.Length;

            // 如果点云尺寸超过最大纹理尺寸，需要缩放


            if (textureWidth > maxTextureSize)
            {
                scaleFactorX = (float)maxTextureSize / textureWidth;
                textureWidth = maxTextureSize;
            }

            if (textureHeight > maxTextureSize)
            {
                scaleFactorY = (float)maxTextureSize / textureHeight;
                textureHeight = maxTextureSize;
            }

            // 创建纹理图像
            Bitmap textureBitmap = new Bitmap(textureWidth, textureHeight);

            // 生成纹理数据
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // 计算对应的原始点云坐标
                    int srcX = (int)(x / scaleFactorX);
                    int srcY = (int)(y / scaleFactorY);

                    // 确保坐标在有效范围内
                    srcX = Math.Min(srcX, Surface.Width - 1);
                    srcY = Math.Min(srcY, Surface.Length - 1);

                    int index = srcY * Surface.Width + srcX;

                    // 获取高度值
                    float height = 0;
                    if (Surface.Type == SurfaceType.Surface)
                    {
                        height = Surface.Data[index] == -32768 ?
                            float.NegativeInfinity :
                            Surface.ZOffset + Surface.Data[index] * Surface.ZScale;
                    }
                    else if (Surface.Type == SurfaceType.PointCloud)
                    {
                        height = Surface.Data[index * 3 + 2] == -32768 ?
                            float.NegativeInfinity :
                            Surface.ZOffset + Surface.Data[index * 3 + 2] * Surface.ZScale;
                    }

                    // 计算颜色
                    Color pixelColor;

                    if (float.IsInfinity(height))
                    {
                        pixelColor = Color.Black; // 无效点为黑色
                    }
                    else
                    {
                        // 根据高度计算颜色
                        var color = CxExtension.GetColorByHeight(height, ZMin, ZMax);

                        // 应用亮度信息
                        float intensity = 1.0f;
                        if (Surface.Intensity != null && Surface.Intensity.Length > index)
                        {
                            intensity = (float)Surface.Intensity[index] / 255.0f;
                        }

                        if (SurfaceColorMode == SurfaceColorMode.Intensity)
                        {
                            // 纯亮度模式
                            int grayValue = (int)(intensity * 255);
                            pixelColor = Color.FromArgb(grayValue, grayValue, grayValue);
                        }
                        else
                        {
                            // 颜色模式
                            if (surfaceColorMode == SurfaceColorMode.Color)
                            {
                                intensity = 1.0f; // 纯色模式不应用亮度
                            }

                            int r = (int)Math.Min(color.r * intensity * 255, 255);
                            int g = (int)Math.Min(color.g * intensity * 255, 255);
                            int b = (int)Math.Min(color.b * intensity * 255, 255);

                            pixelColor = Color.FromArgb(r, g, b);
                        }
                    }

                    textureBitmap.SetPixel(x, y, pixelColor);
                }
            }

            // 锁定位图数据
            BitmapData bitmapData = textureBitmap.LockBits(
                new Rectangle(0, 0, textureBitmap.Width, textureBitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // 绑定纹理
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

            // 设置纹理参数
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP);

            // 上传纹理数据
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, textureBitmap.Width, textureBitmap.Height,
                0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, bitmapData.Scan0);

            // 解锁位图
            textureBitmap.UnlockBits(bitmapData);
            textureBitmap.Dispose();
        }

        /// <summary>
        /// 为采样后的点云生成网格索引
        /// </summary>
        private List<uint> GenerateMeshIndexFromSampledPointCloud(int sampledWidth, int sampledLength)
        {
            if (sampledWidth <= 1 || sampledLength <= 1)
                return new List<uint>();

            int totalTriangles = (sampledWidth - 1) * (sampledLength - 1) * 2;
            int totalIndices = totalTriangles * 3;

            List<uint> meshIndices = new List<uint>(totalIndices);
            object lockObj = new object();

            // 使用并行循环
            Parallel.For(0, sampledLength - 1, y =>
            {
                uint rowStart = (uint)y * (uint)sampledWidth;
                uint nextRowStart = (uint)(y + 1) * (uint)sampledWidth;

                List<uint> localIndices = new List<uint>();

                for (uint x = 0; x < sampledWidth - 1; x++)
                {
                    uint topLeft = rowStart + x;
                    uint topRight = topLeft + 1;
                    uint bottomLeft = nextRowStart + x;
                    uint bottomRight = bottomLeft + 1;

                    // 添加第一个三角形
                    localIndices.Add(topLeft);
                    localIndices.Add(bottomLeft);
                    localIndices.Add(topRight);

                    // 添加第二个三角形
                    localIndices.Add(topRight);
                    localIndices.Add(bottomLeft);
                    localIndices.Add(bottomRight);
                }

                // 合并结果
                lock (lockObj)
                {
                    meshIndices.AddRange(localIndices);
                }
            });

            return meshIndices;
        }

        /// <summary>
        /// 获取点云的边界
        /// </summary>
        private Box3D? GetBoundingBox()
        {
            if (Surface == null || Surface.Data.Length == 0) return null;

            // 计算点云的边界
            var data = Surface.ToPoints();

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var point in data)
            {
                if (float.IsInfinity(point.X) || float.IsInfinity(point.Y) || float.IsInfinity(point.Z))
                    continue;

                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Z < minZ) minZ = point.Z;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
                if (point.Z > maxZ) maxZ = point.Z;
            }

            // 计算中心点和尺寸
            var center = new CxPoint3D(
                (minX + maxX) / 2,
                (minY + maxY) / 2,
                (minZ + maxZ) / 2
            );

            var size = new CxSize3D(
                maxX - minX,
                maxY - minY,
                maxZ - minZ
            );

            return new Box3D(center, size);
        }

        public void Dispose()
        {
            IsDisposed = true; // 设置释放标记
        }
    }
}