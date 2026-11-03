using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Shader-backed mesh renderer using VAO/VBO resources and GLSL.
    /// Colour is computed per-fragment in the GPU using the <c>colorMin</c>/<c>colorMax</c> uniforms,
    /// so switching colour mode costs only a uniform update — no VBO rebuild required.
    /// </summary>
    public class CxMeshItem : AbstractCxObjRenderItem
    {
        public CxMesh Mesh { get; private set; }

        public CxMeshItem(CxMesh mesh,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
            : base(surfaceMode, surfaceColorMode)
        {
            Mesh = mesh;
            Initialize();
        }

        /// <summary>Computes the local bounding box of the mesh (may be <c>null</c>).</summary>
        protected override CxBox3D? ComputeBoundingBox() => CxExtension.CalculateBoundingBox(Mesh?.Vertices);

        /// <inheritdoc/>
        protected override float[] GetDiffData() => Mesh?.Diff;

        /// <inheritdoc/>
        protected override bool HasSufficientDiffData()
            => Mesh?.Diff != null && Mesh.Diff.Length >= Mesh.Vertices.Length;

        /// <inheritdoc/>
        protected override bool HasRenderableData
            => Mesh?.Vertices != null && Mesh.Vertices.Length > 0;

        /// <inheritdoc/>
        protected override void ClearDataReference() => Mesh = null;

        /// <summary>
        /// A mesh has no per-point grid: the un-projected world coordinate is itself the answer,
        /// and it outranks any grid candidate.
        /// </summary>
        internal override void PickAt(CxPoint3D world, bool useDiff,
            ref float bestDist, ref CxPoint3D? bestPoint, ref float? bestIntensity)
        {
            if (0f < bestDist) { bestDist = 0f; bestPoint = world; bestIntensity = null; }
        }

        protected override RenderData BuildRenderData()
        {
            var vertices = new float[Mesh.Vertices.Length * 3];
            for (int i = 0; i < Mesh.Vertices.Length; i++)
            {
                vertices[i * 3]     = Mesh.Vertices[i].X;
                vertices[i * 3 + 1] = Mesh.Vertices[i].Y;
                vertices[i * 3 + 2] = Mesh.Vertices[i].Z;
            }

            int vertexCount = Mesh.Vertices.Length;
            var meshUvs = Mesh.UVs ?? new CxPoint2D[0];
            bool hasTextureUvs = Mesh.TextureWidth > 0 && Mesh.TextureHeight > 0
                && meshUvs.Length >= vertexCount;
            bool hasVertexIntensity = !hasTextureUvs && Mesh.Intensity != null
                && Mesh.Intensity.Length >= vertexCount;
            int textureWidth = hasTextureUvs ? Mesh.TextureWidth : 1;
            int textureHeight = hasTextureUvs ? Mesh.TextureHeight : 1;
            var uvCoords = new float[vertexCount * 2];
            float[] intensityValues = hasVertexIntensity ? new float[vertexCount] : null;
            for (int i = 0; i < vertexCount; i++)
            {
                if (hasTextureUvs)
                {
                    uvCoords[i * 2] = meshUvs[i].X;
                    uvCoords[i * 2 + 1] = meshUvs[i].Y;
                }
                else if (hasVertexIntensity)
                {
                    uvCoords[i * 2] = 0.5f;
                    uvCoords[i * 2 + 1] = 0.5f;
                    intensityValues[i] = Mesh.Intensity[i] / 255f;
                }
                else
                {
                    uvCoords[i * 2] = 0.5f;
                    uvCoords[i * 2 + 1] = 0.5f;
                }
            }

            byte[] textureBytes = hasTextureUvs
                ? GenerateIntensityTextureData(textureWidth, textureHeight)
                : new byte[] { 255, 255, 255, 255 };
            var indices = CurrentSurfaceMode == SurfaceMode.Mesh ? Mesh.Indices : null;
            // Smoothed normals from the index buffer; only the lit (Mesh) path uses them.
            float[] normals = CxExtension.ComputeTriangleNormals(vertices, indices);

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                UVCoords    = uvCoords,
                Indices     = indices,
                Normals     = normals,
                VertexCount = Mesh.Vertices.Length,
                IndexCount  = indices?.Length ?? 0,
                ShaderSource = new ShaderSource
                {
                    VertexSource   = VertexShaderSource,
                    FragmentSource = FragmentShaderSource,
                },
                TextureData = new TextureData
                {
                    Width  = textureWidth,
                    Height = textureHeight,
                    Data   = textureBytes,
                },
                Uniforms = new Dictionary<string, object>
                {
                    ["colorMin"]      = ColorMin,
                    ["colorMax"]      = ColorMax,
                    ["colorMode"] = EffectiveColorMode,
                    ["surfaceMode"] = CurrentSurfaceMode == SurfaceMode.Mesh ? 1 : 0,
                    ["useIntensityAttribute"] = hasVertexIntensity ? 1 : 0,
                },
                IntensityValues = intensityValues,
            };

            // 惰性填充 DiffValues VBO（只在 Diff 模式且数据充足时）
            if (CurrentSurfaceColorMode == SurfaceColorMode.Diff
                && Mesh?.Diff != null && Mesh.Diff.Length >= Mesh.Vertices.Length)
            {
                _cachedRenderData.DiffValues = new float[Mesh.Vertices.Length];
                Array.Copy(Mesh.Diff, _cachedRenderData.DiffValues, _cachedRenderData.DiffValues.Length);
            }

            return _cachedRenderData;
        }

        private byte[] GenerateIntensityTextureData(int width, int height)
        {
            if (width <= 0 || height <= 0) return new byte[4] { 255, 255, 255, 255 };

            var data = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int si = y * width + x;
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

