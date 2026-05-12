using SharpGL;
using System;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a <see cref="CxMesh"/> using the fixed-function OpenGL pipeline (VBO + colour array).
    /// Colours are baked into the VBO at prepare time using either the height colour map or
    /// the per-vertex intensity channel.
    /// </summary>
    public class CxMeshItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public event Action OnRenderDataChanged;

        public CxMesh Mesh { get; private set; }
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }

        private SurfaceMode _surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get => _surfaceMode;
            set => _surfaceMode = value; // 只影响 Draw() 的绘制调用，无需重建资源
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
                    _cachedRenderData = null;
                    OnRenderDataChanged?.Invoke();
                }
            }
        }

        private RenderData _cachedRenderData;

        public CxMeshItem(CxMesh mesh,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            Mesh = mesh;
            _surfaceMode = surfaceMode;
            _surfaceColorMode = surfaceColorMode;

            BoundingBox = CxExtension.CalculateBoundingBox(mesh?.Vertices);
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
        }

        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || Mesh == null || Mesh.Vertices == null || Mesh.Vertices.Length == 0
                || Mesh.Indices == null || Mesh.Indices.Length == 0)
                return null;

            var vertices = new float[Mesh.Vertices.Length * 3];
            var colors   = new float[Mesh.Vertices.Length * 3];

            for (int i = 0; i < Mesh.Vertices.Length; i++)
            {
                vertices[i * 3]     = Mesh.Vertices[i].X;
                vertices[i * 3 + 1] = Mesh.Vertices[i].Y;
                vertices[i * 3 + 2] = Mesh.Vertices[i].Z;

                float intensity = 1f;
                if (Mesh.Intensity != null)
                {
                    if (Mesh.UVs != null && Mesh.TextureWidth > 0 && Mesh.TextureHeight > 0
                        && i < Mesh.UVs.Length)
                    {
                        int tx = (int)(Mesh.UVs[i].X * (Mesh.TextureWidth  - 1) + 0.5f);
                        int ty = (int)(Mesh.UVs[i].Y * (Mesh.TextureHeight - 1) + 0.5f);
                        int si = ty * Mesh.TextureWidth + tx;
                        if (si < Mesh.Intensity.Length)
                            intensity = Mesh.Intensity[si] / 255f;
                    }
                    else if (Mesh.Intensity.Length > i)
                    {
                        intensity = Mesh.Intensity[i] / 255f;
                    }
                }

                if (_surfaceColorMode == SurfaceColorMode.Intensity)
                {
                    colors[i * 3]     = Math.Min(intensity, 1f);
                    colors[i * 3 + 1] = Math.Min(intensity, 1f);
                    colors[i * 3 + 2] = Math.Min(intensity, 1f);
                }
                else
                {
                    var c = CxExtension.GetColorByHeight(Mesh.Vertices[i].Z, ZMin, ZMax);
                    float factor = (_surfaceColorMode == SurfaceColorMode.Color) ? 1f : intensity;
                    colors[i * 3]     = Math.Min(c.r * factor, 1f);
                    colors[i * 3 + 1] = Math.Min(c.g * factor, 1f);
                    colors[i * 3 + 2] = Math.Min(c.b * factor, 1f);
                }
            }

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                Colors      = colors,
                Indices     = Mesh.Indices,
                VertexCount = Mesh.Vertices.Length,
                IndexCount  = Mesh.Indices.Length,
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
            Mesh = null;
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
        }
    }
}
