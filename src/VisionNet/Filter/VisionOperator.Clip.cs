using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenCvSharp;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Spatial clipping operations — retains only the data inside a given <see cref="CxBox3D"/> ROI.
    /// </summary>
    public static partial class VisionOperator
    {
        /// <summary>
        /// Returns a new mesh containing only the triangles whose three vertices all fall inside
        /// <paramref name="roi"/>. Triangles that straddle the boundary are removed entirely.
        /// UV coordinates, per-vertex intensity, and texture dimensions are preserved.
        /// </summary>
        /// <returns>Clipped mesh, or <c>null</c> if the input is invalid or no triangles survive.</returns>
        public static CxMesh ClipMesh(CxMesh mesh, CxBox3D roi)
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
        public static CxPointCloud ClipPointCloud(CxPointCloud cloud, CxBox3D roi)
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
        public static CxSurface ClipSurface(CxSurface surface, CxBox3D roi)
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

        /// <summary>
        /// Clips a <see cref="CxImage"/> using a closed 2D polygon as ROI.
        /// Pixels outside the polygon are set to zero.
        /// </summary>
        /// <param name="image">Source image.</param>
        /// <param name="polygon">Closed polygon in pixel coordinates.</param>
        /// <returns>A new <see cref="CxImage"/> with the same dimensions, type, and channel count.</returns>
        public static CxImage ClipImage(CxImage image, CxPolygon2D polygon)
        {
            if (image == null || image.Data == null) return null;
            if (polygon.Points == null || polygon.Points.Length < 3 || !polygon.IsClosed) return null;

            int w = image.Width, h = image.Height, ch = image.Channel;
            int total = w * h * ch;

            var pts = polygon.Points.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();

            var mask = new Mat(h, w, MatType.CV_8UC1, Scalar.All(0));
            Cv2.FillPoly(mask, new[] { pts }, new Scalar(255));

            var mt = GetMatType(image.Type, ch);
            var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
            try
            {
                using (var src = new Mat(h, w, mt, handle.AddrOfPinnedObject()))
                using (var dst = new Mat(h, w, mt, Scalar.All(0)))
                {
                    src.CopyTo(dst, mask);

                    if (image.Type == PlainType.UInt8)
                    {
                        var arr = new byte[total];
                        Marshal.Copy(dst.Data, arr, 0, total);
                        return new CxImage(w, h, arr, ch);
                    }
                    if (image.Type == PlainType.Int16)
                    {
                        var arr = new short[total];
                        Marshal.Copy(dst.Data, arr, 0, total);
                        return new CxImage(w, h, arr, ch);
                    }
                    if (image.Type == PlainType.Int32)
                    {
                        var arr = new int[total];
                        Marshal.Copy(dst.Data, arr, 0, total);
                        return new CxImage(w, h, arr, ch);
                    }
                    var farr = new float[total];
                    Marshal.Copy(dst.Data, farr, 0, total);
                    return new CxImage(w, h, farr, ch);
                }
            }
            finally { handle.Free(); }
        }

        /// <summary>
        /// Clips a <see cref="CxSurface"/> using a closed 2D polygon as ROI (XY only, Z ignored).
        /// Cells outside the polygon are marked invalid (<see cref="short.MinValue"/>).
        /// </summary>
        /// <param name="surface">Source surface.</param>
        /// <param name="polygon">Closed polygon in world coordinates.</param>
        /// <returns>A new <see cref="CxSurface"/> with the same grid dimensions and offsets.</returns>
        public static CxSurface ClipSurface(CxSurface surface, CxPolygon2D polygon)
        {
            if (surface == null || surface.Data == null) return null;
            if (polygon.Points == null || polygon.Points.Length < 3 || !polygon.IsClosed) return null;
            if (surface.XScale == 0 || surface.YScale == 0) return null;

            int W = surface.Width, H = surface.Length;
            int total = W * H;

            var gridPts = polygon.Points.Select(p => new OpenCvSharp.Point(
                (int)Math.Round((p.X - surface.XOffset) / surface.XScale),
                (int)Math.Round((p.Y - surface.YOffset) / surface.YScale))).ToArray();

            var mask = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0));
            Cv2.FillPoly(mask, new[] { gridPts }, new Scalar(255));

            short invalid = short.MinValue;
            var outData = new short[total];

            var srcHandle = GCHandle.Alloc(surface.Data, GCHandleType.Pinned);
            try
            {
                using (var src = new Mat(H, W, MatType.CV_16SC1, srcHandle.AddrOfPinnedObject()))
                using (var dst = new Mat(H, W, MatType.CV_16SC1, new Scalar(invalid)))
                {
                    src.CopyTo(dst, mask);
                    Marshal.Copy(dst.Data, outData, 0, total);
                }
            }
            finally { srcHandle.Free(); }

            byte[] outIntensity = null;
            if (surface.Intensity != null && surface.Intensity.Length >= total)
            {
                outIntensity = new byte[total];
                var intHandle = GCHandle.Alloc(surface.Intensity, GCHandleType.Pinned);
                try
                {
                    using (var srcI = new Mat(H, W, MatType.CV_8UC1, intHandle.AddrOfPinnedObject()))
                    using (var dstI = new Mat(H, W, MatType.CV_8UC1, Scalar.All(0)))
                    {
                        srcI.CopyTo(dstI, mask);
                        Marshal.Copy(dstI.Data, outIntensity, 0, total);
                    }
                }
                finally { intHandle.Free(); }
            }

            return new CxSurface(W, H, outData, outIntensity,
                surface.XOffset, surface.YOffset, surface.ZOffset,
                surface.XScale,   surface.YScale,   surface.ZScale);
        }

        private static MatType GetMatType(PlainType type, int channels)
        {
            if (channels == 1)
                return type == PlainType.UInt8 ? MatType.CV_8UC1
                     : type == PlainType.Int16 ? MatType.CV_16SC1
                     : type == PlainType.Int32 ? MatType.CV_32SC1
                     : MatType.CV_32FC1;
            if (channels == 3)
                return type == PlainType.UInt8 ? MatType.CV_8UC3
                     : type == PlainType.Int16 ? MatType.CV_16SC3
                     : type == PlainType.Int32 ? MatType.CV_32SC3
                     : MatType.CV_32FC3;
            return type == PlainType.UInt8 ? MatType.CV_8UC4
                 : type == PlainType.Int16 ? MatType.CV_16SC4
                 : type == PlainType.Int32 ? MatType.CV_32SC4
                 : MatType.CV_32FC4;
        }

        private static bool InsideBox(float x, float y, float z,
            float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
            => x >= minX && x <= maxX
            && y >= minY && y <= maxY
            && z >= minZ && z <= maxZ;
    }
}
