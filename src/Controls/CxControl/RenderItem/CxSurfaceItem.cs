using SharpGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxSurfaceItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public CxSurface Surface { get; private set; }
        private uint[] vboIds = new uint[2];
        private bool vboInitialized = false;
        private bool pointCloudUpdated = false;
        private List<uint> meshIndexs = new List<uint>();
        public bool IsDisposed { get; private set; } = false; // 标记是否释放资源
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }
        private SurfaceMode surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get
            {
                return surfaceMode;
            }
            set
            {
                pointCloudUpdated = value != surfaceMode;
                surfaceMode = value;
            }
        }
        private SurfaceColorMode surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get
            {
                return surfaceColorMode;
            }
            set
            {
                pointCloudUpdated = value != surfaceColorMode;
                surfaceColorMode = value;
            }
        }
        public CxSurfaceItem(CxSurface surface, SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            this.Surface = surface;
            this.SurfaceMode = surfaceMode;
            this.SurfaceColorMode = surfaceColorMode;
            BoundingBox = GetBoundingBox();
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
            pointCloudUpdated = false;
        }
        public void Draw(OpenGL gl)
        {
            if (IsDisposed)
            {
                IsDisposed = false; // 重置释放标记
                if (vboInitialized)
                {
                    gl.DeleteBuffers(2, vboIds);
                    vboInitialized = false;
                }
                if (Surface != null)
                    Surface.Dispose();
                Surface = null;
                OnDisposed?.Invoke(); // 触发释放事件
                IsDisposed = true; // 重置释放标记
                Debug.WriteLine("SurfaceItem disposed");
            }
            if (Surface == null ) return;
            if (Surface.Data.Length == 0) return;

            if (vboInitialized && pointCloudUpdated)
            {
                gl.DeleteBuffers(2, vboIds);
                vboInitialized = false;
            }

            if (!vboInitialized)
            {
                gl.GenBuffers(2, vboIds);
                vboInitialized = true;
                pointCloudUpdated = true; // 初次初始化时标记为已更新
            }

            if (pointCloudUpdated)
            {
                float[] vertices = null;
                float[] colors = null;
                CxPoint3D[] vertexs = Surface.ToPoints();
                if (SurfaceMode == SurfaceMode.Mesh)
                {
                    meshIndexs = GenerateMeshIndexFromPointCloud(Surface);
                }
                vertices = new float[vertexs.Length * 3];
                colors = new float[vertexs.Length * 3];

                for (int i = 0; i < vertexs.Length; i++)
                {
                    vertices[i * 3] = (float)vertexs[i].X;
                    vertices[i * 3 + 1] = (float)vertexs[i].Y;
                    vertices[i * 3 + 2] = (float)vertexs[i].Z;

                    float intensity = 1;
                    if (Surface.Intensity.Length == 0)
                    {
                        intensity = 1;
                    }
                    else
                    {
                        intensity = (float)Surface.Intensity[i] / 255.0f; // 亮度因子
                    }

                    if (SurfaceColorMode == SurfaceColorMode.Intensity)
                    {
                        colors[i * 3] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 1] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 2] = Math.Min(intensity, 1.0f);
                    }
                    else
                    {
                        var color = CxExtension.GetColorByHeight(vertexs[i].Z, ZMin, ZMax);
                        if (surfaceColorMode == SurfaceColorMode.Color)
                        {
                            intensity = 1;
                        }
                        colors[i * 3] = Math.Min(color.r * intensity, 1.0f);
                        colors[i * 3 + 1] = Math.Min(color.g * intensity, 1.0f);
                        colors[i * 3 + 2] = Math.Min(color.b * intensity, 1.0f);
                    }
                }

                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices, OpenGL.GL_STATIC_DRAW);

                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, colors, OpenGL.GL_STATIC_DRAW);
                pointCloudUpdated = false; // 重置标记
            }

            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.EnableClientState(OpenGL.GL_COLOR_ARRAY);

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
            gl.VertexPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
            gl.ColorPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            if (SurfaceMode == SurfaceMode.PointCloud)
                gl.DrawArrays(OpenGL.GL_POINTS, 0, Surface.Data.Length);
            else if (SurfaceMode == SurfaceMode.Mesh)
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, meshIndexs.Count, meshIndexs.ToArray());
            }

            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.DisableClientState(OpenGL.GL_COLOR_ARRAY);
        }
        /// <summary>
        /// 生成点云网格索引
        /// </summary>
        private List<uint> GenerateMeshIndexFromPointCloud(CxSurface pointCloud)
        {
            if (pointCloud == null || pointCloud.Data.Length == 0)
                return new List<uint>();

            int totalTriangles = (pointCloud.Width - 1) * (pointCloud.Length - 1) * 2;
            int totalIndices = totalTriangles * 3;

            List<uint> meshIndices = new List<uint>(totalIndices);
            object lockObj = new object();

            // 使用并行循环
            Parallel.For(0, pointCloud.Length - 1, y =>
            {
                uint rowStart = (uint)y * (uint)pointCloud.Width;
                uint nextRowStart = (uint)(y + 1) * (uint)pointCloud.Width;

                List<uint> localIndices = new List<uint>();

                for (uint x = 0; x < pointCloud.Width - 1; x++)
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
