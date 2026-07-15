using System;
using System.Threading.Tasks;
using VisionNet.Compute;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Surface reconstruction, mesh conversion and rasterisation operations.
    /// </summary>
    public static partial class VisionOperator
    {
        /// <summary>
        /// Projects a triangle mesh onto a uniform XY height map at the specified
        /// pose and resolution using GPU-accelerated rasterisation.
        /// Grid origin and Z scale are automatically derived from the projected bounding box.
        /// </summary>
        public static CxSurface MeshToSurface(CxMesh mesh, CxMatrix4X4 matrix,
            float xScale = 0.01f, float yScale = 0.01f,
            ProjectionMode mode = ProjectionMode.Max)
        {
            if (mesh == null || matrix == null || mesh.Vertices == null || mesh.Indices == null)
                return null;
            if (xScale <= 0 || yScale <= 0)
                return null;

            using (var projector = new CxMeshToSurface(mesh))
            {
                return projector.Project(matrix, xScale, yScale, mode);
            }
        }

        /// <summary>
        /// Projects a triangle mesh onto a uniform XY height map within a specified bounding box.
        /// </summary>
        public static CxSurface MeshToSurface(CxMesh mesh, CxMatrix4X4 matrix,
            CxBox3D bounds,
            float xScale = 0.01f, float yScale = 0.01f,
            ProjectionMode mode = ProjectionMode.Max)
        {
            if (mesh == null || matrix == null || mesh.Vertices == null || mesh.Indices == null)
                return null;
            if (xScale <= 0 || yScale <= 0 || bounds.Size.Width <= 0 || bounds.Size.Height <= 0)
                return null;

            int width  = Math.Max(1, (int)Math.Ceiling(bounds.Size.Width  / xScale));
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Size.Height / yScale));
            float xOffset = bounds.Center.X - width  * xScale / 2f;
            float yOffset = bounds.Center.Y - height * yScale / 2f;
            float zOffset = bounds.Center.Z;
            float zScale  = Math.Max(bounds.Size.Depth / ushort.MaxValue, 1e-6f);

            using (var projector = new CxMeshToSurface(mesh))
            {
                return projector.Project(matrix,
                    xOffset, yOffset, zOffset,
                    xScale, yScale, zScale,
                    width, height, mode);
            }
        }

        /// <summary>
        /// Converts a <see cref="CxSurface"/> to a mesh.
        /// Each valid quad produces two CCW triangles; invalid cells leave holes.
        /// Intensity data is propagated per-vertex when present.
        /// </summary>
        public static CxMesh SurfaceToMesh(CxSurface surface, bool generateUVs = false)
        {
            if (surface == null || surface.Data == null || surface.Data.Length == 0)
                return null;

            int W = surface.Width, H = surface.Length;
            int total = W * H;

            bool[] valid = new bool[total];
            Parallel.For(0, total, i =>
            {
                valid[i] = surface.Data[i] != short.MinValue;
            });

            int[] indexMap = new int[total];
            int validCount = 0;
            for (int i = 0; i < total; i++)
                indexMap[i] = valid[i] ? validCount++ : -1;

            if (validCount == 0) return null;

            var vertices = new CxPoint3D[validCount];
            var uvs = generateUVs ? new CxPoint2D[validCount] : null;
            bool hasIntensity = surface.Intensity != null && surface.Intensity.Length >= total;
            float uDenom = W > 1 ? W - 1f : 1f;
            float vDenom = H > 1 ? H - 1f : 1f;

            Parallel.For(0, total, i =>
            {
                if (!valid[i]) return;
                int vi = indexMap[i];
                int col = i % W, row = i / W;

                vertices[vi] = new CxPoint3D(
                    surface.XOffset + col * surface.XScale,
                    surface.YOffset + row * surface.YScale,
                    surface.ZOffset + surface.Data[i] * surface.ZScale);

                if (generateUVs)
                    uvs[vi] = new CxPoint2D(col / uDenom, row / vDenom);
            });

            byte[] intensity = null;
            if (hasIntensity)
            {
                if (generateUVs)
                {
                    intensity = new byte[total];
                    Parallel.For(0, total, i =>
                    {
                        intensity[i] = valid[i] ? surface.Intensity[i] : (byte)0;
                    });
                }
                else
                {
                    intensity = new byte[validCount];
                    Parallel.For(0, total, i =>
                    {
                        if (valid[i]) intensity[indexMap[i]] = surface.Intensity[i];
                    });
                }
            }

            var mesh = new CxMesh
            {
                Vertices = vertices,
                Intensity = intensity,
            };

            int quadRowLimit = H - 1;
            int quadColLimit = W - 1;
            uint[] indices = new uint[Math.Max(quadRowLimit, 0) * Math.Max(quadColLimit, 0) * 6];
            int triIdx = 0;

            for (int row = 0; row < quadRowLimit; row++)
            {
                for (int col = 0; col < quadColLimit; col++)
                {
                    int v00 = indexMap[row * W + col];
                    int v01 = indexMap[row * W + col + 1];
                    int v10 = indexMap[(row + 1) * W + col];
                    int v11 = indexMap[(row + 1) * W + col + 1];
                    if (v00 < 0 || v01 < 0 || v10 < 0 || v11 < 0) continue;

                    indices[triIdx++] = (uint)v00;
                    indices[triIdx++] = (uint)v10;
                    indices[triIdx++] = (uint)v11;
                    indices[triIdx++] = (uint)v00;
                    indices[triIdx++] = (uint)v11;
                    indices[triIdx++] = (uint)v01;
                }
            }

            if (triIdx < indices.Length)
            {
                uint[] compact = new uint[triIdx];
                Array.Copy(indices, compact, triIdx);
                indices = compact;
            }
            mesh.Indices = indices;

            if (generateUVs)
            {
                mesh.UVs = uvs;
                mesh.TextureWidth = W;
                mesh.TextureHeight = H;
            }

            return mesh;
        }

        /// <summary>
        /// Converts an ordered point cloud to a mesh.
        /// Each valid quad produces two CCW triangles; invalid cells leave holes.
        /// </summary>
        public static CxMesh PointCloudToMesh(CxPointCloud cloud, bool generateUVs = false)
        {
            if (cloud == null || cloud.Data == null || cloud.Data.Length == 0)
                return null;

            int W = cloud.Width, H = cloud.Length;
            int total = W * H;

            if (cloud.Data.Length < total * 3)
                throw new ArgumentException("Point cloud Data length must be Width × Length × 3.");

            bool[] valid = new bool[total];
            Parallel.For(0, total, i => valid[i] = cloud.Data[i * 3] != short.MinValue);

            int[] indexMap = new int[total];
            int validCount = 0;
            for (int i = 0; i < total; i++)
                indexMap[i] = valid[i] ? validCount++ : -1;

            if (validCount == 0) return null;

            var vertices = new CxPoint3D[validCount];
            var uvs = generateUVs ? new CxPoint2D[validCount] : null;
            bool hasIntensity = cloud.Intensity != null && cloud.Intensity.Length >= total;
            float uDenom = W > 1 ? W - 1f : 1f;
            float vDenom = H > 1 ? H - 1f : 1f;

            Parallel.For(0, total, i =>
            {
                if (!valid[i]) return;
                int vi = indexMap[i];
                int col = i % W, row = i / W;

                vertices[vi] = new CxPoint3D(
                    cloud.XOffset + cloud.Data[i * 3]     * cloud.XScale,
                    cloud.YOffset + cloud.Data[i * 3 + 1] * cloud.YScale,
                    cloud.ZOffset + cloud.Data[i * 3 + 2] * cloud.ZScale);

                if (generateUVs)
                    uvs[vi] = new CxPoint2D(col / uDenom, row / vDenom);
            });

            byte[] intensity = null;
            if (hasIntensity)
            {
                if (generateUVs)
                {
                    intensity = new byte[total];
                    Parallel.For(0, total, i =>
                        intensity[i] = valid[i] ? cloud.Intensity[i] : (byte)0);
                }
                else
                {
                    intensity = new byte[validCount];
                    Parallel.For(0, total, i =>
                    {
                        if (valid[i]) intensity[indexMap[i]] = cloud.Intensity[i];
                    });
                }
            }

            var mesh = new CxMesh
            {
                Vertices = vertices,
                Intensity = intensity,
            };

            int quadRowLimit = H - 1;
            int quadColLimit = W - 1;
            uint[] indices = new uint[Math.Max(quadRowLimit, 0) * Math.Max(quadColLimit, 0) * 6];
            int triIdx = 0;

            for (int row = 0; row < quadRowLimit; row++)
                for (int col = 0; col < quadColLimit; col++)
                {
                    int v00 = indexMap[row * W + col];
                    int v01 = indexMap[row * W + col + 1];
                    int v10 = indexMap[(row + 1) * W + col];
                    int v11 = indexMap[(row + 1) * W + col + 1];
                    if (v00 < 0 || v01 < 0 || v10 < 0 || v11 < 0) continue;

                    indices[triIdx++] = (uint)v00;
                    indices[triIdx++] = (uint)v10;
                    indices[triIdx++] = (uint)v11;
                    indices[triIdx++] = (uint)v00;
                    indices[triIdx++] = (uint)v11;
                    indices[triIdx++] = (uint)v01;
                }

            if (triIdx < indices.Length)
            {
                uint[] compact = new uint[triIdx];
                Array.Copy(indices, compact, triIdx);
                indices = compact;
            }
            mesh.Indices = indices;

            if (generateUVs)
            {
                mesh.UVs = uvs;
                mesh.TextureWidth = W;
                mesh.TextureHeight = H;
            }

            return mesh;
        }

        // ── 预留扩展 ──────────────────────────────────────────────────────────
        // Poisson 曲面重建
        // MarchingCubes 等值面提取
        // 孔洞填充
    }
}
