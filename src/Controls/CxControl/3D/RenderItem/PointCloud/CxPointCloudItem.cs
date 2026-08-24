using SharpGL;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a <see cref="CxPointCloud"/> as a point cloud or triangle mesh using the
    /// fixed-function OpenGL pipeline (VBO + colour array).
    /// </summary>
    public class CxPointCloudItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxPointCloud PointCloud { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ColorMin { get; set; }
        public float ColorMax { get; set; }
        public float BaseZMin => _baseZMin;
        public float BaseZMax => _baseZMax;
        public float BaseDiffMin => _baseDiffMin;
        public float BaseDiffMax => _baseDiffMax;
        public CxBox3D? BoundingBox { get; private set; }

        /// <summary>
        /// Per-frame diff range propagation from <see cref="CxDisplay"/>. Only applies in
        /// Diff mode; fixed-function path must re-bake colours.
        /// </summary>
        public void SetGlobalDiffRange(float min, float max)
        {
            if (_surfaceColorMode != SurfaceColorMode.Diff) return;
            if (Math.Abs(ColorMin - min) < 1e-6f && Math.Abs(ColorMax - max) < 1e-6f) return;

            ColorMin = min;
            ColorMax = max;
            _cachedRenderData = null;
            OnRenderDataChanged?.Invoke();
        }

        private SurfaceMode _surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set => _surfaceMode = value;
        }

        private SurfaceColorMode _surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get => _surfaceColorMode;
            set
            {
                if (value == SurfaceColorMode.Lit) value = SurfaceColorMode.Color;
                if (_surfaceColorMode == value) return;
                var old = _surfaceColorMode;

                // 数据缺失回退
                int gridCount = PointCloud?.Width * PointCloud?.Length ?? 0;
                var pcDiff = PointCloud?.Diff;
                if (value == SurfaceColorMode.Diff
                    && (pcDiff == null || pcDiff.Length < gridCount || gridCount == 0))
                {
                    _surfaceColorMode = SurfaceColorMode.Color;
                    value = SurfaceColorMode.Color;
                }
                else
                {
                    _surfaceColorMode = value;
                }

                // 范围切换
                if (value == SurfaceColorMode.Diff)
                {
                    ColorMin = _baseDiffMin; ColorMax = _baseDiffMax;
                }
                else if (old == SurfaceColorMode.Diff)
                {
                    ColorMin = _baseZMin; ColorMax = _baseZMax;
                }

                // 固定管线一律重建缓存（CPU 颜色烘焙）
                _cachedRenderData = null;
                OnRenderDataChanged?.Invoke();
            }
        }

        private RenderData _cachedRenderData;

        // 构造时一次性预计算的范围缓存
        private float _baseZMin, _baseZMax;
        private float _baseDiffMin, _baseDiffMax;

        public CxPointCloudItem(CxPointCloud pointCloud,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            PointCloud = pointCloud;
            _surfaceMode = surfaceMode;
            // 固定管线不支持 GLSL 光照 → Lit 回退为 Color
            _surfaceColorMode = surfaceColorMode == SurfaceColorMode.Lit
                ? SurfaceColorMode.Color
                : surfaceColorMode;

            BoundingBox = pointCloud?.Data != null && pointCloud.Data.Length > 0
                ? CxExtension.CalculateBoundingBox(pointCloud.ToPoints())
                : null;
            _baseZMax = ColorMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            _baseZMin = ColorMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);

            ComputeDiffRange(pointCloud?.Diff);
        }

        private void ComputeDiffRange(float[] diffData)
        {
            if (diffData == null || diffData.Length == 0)
            {
                _baseDiffMin = 0f; _baseDiffMax = 0f;
                return;
            }
            float dmin = float.MaxValue, dmax = float.MinValue;
            foreach (var d in diffData)
            {
                if (float.IsInfinity(d) || float.IsNaN(d)) continue;
                if (d < dmin) dmin = d;
                if (d > dmax) dmax = d;
            }
            if (dmax - dmin < 1e-6f) dmax = dmin + 1e-6f;
            _baseDiffMin = dmin; _baseDiffMax = dmax;
        }

        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || PointCloud == null || PointCloud.Data == null || PointCloud.Data.Length == 0)
                return null;

            var points = PointCloud.ToPoints();
            var vertices = new float[points.Length * 3];
            var colors = new float[points.Length * 3];

            for (int i = 0; i < points.Length; i++)
            {
                vertices[i * 3]     = points[i].X;
                vertices[i * 3 + 1] = points[i].Y;
                vertices[i * 3 + 2] = points[i].Z;

                float intensity = 1f;
                if (PointCloud.Intensity != null && PointCloud.Intensity.Length > i)
                    intensity = PointCloud.Intensity[i] / 255f;

                if (_surfaceColorMode == SurfaceColorMode.Diff)
                {
                    // PointCloud.Diff 与 ToPoints() 均为 W×L 顺序，1:1 对齐
                    var diff = PointCloud.Diff;
                    float dv = diff != null && i < diff.Length
                        ? diff[i]
                        : points[i].Z;
                    var c = CxExtension.GetColorByHeight(dv, ColorMin, ColorMax);
                    colors[i * 3]     = c.r;
                    colors[i * 3 + 1] = c.g;
                    colors[i * 3 + 2] = c.b;
                }
                else if (_surfaceColorMode == SurfaceColorMode.Intensity)
                {
                    colors[i * 3]     = Math.Min(intensity, 1f);
                    colors[i * 3 + 1] = Math.Min(intensity, 1f);
                    colors[i * 3 + 2] = Math.Min(intensity, 1f);
                }
                else
                {
                    var c = CxExtension.GetColorByHeight(points[i].Z, ColorMin, ColorMax);
                    float factor = (_surfaceColorMode == SurfaceColorMode.Color) ? 1f : intensity;
                    colors[i * 3]     = Math.Min(c.r * factor, 1f);
                    colors[i * 3 + 1] = Math.Min(c.g * factor, 1f);
                    colors[i * 3 + 2] = Math.Min(c.b * factor, 1f);
                }
            }

            uint[] indices = GenerateMeshIndices(PointCloud.Width, PointCloud.Length);

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
            if (_surfaceColorMode == SurfaceColorMode.Diff) return;
            if (Math.Abs(ColorMin - zMin) < 1e-6f && Math.Abs(ColorMax - zMax) < 1e-6f) return;

            ColorMin = zMin;
            ColorMax = zMax;
            _cachedRenderData = null;
            OnRenderDataChanged?.Invoke();
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            //PointCloud?.Dispose();
            PointCloud = null;
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
