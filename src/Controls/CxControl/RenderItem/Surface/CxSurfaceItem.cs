using SharpGL;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxSurfaceItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxSurface Surface { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }

        private SurfaceMode _surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set
            {
                // SurfaceMode 只影响 Draw() 选择 DrawArrays 还是 DrawElements
                // 索引始终预计算，无需重建 GL 资源
                _surfaceMode = value;
            }
        }

        private SurfaceColorMode _surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get => _surfaceColorMode;
            set
            {
                if (_surfaceColorMode != value)
                {
                    _surfaceColorMode = value;
                    // 颜色数组已缓存到 VBO，Mode 变化需重建
                    _cachedRenderData = null;
                    OnRenderDataChanged?.Invoke();
                }
            }
        }

        private RenderData _cachedRenderData;

        public CxSurfaceItem(CxSurface surface,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            Surface = surface;
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;

            BoundingBox = surface?.Data != null && surface.Data.Length > 0
                ? CxExtension.CalculateBoundingBox(surface.ToPoints())
                : null;
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
        }

        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || Surface == null || Surface.Data == null || Surface.Data.Length == 0)
                return null;

            var points = Surface.ToPoints();
            var vertices = new float[points.Length * 3];
            var colors = new float[points.Length * 3];

            for (int i = 0; i < points.Length; i++)
            {
                vertices[i * 3]     = points[i].X;
                vertices[i * 3 + 1] = points[i].Y;
                vertices[i * 3 + 2] = points[i].Z;

                float intensity = 1f;
                if (Surface.Intensity != null && Surface.Intensity.Length > i)
                    intensity = Surface.Intensity[i] / 255f;

                if (_surfaceColorMode == SurfaceColorMode.Intensity)
                {
                    colors[i * 3]     = Math.Min(intensity, 1f);
                    colors[i * 3 + 1] = Math.Min(intensity, 1f);
                    colors[i * 3 + 2] = Math.Min(intensity, 1f);
                }
                else
                {
                    var c = CxExtension.GetColorByHeight(points[i].Z, ZMin, ZMax);
                    float factor = (_surfaceColorMode == SurfaceColorMode.Color) ? 1f : intensity;
                    colors[i * 3]     = Math.Min(c.r * factor, 1f);
                    colors[i * 3 + 1] = Math.Min(c.g * factor, 1f);
                    colors[i * 3 + 2] = Math.Min(c.b * factor, 1f);
                }
            }

            // 始终生成 Mesh 索引，Draw() 按 SurfaceMode 决定是否使用
            uint[] indices = GenerateMeshIndices(Surface.Width, Surface.Length);

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                Colors      = colors,
                Indices     = indices,
                VertexCount = points.Length,
                IndexCount  = indices.Length,
                UseVAO      = false,
            };

            return _cachedRenderData;
        }

        public void Draw(OpenGL gl, GLResourceHandle handle)
        {
            if (!handle.IsValid || IsDisposed) return;
            var data = _cachedRenderData;
            if (data == null) return;

            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.EnableClientState(OpenGL.GL_COLOR_ARRAY);

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[0]);
            gl.VertexPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[1]);
            gl.ColorPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            if (_surfaceMode == SurfaceMode.PointCloud)
            {
                gl.DrawArrays(OpenGL.GL_POINTS, 0, data.VertexCount);
            }
            else if (_surfaceMode == SurfaceMode.Mesh && handle.HasEBO)
            {
                gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, handle.ElementBufferId);
                gl.DrawElements(OpenGL.GL_TRIANGLES, data.IndexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
                gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
            }

            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.DisableClientState(OpenGL.GL_COLOR_ARRAY);
        }

        public void SetGlobalZRange(float zMin, float zMax)
        {
            if (_surfaceColorMode == SurfaceColorMode.Intensity) return;
            if (Math.Abs(ZMin - zMin) < 1e-6f && Math.Abs(ZMax - zMax) < 1e-6f) return;

            ZMin = zMin;
            ZMax = zMax;
            _cachedRenderData = null;
            OnRenderDataChanged?.Invoke();
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            Surface?.Dispose();
            Surface = null;
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
        }

        private uint[] GenerateMeshIndices(int width, int height)
        {
            if (width <= 1 || height <= 1) return new uint[0];

            int total = (width - 1) * (height - 1) * 6;
            var indices = new uint[total];
            int idx = 0;

            // 顺序生成（非并行），保证索引顺序确定性
            for (int y = 0; y < height - 1; y++)
            {
                uint rowStart  = (uint)(y * width);
                uint nextStart = (uint)((y + 1) * width);
                for (uint x = 0; x < width - 1; x++)
                {
                    uint tl = rowStart  + x;
                    uint tr = tl + 1;
                    uint bl = nextStart + x;
                    uint br = bl + 1;
                    indices[idx++] = tl;
                    indices[idx++] = bl;
                    indices[idx++] = tr;
                    indices[idx++] = tr;
                    indices[idx++] = bl;
                    indices[idx++] = br;
                }
            }

            return indices;
        }
    }
}
