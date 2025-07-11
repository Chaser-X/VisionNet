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
        private uint[] vboIds = new uint[2]; // ���㡢UV����
        private uint textureId = 0; // ����ID
        private bool vboInitialized = false;
        private bool textureInitialized = false;
        private bool pointCloudUpdated = false;
        private List<uint> meshIndexs = new List<uint>();
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }

        // ��󶥵���������
        public int MaxPointCount { get; set; } = int.MaxValue;

        // �������� - ����MaxPointCount��̬����
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

            // �����������
            CalculateSamplingFactors();

            BoundingBox = GetBoundingBox();
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
            pointCloudUpdated = false;
        }

        /// <summary>
        /// �����������
        /// </summary>
        private void CalculateSamplingFactors()
        {
            if (Surface == null || Surface.Width <= 0 || Surface.Length <= 0)
                return;

            int totalPoints = Surface.Width * Surface.Length;

            // ����ܵ���С�����������ƣ�����Ҫ����
            if (totalPoints <= MaxPointCount)
            {
                samplingFactorX = 1;
                samplingFactorY = 1;
                return;
            }

            // ������Ҫ�Ĳ�����
            double samplingRate = Math.Sqrt((double)MaxPointCount / totalPoints);

            // ����X��Y����Ĳ�������
            samplingFactorX = Math.Max(1, (int)(1.0 / samplingRate));
            samplingFactorY = Math.Max(1, (int)(1.0 / samplingRate));

            // ȷ��������ĵ������ᳬ���������
            while ((Surface.Width / samplingFactorX) * (Surface.Length / samplingFactorY) > MaxPointCount)
            {
                if (samplingFactorX <= samplingFactorY)
                    samplingFactorX++;
                else
                    samplingFactorY++;
            }

            // ȷ������Ĳ����ߴ�����ȷ��
            int sampledWidth = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // ����ȡ��
            int sampledLength = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // ����ȡ��

            // �ٴ���֤�ܵ���
            if (sampledWidth * sampledLength > MaxPointCount)
            {
                // �����Ȼ���������Ӳ�������
                double ratio = Math.Sqrt((double)(sampledWidth * sampledLength) / MaxPointCount);
                samplingFactorX = Math.Max(1, (int)(samplingFactorX * ratio));
                samplingFactorY = Math.Max(1, (int)(samplingFactorY * ratio));
            }
        }

        public void Draw(OpenGL gl)
        {
            if (IsDisposed)
            {
                IsDisposed = false; // �����ͷű��
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
                // ���¼����������
                CalculateSamplingFactors();

                // ��������������ߴ�
                int sampledWidth = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // ����ȡ��
                int sampledLength = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // ����ȡ��
                int totalVertices = sampledWidth * sampledLength;

                // ���ɶ����UV����
                float[] vertices = new float[totalVertices * 3];
                float[] uvCoords = new float[totalVertices * 2];

                int vertexIndex = 0;

                for (int y = 0; y < Surface.Length ; y += samplingFactorY)
                {
                    for (int x = 0; x < Surface.Width ; x += samplingFactorX)
                    {
                        int surfaceIndex = y * Surface.Width + x;

                        // ��ȡ��������
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

                        // ���ö�������
                        vertices[vertexIndex * 3] = xPos;
                        vertices[vertexIndex * 3 + 1] = yPos;
                        vertices[vertexIndex * 3 + 2] = zPos;

                        // ����UV���� - ʹ��ԭʼ����ϵ�ı���
                        uvCoords[vertexIndex * 2] = (float)x / (Surface.Width - 1);
                        uvCoords[vertexIndex * 2 + 1] = (float)y / (Surface.Length - 1);


                        // ����������ţ���Ҫ���� UV ����ļ���
                        if (scaleFactorX != 1.0f || scaleFactorY != 1.0f)
                        {
                            for (int i = 0; i < uvCoords.Length / 2; i++)
                            {
                                // ���� UV ������ƥ�����ź������
                                uvCoords[i * 2] = Math.Min(uvCoords[i * 2] * scaleFactorX, 1.0f);
                                uvCoords[i * 2 + 1] = Math.Min(uvCoords[i * 2 + 1] * scaleFactorY, 1.0f);
                            }
                        }


                        vertexIndex++;
                    }
                }

                // �������� - ʹ��ԭʼ�ֱ���
                GenerateTexture(gl);

                // ���������ģʽ����������
                if (SurfaceMode == SurfaceMode.Mesh)
                {
                    meshIndexs = GenerateMeshIndexFromSampledPointCloud(sampledWidth, sampledLength);
                }

                // �󶨶�������
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices, OpenGL.GL_STATIC_DRAW);

                // ��UV����
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, uvCoords, OpenGL.GL_STATIC_DRAW);

                pointCloudUpdated = false;
            }

            // ��������
            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

            // ���ö����������������
            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.EnableClientState(OpenGL.GL_TEXTURE_COORD_ARRAY);

            // �󶨶�������
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
            gl.VertexPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            // ����������
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
            gl.TexCoordPointer(2, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            // ���������Ķ�������
            int sampledWidth2 = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // ����ȡ��
            int sampledLength2 = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // ����ȡ��
            int totalVertices2 = sampledWidth2 * sampledLength2;


            // ������Ⱦģʽ����
            if (SurfaceMode == SurfaceMode.PointCloud)
            {
                gl.DrawArrays(OpenGL.GL_POINTS, 0, totalVertices2);
            }
            else if (SurfaceMode == SurfaceMode.Mesh)
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, meshIndexs.Count, meshIndexs.ToArray());
            }

            // ����״̬
            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.DisableClientState(OpenGL.GL_TEXTURE_COORD_ARRAY);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
        }
        float scaleFactorX = 1.0f;
        float scaleFactorY = 1.0f;
        /// <summary>
        /// �������� - �����������ߴ�����
        /// </summary>
        private void GenerateTexture(OpenGL gl)
        {
            // ��ѯ�������ߴ�
            int[] maxSize = new int[1];
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, maxSize);
            int maxTextureSize = maxSize[0];

            // ȷ������ߴ磬����Ӳ������
            int textureWidth = Surface.Width;
            int textureHeight = Surface.Length;

            // ������Ƴߴ糬���������ߴ磬��Ҫ����


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

            // ��������ͼ��
            Bitmap textureBitmap = new Bitmap(textureWidth, textureHeight);

            // ������������
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // �����Ӧ��ԭʼ��������
                    int srcX = (int)(x / scaleFactorX);
                    int srcY = (int)(y / scaleFactorY);

                    // ȷ����������Ч��Χ��
                    srcX = Math.Min(srcX, Surface.Width - 1);
                    srcY = Math.Min(srcY, Surface.Length - 1);

                    int index = srcY * Surface.Width + srcX;

                    // ��ȡ�߶�ֵ
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

                    // ������ɫ
                    Color pixelColor;

                    if (float.IsInfinity(height))
                    {
                        pixelColor = Color.Black; // ��Ч��Ϊ��ɫ
                    }
                    else
                    {
                        // ���ݸ߶ȼ�����ɫ
                        var color = CxExtension.GetColorByHeight(height, ZMin, ZMax);

                        // Ӧ��������Ϣ
                        float intensity = 1.0f;
                        if (Surface.Intensity != null && Surface.Intensity.Length > index)
                        {
                            intensity = (float)Surface.Intensity[index] / 255.0f;
                        }

                        if (SurfaceColorMode == SurfaceColorMode.Intensity)
                        {
                            // ������ģʽ
                            int grayValue = (int)(intensity * 255);
                            pixelColor = Color.FromArgb(grayValue, grayValue, grayValue);
                        }
                        else
                        {
                            // ��ɫģʽ
                            if (surfaceColorMode == SurfaceColorMode.Color)
                            {
                                intensity = 1.0f; // ��ɫģʽ��Ӧ������
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

            // ����λͼ����
            BitmapData bitmapData = textureBitmap.LockBits(
                new Rectangle(0, 0, textureBitmap.Width, textureBitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // ������
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, textureId);

            // �����������
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP);

            // �ϴ���������
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, textureBitmap.Width, textureBitmap.Height,
                0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, bitmapData.Scan0);

            // ����λͼ
            textureBitmap.UnlockBits(bitmapData);
            textureBitmap.Dispose();
        }

        /// <summary>
        /// Ϊ������ĵ���������������
        /// </summary>
        private List<uint> GenerateMeshIndexFromSampledPointCloud(int sampledWidth, int sampledLength)
        {
            if (sampledWidth <= 1 || sampledLength <= 1)
                return new List<uint>();

            int totalTriangles = (sampledWidth - 1) * (sampledLength - 1) * 2;
            int totalIndices = totalTriangles * 3;

            List<uint> meshIndices = new List<uint>(totalIndices);
            object lockObj = new object();

            // ʹ�ò���ѭ��
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

                    // ��ӵ�һ��������
                    localIndices.Add(topLeft);
                    localIndices.Add(bottomLeft);
                    localIndices.Add(topRight);

                    // ��ӵڶ���������
                    localIndices.Add(topRight);
                    localIndices.Add(bottomLeft);
                    localIndices.Add(bottomRight);
                }

                // �ϲ����
                lock (lockObj)
                {
                    meshIndices.AddRange(localIndices);
                }
            });

            return meshIndices;
        }

        /// <summary>
        /// ��ȡ���Ƶı߽�
        /// </summary>
        private Box3D? GetBoundingBox()
        {
            if (Surface == null || Surface.Data.Length == 0) return null;

            // ������Ƶı߽�
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

            // �������ĵ�ͳߴ�
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
            IsDisposed = true; // �����ͷű��
        }
    }
}