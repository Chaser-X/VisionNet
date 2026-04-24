using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxMeshAdvancedItem : ICxObjRenderItem
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
            set => _surfaceMode = value;
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
                    if (_cachedRenderData?.Uniforms != null)
                        _cachedRenderData.Uniforms["colorMode"] = (int)value;
                }
            }
        }

        private RenderData _cachedRenderData;

        public CxMeshAdvancedItem(CxMesh mesh,
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

            if (IsDisposed || Mesh == null || Mesh.Vertexs == null || Mesh.Vertexs.Length == 0)
                return null;

            var vertices = new float[Mesh.Vertexs.Length * 3];
            for (int i = 0; i < Mesh.Vertexs.Length; i++)
            {
                vertices[i * 3]     = Mesh.Vertexs[i].X;
                vertices[i * 3 + 1] = Mesh.Vertexs[i].Y;
                vertices[i * 3 + 2] = Mesh.Vertexs[i].Z;
            }

            var uvCoords = new float[Mesh.UVs.Length * 2];
            for (int i = 0; i < Mesh.UVs.Length; i++)
            {
                uvCoords[i * 2]     = Mesh.UVs[i].X;
                uvCoords[i * 2 + 1] = Mesh.UVs[i].Y;
            }

            byte[] textureBytes = GenerateIntensityTextureData();

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                UVCoords    = uvCoords,
                Indices     = Mesh.Indices,
                VertexCount = Mesh.Vertexs.Length,
                IndexCount  = Mesh.Indices?.Length ?? 0,
                UseVAO      = true,
                ShaderSource = new ShaderSource
                {
                    VertexSource   = CxSurfaceAdvancedItem.VertexShaderSource,
                    FragmentSource = CxSurfaceAdvancedItem.FragmentShaderSource,
                },
                TextureData = new TextureData
                {
                    Width  = Mesh.TextureWidth,
                    Height = Mesh.TextureHeight,
                    Data   = textureBytes,
                },
                Uniforms = new Dictionary<string, object>
                {
                    ["zMin"]      = ZMin,
                    ["zMax"]      = ZMax,
                    ["colorMode"] = (int)_surfaceColorMode,
                },
            };

            return _cachedRenderData;
        }

        public void Draw(OpenGL gl, GLResourceHandle handle)
        {
            if (!handle.IsValid || IsDisposed) return;
            var data = _cachedRenderData;
            if (data == null) return;

            gl.UseProgram(handle.ShaderProgram);
            gl.BindVertexArray(handle.VaoId);

            float[] proj = new float[16];
            float[] view = new float[16];
            gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, proj);
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, view);
            gl.UniformMatrix4(gl.GetUniformLocation(handle.ShaderProgram, "view"),       1, false, view);
            gl.UniformMatrix4(gl.GetUniformLocation(handle.ShaderProgram, "projection"), 1, false, proj);

            foreach (var kv in data.Uniforms)
            {
                int loc = gl.GetUniformLocation(handle.ShaderProgram, kv.Key);
                if (kv.Value is float f) gl.Uniform1(loc, f);
                else if (kv.Value is int i) gl.Uniform1(loc, i);
            }

            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, handle.TextureId);
            gl.Uniform1(gl.GetUniformLocation(handle.ShaderProgram, "intensityTexture"), 0);

            if (_surfaceMode == SurfaceMode.PointCloud)
                gl.DrawArrays(OpenGL.GL_POINTS, 0, data.VertexCount);
            else if (_surfaceMode == SurfaceMode.Mesh)
                gl.DrawElements(OpenGL.GL_TRIANGLES, data.IndexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.BindVertexArray(0);
            gl.UseProgram(0);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            Mesh = null;
            _cachedRenderData = null;
            IsDisposed = true;
            OnDisposed?.Invoke();
        }

        private byte[] GenerateIntensityTextureData()
        {
            int w = Mesh.TextureWidth;
            int h = Mesh.TextureHeight;
            if (w <= 0 || h <= 0) return new byte[4] { 255, 255, 255, 255 };

            var data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int si = y * w + x;
                    byte v = 255;
                    if (Mesh.Intensity != null && Mesh.Intensity.Length > si)
                        v = Mesh.Intensity[si];
                    int di = si * 4;
                    data[di] = data[di + 1] = data[di + 2] = data[di + 3] = v;
                }
            }

            return data;
        }
    }
}
