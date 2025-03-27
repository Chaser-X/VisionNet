using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public enum SurfaceMode
    {
        PointCloud = 1,
        Mesh = 2,
        HeightMap = 4,
        Intensity = 8,
    }
    public class CxSurfaceItem : IRenderItem
    {
        public CxSurface Surface { get; private set; }
        private uint[] vboIds = new uint[2];
        private bool vboInitialized = false;
        private bool pointCloudUpdated = false;
        private List<uint> meshIndexs = new List<uint>();
        public string ID { get; set; }
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
        public CxSurfaceItem(CxSurface surface, SurfaceMode surfaceMode = SurfaceMode.PointCloud)
        {
            this.Surface = surface;
            this.SurfaceMode = surfaceMode;
            BoundingBox = GetBoundingBox();
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Z / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Z / 2);
            pointCloudUpdated = false;
        }
        public void Draw(OpenGL gl)
        {
            if (Surface == null || Surface.Data.Length == 0) return;

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
                if (((int)SurfaceMode & (int)SurfaceMode.Mesh) != 0)
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

                    if (((int)SurfaceMode & (int)SurfaceMode.Intensity) != 0)
                    {
                        colors[i * 3] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 1] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 2] = Math.Min(intensity, 1.0f);
                    }
                    else
                    {
                        var color = CxExtension.GetColorByHeight(vertexs[i].Z, ZMin, ZMax);
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

            if (((int)SurfaceMode & (int)SurfaceMode.PointCloud) != 0)
                gl.DrawArrays(OpenGL.GL_POINTS, 0, Surface.Data.Length);
            else if (((int)SurfaceMode & (int)SurfaceMode.Mesh) != 0)
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
            List<uint> meshIndices = new List<uint>();
            if (pointCloud == null || pointCloud.Data.Length == 0) return meshIndices;
            // 生成索引
            for (uint y = 0; y < pointCloud.Length - 1; y++)
            {
                for (uint x = 0; x < pointCloud.Width - 1; x++)
                {
                    uint topLeft = (uint)(y * pointCloud.Width + x);
                    uint topRight = (uint)(topLeft + 1);
                    uint bottomLeft = (uint)(topLeft + pointCloud.Width);
                    uint bottomRight = (uint)(bottomLeft + 1);

                    // 第一个三角形
                    meshIndices.Add(topLeft);
                    meshIndices.Add(bottomLeft);
                    meshIndices.Add(topRight);

                    // 第二个三角形
                    meshIndices.Add(topRight);
                    meshIndices.Add(bottomLeft);
                    meshIndices.Add(bottomRight);
                }
            }
            return meshIndices;
            // openGLControl.Invalidate(); // 刷新控件以更新显示
        }

        /// <summary>
        /// 获取点云的边界
        /// </summary>
        private Box3D? GetBoundingBox()
        {
            if (Surface == null || Surface.Data.Length == 0) return null;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            // 计算点云的边界
            var data = Surface.ToPoints();
            foreach (var point in data)
            {
                if (!double.IsInfinity(point.Z))
                {
                    if (point.Z < minZ) minZ = point.Z;
                    if (point.Z > maxZ) maxZ = point.Z;
                }
            }
            var x = Surface.XOffset + Surface.Width / 2 * Surface.XScale;
            var y = Surface.YOffset + Surface.Length / 2 * Surface.YScale;
            var z = (float)(maxZ + minZ) / 2;
            var center = new CxPoint3D(x, y, z);

            var size = new CxVector3D(Surface.Width * Surface.XScale, Surface.Length * Surface.YScale, (float)(maxZ - minZ));
            return new Box3D(center, size);
        }
    }
}
