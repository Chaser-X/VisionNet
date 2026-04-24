using SharpGL;
using System;
using VisionNet.DataType;

namespace VisionNet.Controls
{
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

            BoundingBox = CxExtension.CalculateBoundingBox(mesh?.Vertexs);
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
        }

        public RenderData PrepareRenderData()
        {
            if (_cachedRenderData != null) return _cachedRenderData;

            if (IsDisposed || Mesh == null || Mesh.Vertexs == null || Mesh.Vertexs.Length == 0
                || Mesh.Indices == null || Mesh.Indices.Length == 0)
                return null;

            var vertices = new float[Mesh.Vertexs.Length * 3];
            var colors   = new float[Mesh.Vertexs.Length * 3];

            for (int i = 0; i < Mesh.Vertexs.Length; i++)
            {
                vertices[i * 3]     = Mesh.Vertexs[i].X;
                vertices[i * 3 + 1] = Mesh.Vertexs[i].Y;
                vertices[i * 3 + 2] = Mesh.Vertexs[i].Z;

                float intensity = 1f;
                if (Mesh.Intensity != null && Mesh.Intensity.Length > i)
                    intensity = Mesh.Intensity[i] / 255f;

                if (_surfaceColorMode == SurfaceColorMode.Intensity)
                {
                    colors[i * 3]     = Math.Min(intensity, 1f);
                    colors[i * 3 + 1] = Math.Min(intensity, 1f);
                    colors[i * 3 + 2] = Math.Min(intensity, 1f);
                }
                else
                {
                    var c = CxExtension.GetColorByHeight(Mesh.Vertexs[i].Z, ZMin, ZMax);
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
                VertexCount = Mesh.Vertexs.Length,
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
