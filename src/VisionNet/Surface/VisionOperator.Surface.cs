using System;
using System.Collections.Generic;
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
        /// Merges several <see cref="CxSurface"/>s (each at its own pose) into a single
        /// <see cref="CxPointCloud"/> arranged as a regular grid so it can be meshed via
        /// <see cref="PointCloudToMesh"/> / <see cref="SurfaceToMesh"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>Layout</b>: surfaces are laid out side-by-side along X (columns). The grid
        /// height is <c>max(Length_i)</c>; the width is <c>Σ W_i + (N-1)</c> — one separator
        /// column of invalid points is inserted between adjacent surface blocks so that mesh
        /// rasterisation breaks at block boundaries (no spurious triangles connecting
        /// different surfaces). Rows beyond a block's own <c>Length</c> are also invalid.</para>
        /// <para><b>Simple merge</b>: no overlap handling, no deduplication, no resampling.
        /// <b>Ordered</b>: block order follows <paramref name="surfaces"/>; within a block the
        /// row-major order of the source surface is preserved.</para>
        /// <para><b>Quantisation</b>: the merged bounding box of all pose-transformed valid
        /// points is mapped to <c>short</c> with <c>scale = span / 65534</c> per axis.
        /// <see cref="CxSurface.Intensity"/> and <see cref="CxSurface.Diff"/> are carried over
        /// per-point (0 where a surface lacks the channel or at separator/out-of-range cells).</para>
        /// </remarks>
        /// <param name="surfaces">The surfaces to merge. Order defines block order.</param>
        /// <param name="poses">Optional per-surface pose matrices; <c>null</c> uses identity for
        /// every surface. If non-null, its length must equal <paramref name="surfaces"/>.</param>
        /// <returns>The merged point cloud as a regular grid, or <c>null</c> if no valid points.</returns>
        /// <exception cref="ArgumentException"><paramref name="poses"/> length mismatches
        /// <paramref name="surfaces"/>.</exception>
        public static CxPointCloud MergeSurfacesToPointCloud(
            IList<CxSurface> surfaces,
            IList<CxMatrix4X4> poses = null)
        {
            if (surfaces == null || surfaces.Count == 0) return null;
            if (poses != null && poses.Count != surfaces.Count)
                throw new ArgumentException(
                    $"poses count ({poses.Count}) must match surfaces count ({surfaces.Count}).",
                    nameof(poses));

            int n = surfaces.Count;

            // ── Collect non-empty blocks (preserve input order). Empty/null surfaces are dropped. ──
            var blocks = new List<(CxSurface surface, CxMatrix4X4 pose)>();
            for (int i = 0; i < n; i++)
            {
                var s = surfaces[i];
                if (s == null || s.Data == null || s.Width <= 0 || s.Length <= 0) continue;
                var pose = poses != null ? poses[i] : null;
                blocks.Add((s, pose));
            }
            if (blocks.Count == 0) return null;
            int nb = blocks.Count;

            // ── Grid geometry: width = Σ W_i + (nb-1) separator cols; height = max Length_i ──
            int hMax = 0, totalW = 0;
            for (int i = 0; i < nb; i++)
            {
                if (blocks[i].surface.Length > hMax) hMax = blocks[i].surface.Length;
                totalW += blocks[i].surface.Width;
            }
            totalW += Math.Max(0, nb - 1); // separator columns between blocks
            if (hMax <= 0 || totalW <= 0) return null;

            // colStart[i] = first column index of block i; one separator after each block except the last.
            var colStart = new int[nb];
            int cursor = 0;
            for (int i = 0; i < nb; i++)
            {
                colStart[i] = cursor;
                cursor += blocks[i].surface.Width;
                if (i < nb - 1) cursor += 1; // separator column
            }

            // ── First pass: transform valid points, collect world-space coords + BBox ──
            // Reads surface.Data directly (row-major short heights) instead of ToPoints(), to
            // avoid an extra full-array copy. Transformed coords are kept per block in row-major
            // order so the second pass preserves ordering. Invalid cells (short.MinValue) map to
            // a NaN sentinel and are skipped.
            var blockPts = new CxPoint3D[nb][];
            float xMin = float.MaxValue, xMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            float zMin = float.MaxValue, zMax = float.MinValue;
            int validTotal = 0;

            for (int i = 0; i < nb; i++)
            {
                var s = blocks[i].surface;
                var pose = blocks[i].pose;
                int w = s.Width, h = s.Length;
                var srcData = s.Data;
                var xformed = new CxPoint3D[w * h];

                for (int row = 0; row < h; row++)
                {
                    float y = s.YOffset + row * s.YScale;
                    for (int col = 0; col < w; col++)
                    {
                        int srcIdx = row * w + col;
                        short dz = srcData[srcIdx];
                        if (dz == short.MinValue)
                        {
                            xformed[srcIdx] = new CxPoint3D(float.NaN, float.NaN, float.NaN);
                            continue;
                        }

                        var p = new CxPoint3D(
                            s.XOffset + col * s.XScale,
                            y,
                            s.ZOffset + dz * s.ZScale);
                        var tp = pose != null ? pose.TransformPoint3D(p) : p;
                        xformed[srcIdx] = tp;
                        validTotal++;

                        if (tp.X < xMin) xMin = tp.X;
                        if (tp.X > xMax) xMax = tp.X;
                        if (tp.Y < yMin) yMin = tp.Y;
                        if (tp.Y > yMax) yMax = tp.Y;
                        if (tp.Z < zMin) zMin = tp.Z;
                        if (tp.Z > zMax) zMax = tp.Z;
                    }
                }
                blockPts[i] = xformed;
            }

            if (validTotal == 0) return null;

            // ── Quantisation parameters ──
            // Map each axis span onto the full short range [-32767, 32767] (65534 steps) centred
            // at the bbox midpoint. Using offset = min with scale = span/65534 would push the upper
            // half past short.MaxValue and clamp it — flattening the upper part of the cloud.
            // Centring at mid keeps q in [-32767, 32767] and reserves short.MinValue (-32768) for
            // the invalid sentinel.
            float xSpan = xMax - xMin, ySpan = yMax - yMin, zSpan = zMax - zMin;
            float xScale = xSpan > 1e-12f ? xSpan / 65534f : 1e-6f;
            float yScale = ySpan > 1e-12f ? ySpan / 65534f : 1e-6f;
            float zScale = zSpan > 1e-12f ? zSpan / 65534f : 1e-6f;
            float xOffset = (xMin + xMax) * 0.5f;
            float yOffset = (yMin + yMax) * 0.5f;
            float zOffset = (zMin + zMax) * 0.5f;

            // ── Second pass: write quantised short data + intensity + diff into the merged grid ──
            // Default every cell to invalid; then fill in each block's valid points. Separator
            // columns and out-of-range rows stay invalid (skipped by SurfaceToMesh/PointCloudToMesh).
            var data = new short[totalW * hMax * 3];
            var intensity = new byte[totalW * hMax];
            var diff = new float[totalW * hMax];
            for (int i = 0; i < data.Length; i++) data[i] = short.MinValue;

            for (int i = 0; i < nb; i++)
            {
                var xformed = blockPts[i];
                if (xformed == null) continue;

                var s = blocks[i].surface;
                int w = s.Width, h = s.Length;
                int cs = colStart[i];
                byte[] sIntensity = s.Intensity;
                float[] sDiff = s.Diff;
                bool hasIntensity = sIntensity != null && sIntensity.Length >= w * h;
                bool hasDiff = sDiff != null && sDiff.Length >= w * h;

                for (int row = 0; row < h; row++)
                {
                    for (int col = 0; col < w; col++)
                    {
                        int srcIdx = row * w + col;
                        int dstCol = cs + col;
                        int dstIdx = row * totalW + dstCol; // merged grid is row-major

                        var p = xformed[srcIdx];
                        if (float.IsNaN(p.X))
                            continue; // leave invalid (already short.MinValue)

                        data[dstIdx * 3]     = Quantize(p.X, xOffset, xScale);
                        data[dstIdx * 3 + 1] = Quantize(p.Y, yOffset, yScale);
                        data[dstIdx * 3 + 2] = Quantize(p.Z, zOffset, zScale);
                        if (hasIntensity) intensity[dstIdx] = sIntensity[srcIdx];
                        if (hasDiff)     diff[dstIdx]     = sDiff[srcIdx];
                    }
                }
            }

            var cloud = new CxPointCloud(totalW, hMax, data, intensity,
                xOffset, yOffset, zOffset, xScale, yScale, zScale);
            cloud.Diff = diff;
            return cloud;
        }

        /// <summary>Quantises a world-space coordinate to a short grid cell.</summary>
        private static short Quantize(float value, float offset, float scale)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return short.MinValue;
            int q = (int)Math.Round((value - offset) / scale);
            if (q < short.MinValue + 1) return (short)(short.MinValue + 1);
            if (q > short.MaxValue) return short.MaxValue;
            return (short)q;
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

        /// <summary>
        /// Creates a grayscale <see cref="CxImage"/> from a <see cref="CxSurface"/>.
        /// </summary>
        /// <param name="surface">Source surface data.</param>
        /// <param name="pixelOffset">
        /// <c>true</c> → output <see cref="PlainType.Real"/> with world-space Z values (ZOffset + Data×ZScale);<br/>
        /// <c>false</c> → output <see cref="PlainType.Int16"/> with raw surface Data.
        /// </param>
        /// <param name="image">Output image, or <c>null</c> if <paramref name="surface"/> is null.</param>
        public static void CreateImageFromSurface(CxSurface surface, bool pixelOffset, out CxImage image)
        {
            if (surface == null || surface.Data == null)
            {
                image = null;
                return;
            }

            int w = surface.Width;
            int h = surface.Length;
            int n = w * h;

            if (pixelOffset)
            {
                var pixels = new float[n];
                for (int i = 0; i < n; i++)
                    pixels[i] = surface.Data[i] == short.MinValue
                        ? float.NaN
                        : surface.ZOffset + surface.Data[i] * surface.ZScale;
                image = new CxImage(w, h, pixels, 1);
            }
            else
            {
                var pixels = new short[n];
                Array.Copy(surface.Data, pixels, n);
                image = new CxImage(w, h, pixels, 1);
            }
        }
    }
}
