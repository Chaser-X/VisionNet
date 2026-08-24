using SharpGL;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a <see cref="CxSurface"/> as a point cloud or triangle mesh using the
    /// fixed-function OpenGL pipeline (VBO + colour array).
    /// Colours are baked into the VBO at prepare time; changing colour mode or the
    /// global Z range triggers a VBO rebuild via <see cref="ICxObjRenderItem.OnRenderDataChanged"/>.
    /// </summary>
    public class CxSurfaceItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxSurface Surface { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ColorMin { get; set; }
        public float ColorMax { get; set; }
        public float BaseZMin => _baseZMin;
        public float BaseZMax => _baseZMax;
        public float BaseDiffMin => _baseDiffMin;
        public float BaseDiffMax => _baseDiffMax;
        public CxBox3D? BoundingBox { get; private set; }

        /// <summary>
        /// Per-grid-point difference values (<c>float[Width*Length]</c>) for
        /// <see cref="SurfaceColorMode.Diff"/>. <c>null</c>/insufficient → Diff falls back to Color.
        /// Setting a new value invalidates cached render data (colours are re-baked).
        /// </summary>
        public float[] DiffValues
        {
            get => _diffValues;
            set
            {
                _diffValues = value;
                ComputeDiffRange(value);
                if (_surfaceColorMode == SurfaceColorMode.Diff)
                {
                    ColorMin = _baseDiffMin; ColorMax = _baseDiffMax;
                }
                _cachedRenderData = null;
                OnRenderDataChanged?.Invoke();
            }
        }
        private float[] _diffValues;

        /// <summary>
        /// Overrides the diff value range used for colour mapping in Diff mode.
        /// When not called, the range is auto-computed from <see cref="DiffValues"/>.
        /// </summary>
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
                if (value == SurfaceColorMode.Lit) value = SurfaceColorMode.Color;
                if (_surfaceColorMode == value) return;
                var old = _surfaceColorMode;

                // 数据缺失回退
                int gridCount = Surface?.Width * Surface?.Length ?? 0;
                if (value == SurfaceColorMode.Diff
                    && (_diffValues == null || _diffValues.Length < gridCount || gridCount == 0))
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

        public CxSurfaceItem(CxSurface surface,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color,
            float[] diff = null)
        {
            Surface = surface;
            _surfaceMode = surfaceMode;
            // 固定管线不支持 GLSL 光照 → Lit 回退为 Color
            _surfaceColorMode = surfaceColorMode == SurfaceColorMode.Lit
                ? SurfaceColorMode.Color
                : surfaceColorMode;

            BoundingBox = surface?.Data != null && surface.Data.Length > 0
                ? CxExtension.CalculateBoundingBox(surface.ToPoints())
                : null;
            _baseZMax = ColorMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            _baseZMin = ColorMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);

            // 直接赋 backing field，绕过 DiffValues setter 的 invalidate（此时无缓存）。
            _diffValues = diff;
            ComputeDiffRange(diff);
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

                if (_surfaceColorMode == SurfaceColorMode.Diff)
                {
                    // DiffValues 与 ToPoints() 均为 W×L 顺序，1:1 对齐
                    float dv = _diffValues != null && i < _diffValues.Length
                        ? _diffValues[i]
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
            //Surface?.Dispose();
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
