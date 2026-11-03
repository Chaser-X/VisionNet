using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Shader-backed structured-surface renderer using VAO/VBO resources and an intensity texture.
    /// Supports automatic down-sampling when the point count exceeds <c>maxPointCount</c>.
    /// Colour mode changes update shader uniforms only — no VBO rebuild needed.
    /// </summary>
    public class CxSurfaceItem : AbstractCxObjRenderItem
    {
        public CxSurface Surface { get; private set; }

        private int _samplingFactorX = 1;
        private int _samplingFactorY = 1;

        /// <summary>Upload budget; larger surfaces are down-sampled before upload.</summary>
        public int MaxPointCount
        {
            get => MaxPointCountCore;
            set => MaxPointCountCore = value;
        }

        public CxSurfaceItem(CxSurface surface,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color,
            int maxPointCount = 2_000_000)
            : base(surfaceMode, surfaceColorMode)
        {
            Surface = surface;
            MaxPointCount = maxPointCount;

            CalculateSamplingFactors();
            Initialize();
        }

        /// <summary>Computes the local bounding box of the surface (may be <c>null</c>).</summary>
        protected override CxBox3D? ComputeBoundingBox()
            => Surface?.Data != null && Surface.Data.Length > 0
                ? CxExtension.CalculateBoundingBox(Surface)
                : null;

        /// <inheritdoc/>
        protected override float[] GetDiffData() => Surface?.Diff;

        /// <inheritdoc/>
        protected override bool HasSufficientDiffData()
        {
            int gridCount = Surface?.Width * Surface?.Length ?? 0;
            var diff = Surface?.Diff;
            return diff != null && diff.Length >= gridCount && gridCount > 0;
        }

        /// <inheritdoc/>
        protected override bool HasRenderableData
            => Surface?.Data != null && Surface.Data.Length > 0;

        /// <inheritdoc/>
        protected override void ClearDataReference()
        {
            //Surface?.Dispose();
            Surface = null;
        }

        /// <summary>
        /// Searches the 5×5 grid neighbourhood around the picked cell. The query point is moved
        /// into the item's local frame first, so grid indexing stays correct under a pose.
        /// </summary>
        internal override void PickAt(CxPoint3D world, bool useDiff,
            ref float bestDist, ref CxPoint3D? bestPoint, ref float? bestIntensity)
        {
            if (Surface == null) return;

            var local = ModelMatrixInverse.TransformPoint3D(world);
            if (float.IsNaN(local.X) || float.IsNaN(local.Y) || float.IsNaN(local.Z)) return;

            CxExtension.SearchSurfaceNeighbourhood(Surface, ModelMatrix, local, world, useDiff,
                ref bestDist, ref bestPoint, ref bestIntensity);
        }

        protected override RenderData BuildRenderData()
        {
            CalculateSamplingFactors();

            int sampledWidth  = (Surface.Width  + _samplingFactorX - 1) / _samplingFactorX;
            int sampledLength = (Surface.Length + _samplingFactorY - 1) / _samplingFactorY;
            int totalVertices = sampledWidth * sampledLength;

            var vertices = new float[totalVertices * 3];
            var uvCoords = new float[totalVertices * 2];

            // Diff 模式下按同一采样步长抽样 Surface.Diff（W×L 网格 → 采样顶点 1:1）
            var srcDiff = Surface.Diff;
            bool useDiff = CurrentSurfaceColorMode == SurfaceColorMode.Diff
                && srcDiff != null && srcDiff.Length >= Surface.Width * Surface.Length;
            float[] diffOut = useDiff ? new float[totalVertices] : null;

            int vi = 0;
            for (int y = 0; y < Surface.Length && vi < totalVertices; y += _samplingFactorY)
            {
                for (int x = 0; x < Surface.Width && vi < totalVertices; x += _samplingFactorX)
                {
                    int si = y * Surface.Width + x;

                    float xPos = Surface.XOffset + x * Surface.XScale;
                    float yPos = Surface.YOffset + y * Surface.YScale;
                    float zPos = Surface.Data[si] == -32768
                        ? float.NegativeInfinity
                        : Surface.ZOffset + Surface.Data[si] * Surface.ZScale;

                    vertices[vi * 3]     = xPos;
                    vertices[vi * 3 + 1] = yPos;
                    vertices[vi * 3 + 2] = zPos;
                    // The intensity texture is sampled at the same reduced grid.
                    // Use sampled-grid coordinates so downsampling does not shift
                    // the texture lookup toward its first texel.
                    int sampleX = x / _samplingFactorX;
                    int sampleY = y / _samplingFactorY;
                    uvCoords[vi * 2]     = (float)sampleX / Math.Max(sampledWidth  - 1, 1);
                    uvCoords[vi * 2 + 1] = (float)sampleY / Math.Max(sampledLength - 1, 1);

                    if (diffOut != null)
                        diffOut[vi] = Surface.Data[si] == -32768
                            ? float.NegativeInfinity
                            : srcDiff[si];

                    vi++;
                }
            }

            uint[] indices = CurrentSurfaceMode == SurfaceMode.Mesh
                ? GenerateMeshIndices(sampledWidth, sampledLength)
                : null;
            byte[] textureBytes = GenerateIntensityTextureData();
            // Normals are only needed when the item is lit, i.e. in Mesh mode; point-cloud
            // mode draws unlit points and skips the extra array.
            float[] normals = indices != null
                ? CxExtension.ComputeGridNormals(vertices, sampledWidth, sampledLength)
                : null;

            _cachedRenderData = new RenderData
            {
                Vertices    = vertices,
                UVCoords    = uvCoords,
                Indices     = indices,
                Normals     = normals,
                VertexCount = totalVertices,
                IndexCount  = indices?.Length ?? 0,
                ShaderSource = new ShaderSource
                {
                    VertexSource   = VertexShaderSource,
                    FragmentSource = FragmentShaderSource,
                },
                TextureData = new TextureData
                {
                    Width  = sampledWidth,
                    Height = sampledLength,
                    Data   = textureBytes,
                },
                Uniforms = new Dictionary<string, object>
                {
                    ["colorMin"]      = ColorMin,
                    ["colorMax"]      = ColorMax,
                    ["colorMode"] = EffectiveColorMode,
                    ["surfaceMode"] = CurrentSurfaceMode == SurfaceMode.Mesh ? 1 : 0,
                    ["useIntensityAttribute"] = 0,
                },
            };

            if (diffOut != null)
                _cachedRenderData.DiffValues = diffOut;

            return _cachedRenderData;
        }

        private byte[] GenerateIntensityTextureData()
        {
            int w = (Surface.Width + _samplingFactorX - 1) / _samplingFactorX;
            int h = (Surface.Length + _samplingFactorY - 1) / _samplingFactorY;
            var data = new byte[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int sourceX = Math.Min(x * _samplingFactorX, Surface.Width - 1);
                    int sourceY = Math.Min(y * _samplingFactorY, Surface.Length - 1);
                    int si = sourceY * Surface.Width + sourceX;
                    byte v = 255;
                    if (Surface.Intensity != null && Surface.Intensity.Length > si)
                        v = Surface.Intensity[si];
                    int di = (y * w + x) * 4;
                    data[di] = data[di + 1] = data[di + 2] = data[di + 3] = v;
                }
            }

            return data;
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
                    // Counter-clockwise seen from +Z, so face normals point up like
                    // CxExtension.ComputeGridNormals (the two conventions used to disagree).
                    indices[idx++] = tl;
                    indices[idx++] = tr;
                    indices[idx++] = bl;
                    indices[idx++] = tr;
                    indices[idx++] = br;
                    indices[idx++] = bl;
                }
            }

            return indices;
        }

        private void CalculateSamplingFactors()
        {
            if (Surface == null || Surface.Width <= 0 || Surface.Length <= 0) return;

            int total = Surface.Width * Surface.Length;
            if (total <= MaxPointCount)
            {
                _samplingFactorX = _samplingFactorY = 1;
                return;
            }

            double rate = Math.Sqrt((double)MaxPointCount / total);
            _samplingFactorX = Math.Max(1, (int)(1.0 / rate));
            _samplingFactorY = Math.Max(1, (int)(1.0 / rate));

            while ((((long)Surface.Width + _samplingFactorX - 1) / _samplingFactorX)
                 * (((long)Surface.Length + _samplingFactorY - 1) / _samplingFactorY)
                 > MaxPointCount)
            {
                if (_samplingFactorX <= _samplingFactorY) _samplingFactorX++;
                else _samplingFactorY++;
            }
        }
    }
}

