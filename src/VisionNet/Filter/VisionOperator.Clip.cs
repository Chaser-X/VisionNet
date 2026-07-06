using System;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Spatial clipping operations — retains only the data inside a given <see cref="Box3D"/> ROI.
    /// </summary>
    public static partial class VisionOperator
    {
        /// <summary>
        /// Returns a new mesh containing only the triangles whose three vertices all fall inside
        /// <paramref name="roi"/>. Triangles that straddle the boundary are removed entirely.
        /// UV coordinates, per-vertex intensity, and texture dimensions are preserved.
        /// </summary>
        /// <returns>Clipped mesh, or <c>null</c> if the input is invalid or no triangles survive.</returns>
        public static CxMesh ClipMesh(CxMesh mesh, Box3D roi)
        {
            if (mesh == null || mesh.Vertices == null || mesh.Vertices.Length == 0
                || mesh.Indices == null || mesh.Indices.Length < 3)
                return null;

            float minX = roi.Center.X - roi.Size.Width  / 2f;
            float maxX = roi.Center.X + roi.Size.Width  / 2f;
            float minY = roi.Center.Y - roi.Size.Height / 2f;
            float maxY = roi.Center.Y + roi.Size.Height / 2f;
            float minZ = roi.Center.Z - roi.Size.Depth  / 2f;
            float maxZ = roi.Center.Z + roi.Size.Depth  / 2f;

            int vertCount = mesh.Vertices.Length;
            int triTotal  = mesh.Indices.Length / 3;

            // Step 1: parallel vertex inside-test.
            bool[] inside = new bool[vertCount];
            Parallel.For(0, vertCount, i =>
            {
                var v = mesh.Vertices[i];
                inside[i] = InsideBox(v.X, v.Y, v.Z, minX, maxX, minY, maxY, minZ, maxZ);
            });

            // Step 2: parallel triangle test.
            bool[] triKept = new bool[triTotal];
            Parallel.For(0, triTotal, t =>
            {
                triKept[t] = inside[mesh.Indices[t * 3]]
                          && inside[mesh.Indices[t * 3 + 1]]
                          && inside[mesh.Indices[t * 3 + 2]];
            });

            // Step 3: exclusive prefix sum → output position of each kept triangle.
            int[] triStart = new int[triTotal + 1];
            for (int t = 0; t < triTotal; t++)
                triStart[t + 1] = triStart[t] + (triKept[t] ? 1 : 0);
            int keptCount = triStart[triTotal];
            if (keptCount == 0) return null;

            // Step 4: parallel mark which vertices are referenced by surviving triangles.
            // Writing true is idempotent; bool writes are word-atomic on x86/x64.
            bool[] vertUsed = new bool[vertCount];
            Parallel.For(0, triTotal, t =>
            {
                if (!triKept[t]) return;
                vertUsed[mesh.Indices[t * 3]]     = true;
                vertUsed[mesh.Indices[t * 3 + 1]] = true;
                vertUsed[mesh.Indices[t * 3 + 2]] = true;
            });

            // Step 5: sequential prefix sum → compact vertex remap.
            int[] remap = new int[vertCount];
            int newVertCount = 0;
            for (int i = 0; i < vertCount; i++)
                remap[i] = vertUsed[i] ? newVertCount++ : -1;

            bool hasIntensity = mesh.Intensity != null && mesh.Intensity.Length >= vertCount;
            bool hasUVs       = mesh.UVs       != null && mesh.UVs.Length       >= vertCount;

            // Step 6: parallel vertex copy.
            var newVerts     = new CxPoint3D[newVertCount];
            var newIntensity = hasIntensity ? new byte[newVertCount]      : null;
            var newUVs       = hasUVs       ? new CxPoint2D[newVertCount] : null;
            Parallel.For(0, vertCount, i =>
            {
                int ni = remap[i];
                if (ni < 0) return;
                newVerts[ni] = mesh.Vertices[i];
                if (hasIntensity) newIntensity[ni] = mesh.Intensity[i];
                if (hasUVs)       newUVs[ni]       = mesh.UVs[i];
            });

            // Step 7: parallel index write — each triangle knows its output slot from triStart.
            var newIndices = new uint[keptCount * 3];
            Parallel.For(0, triTotal, t =>
            {
                if (!triKept[t]) return;
                int outBase = triStart[t] * 3;
                newIndices[outBase]     = (uint)remap[mesh.Indices[t * 3]];
                newIndices[outBase + 1] = (uint)remap[mesh.Indices[t * 3 + 1]];
                newIndices[outBase + 2] = (uint)remap[mesh.Indices[t * 3 + 2]];
            });

            return new CxMesh
            {
                Vertices      = newVerts,
                Indices       = newIndices,
                Intensity     = newIntensity,
                UVs           = newUVs,
                TextureWidth  = hasUVs ? mesh.TextureWidth  : 0,
                TextureHeight = hasUVs ? mesh.TextureHeight : 0,
            };
        }

        /// <summary>
        /// Returns a new point cloud with the same grid dimensions as <paramref name="cloud"/>.
        /// Points outside <paramref name="roi"/> are replaced with the invalid marker (<c>-32768</c>).
        /// </summary>
        /// <returns>Clipped point cloud, or <c>null</c> if the input is invalid.</returns>
        public static CxPointCloud ClipPointCloud(CxPointCloud cloud, Box3D roi)
        {
            if (cloud == null || cloud.Data == null
                || cloud.Data.Length < cloud.Width * cloud.Length * 3)
                return null;

            float minX = roi.Center.X - roi.Size.Width  / 2f;
            float maxX = roi.Center.X + roi.Size.Width  / 2f;
            float minY = roi.Center.Y - roi.Size.Height / 2f;
            float maxY = roi.Center.Y + roi.Size.Height / 2f;
            float minZ = roi.Center.Z - roi.Size.Depth  / 2f;
            float maxZ = roi.Center.Z + roi.Size.Depth  / 2f;

            int total    = cloud.Width * cloud.Length;
            var outData  = new short[total * 3];
            bool hasIntensity = cloud.Intensity != null && cloud.Intensity.Length >= total;
            var outIntensity  = hasIntensity ? new byte[total] : null;

            // Pre-fill with invalid marker; valid points are copied selectively.
            for (int i = 0; i < outData.Length; i++) outData[i] = short.MinValue;

            Parallel.For(0, total, i =>
            {
                short ix = cloud.Data[i * 3];
                if (ix == short.MinValue) return; // already invalid

                float wx = cloud.XOffset + ix                   * cloud.XScale;
                float wy = cloud.YOffset + cloud.Data[i * 3 + 1] * cloud.YScale;
                float wz = cloud.ZOffset + cloud.Data[i * 3 + 2] * cloud.ZScale;

                if (!InsideBox(wx, wy, wz, minX, maxX, minY, maxY, minZ, maxZ)) return;

                outData[i * 3]     = ix;
                outData[i * 3 + 1] = cloud.Data[i * 3 + 1];
                outData[i * 3 + 2] = cloud.Data[i * 3 + 2];
                if (hasIntensity) outIntensity[i] = cloud.Intensity[i];
            });

            return new CxPointCloud(cloud.Width, cloud.Length, outData, outIntensity,
                cloud.XOffset, cloud.YOffset, cloud.ZOffset,
                cloud.XScale,  cloud.YScale,  cloud.ZScale);
        }

        /// <summary>
        /// Returns a new surface cropped to the XY overlap between <paramref name="surface"/> and
        /// <paramref name="roi"/>. Cells whose Z height falls outside the ROI Z range are
        /// replaced with the invalid marker (<c>-32768</c>).
        /// </summary>
        /// <returns>
        /// Cropped surface with updated <c>Width</c>, <c>Length</c>, <c>XOffset</c>, and
        /// <c>YOffset</c>; or <c>null</c> if the input is invalid or there is no XY overlap.
        /// </returns>
        public static CxSurface ClipSurface(CxSurface surface, Box3D roi)
        {
            if (surface == null || surface.Data == null
                || surface.Data.Length < surface.Width * surface.Length)
                return null;
            if (surface.XScale == 0 || surface.YScale == 0) return null;

            float minX = roi.Center.X - roi.Size.Width  / 2f;
            float maxX = roi.Center.X + roi.Size.Width  / 2f;
            float minY = roi.Center.Y - roi.Size.Height / 2f;
            float maxY = roi.Center.Y + roi.Size.Height / 2f;
            float minZ = roi.Center.Z - roi.Size.Depth  / 2f;
            float maxZ = roi.Center.Z + roi.Size.Depth  / 2f;

            int W = surface.Width, H = surface.Length;

            int colMin = Math.Max(0,     (int)Math.Ceiling((minX - surface.XOffset) / surface.XScale));
            int colMax = Math.Min(W - 1, (int)Math.Floor  ((maxX - surface.XOffset) / surface.XScale));
            int rowMin = Math.Max(0,     (int)Math.Ceiling((minY - surface.YOffset) / surface.YScale));
            int rowMax = Math.Min(H - 1, (int)Math.Floor  ((maxY - surface.YOffset) / surface.YScale));

            if (colMin > colMax || rowMin > rowMax) return null;

            int newW = colMax - colMin + 1;
            int newH = rowMax - rowMin + 1;
            var outData = new short[newW * newH];

            bool hasIntensity = surface.Intensity != null && surface.Intensity.Length >= W * H;
            var outIntensity  = hasIntensity ? new byte[newW * newH] : null;

            Parallel.For(0, newH, r =>
            {
                int srcRow = rowMin + r;
                for (int c = 0; c < newW; c++)
                {
                    int srcIdx = srcRow * W + (colMin + c);
                    int dstIdx = r      * newW + c;

                    short rawZ = surface.Data[srcIdx];
                    if (rawZ == short.MinValue)
                    {
                        outData[dstIdx] = short.MinValue;
                    }
                    else
                    {
                        float wz = surface.ZOffset + rawZ * surface.ZScale;
                        outData[dstIdx] = (wz >= minZ && wz <= maxZ) ? rawZ : short.MinValue;
                    }

                    if (hasIntensity)
                        outIntensity[dstIdx] = (outData[dstIdx] != short.MinValue)
                            ? surface.Intensity[srcIdx]
                            : (byte)0;
                }
            });

            float newXOffset = surface.XOffset + colMin * surface.XScale;
            float newYOffset = surface.YOffset + rowMin * surface.YScale;

            return new CxSurface(newW, newH, outData, outIntensity,
                newXOffset, newYOffset, surface.ZOffset,
                surface.XScale, surface.YScale, surface.ZScale);
        }

        private static bool InsideBox(float x, float y, float z,
            float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
            => x >= minX && x <= maxX
            && y >= minY && y <= maxY
            && z >= minZ && z <= maxZ;
    }
}
