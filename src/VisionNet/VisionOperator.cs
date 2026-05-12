using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using VisionNet.Compute;
using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Provides static vision-processing operations: point-cloud sampling,
    /// coordinate-frame transformations, bounding-box calculation, and
    /// OpenCL compute lifecycle management.
    /// </summary>
    public static partial class VisionOperator
    {
        // ── OpenCL lifecycle ─────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the shared OpenCL environment (platform + device + command queue).
        /// Must be called once before any GPU-accelerated operation.
        /// </summary>
        /// <returns><c>true</c> if the OpenCL context was created successfully.</returns>
        public static bool InitialLib()
        {
            bool ok = OpenCLEnvironment.Instance.Initialize();
            if (!ok)
                Console.WriteLine("Failed to initialize OpenCL environment.");
            return ok;
        }

        /// <summary>
        /// Releases all OpenCL resources (kernels, programs, command queue, context).
        /// Call once when the application exits.
        /// </summary>
        public static void DestroyLib()
        {
            OpenCLEnvironment.Instance.Cleanup();
        }

        // ── Point-cloud operations (CPU / native) ────────────────────────────────

        /// <summary>
        /// Computes the centroid of a list of 3D points using the native library.
        /// Returns <see cref="float.NegativeInfinity"/> in all axes if the list is empty.
        /// </summary>
        public static CxPoint3D GetPoint3DArrayCenter(List<CxPoint3D> point3Ds)
        {
            var center = new CxPoint3D();
            int ret = GetCenter(point3Ds.ToArray(), point3Ds.Count, ref center);
            if (ret == -1)
                center = new CxPoint3D(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            return center;
        }

        /// <summary>
        /// Re-samples an unordered point cloud onto a uniform (X, Y) grid using the native
        /// library and returns a structured <see cref="CxSurface"/>.
        /// </summary>
        /// <param name="points">Input point positions.</param>
        /// <param name="intensity">Per-point intensity values, or <c>null</c>.</param>
        /// <param name="width">Output grid width (columns).</param>
        /// <param name="height">Output grid height (rows).</param>
        /// <param name="xScale">Grid spacing along X.</param>
        /// <param name="yScale">Grid spacing along Y.</param>
        /// <param name="zScale">Scale factor applied to Z values when encoding as shorts.</param>
        /// <param name="xOffset">World-space X origin of the output grid.</param>
        /// <param name="yOffset">World-space Y origin of the output grid.</param>
        /// <param name="zOffset">World-space Z origin of the output grid.</param>
        public static CxSurface UniformSurface(CxPoint3D[] points, byte[] intensity,
            int width, int height,
            float xScale, float yScale, float zScale,
            float xOffset, float yOffset, float zOffset)
        {
            int gridSize = width * height;
            float[] heightMap    = new float[gridSize];
            byte[]  intensityMap = intensity != null ? new byte[gridSize] : new byte[0];

            UniformGridSample(points, intensity, points.Length,
                xScale, yScale,
                xOffset, xOffset + width  * xScale,
                yOffset, yOffset + height * yScale,
                heightMap, intensityMap, out int mapSize);

            short[] heightData = new short[mapSize];
            for (int i = 0; i < mapSize; i++)
            {
                heightData[i] = (float.IsInfinity(heightMap[i]) || float.IsNaN(heightMap[i]))
                    ? (short)-32768
                    : (short)((heightMap[i] - zOffset) / zScale);
            }

            return new CxSurface(width, height, heightData, intensityMap,
                xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }

        /// <summary>
        /// Transforms a 3D point by a 4×4 column-major matrix (OpenGL convention).
        /// Performs perspective divide when the homogeneous <c>w</c> component is non-zero.
        /// </summary>
        public static CxPoint3D TransformPoint3D(CxPoint3D point, CxMatrix4X4 matrix)
        {
            float[] m = matrix.Data;
            float x = point.X, y = point.Y, z = point.Z;

            float tx = m[0] * x + m[4] * y + m[8]  * z + m[12];
            float ty = m[1] * x + m[5] * y + m[9]  * z + m[13];
            float tz = m[2] * x + m[6] * y + m[10] * z + m[14];
            float tw = m[3] * x + m[7] * y + m[11] * z + m[15];

            if (Math.Abs(tw) > 1e-6f) { tx /= tw; ty /= tw; tz /= tw; }
            return new CxPoint3D(tx, ty, tz);
        }

        // ── Bounding-box (parallel CPU) ──────────────────────────────────────────

        /// <summary>
        /// Computes the axis-aligned bounding box of an array of 3D points using
        /// <see cref="Parallel.ForEach"/> for large arrays.
        /// Returns <c>null</c> if <paramref name="points"/> is empty.
        /// </summary>
        public static Box3D? CalculateBoundingBox(CxPoint3D[] points)
        {
            if (points == null || points.Length == 0) return null;

            var partitioner = Partitioner.Create(points, true);
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            var lockObj = new object();

            Parallel.ForEach(partitioner,
                () => (MinX: float.MaxValue, MaxX: float.MinValue,
                       MinY: float.MaxValue, MaxY: float.MinValue,
                       MinZ: float.MaxValue, MaxZ: float.MinValue),
                (p, _, local) => (
                    Math.Min(local.MinX, p.X), Math.Max(local.MaxX, p.X),
                    Math.Min(local.MinY, p.Y), Math.Max(local.MaxY, p.Y),
                    Math.Min(local.MinZ, p.Z), Math.Max(local.MaxZ, p.Z)),
                local =>
                {
                    lock (lockObj)
                    {
                        if (local.MinX < minX) minX = local.MinX;
                        if (local.MaxX > maxX) maxX = local.MaxX;
                        if (local.MinY < minY) minY = local.MinY;
                        if (local.MaxY > maxY) maxY = local.MaxY;
                        if (local.MinZ < minZ) minZ = local.MinZ;
                        if (local.MaxZ > maxZ) maxZ = local.MaxZ;
                    }
                });

            return new Box3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        /// <summary>
        /// Computes the axis-aligned bounding box of an array of 3D points using
        /// <see cref="Vector3.Min"/> / <see cref="Vector3.Max"/> SIMD instructions.
        /// Returns <c>null</c> if <paramref name="points"/> is empty.
        /// </summary>
        public static Box3D? CalculateBoundingBoxSIMD(CxPoint3D[] points)
        {
            if (points == null || points.Length == 0) return null;

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            for (int i = 0; i < points.Length; i++)
            {
                var v = new Vector3(points[i].X, points[i].Y, points[i].Z);
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            return new Box3D(
                new CxPoint3D((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2),
                new CxSize3D(max.X - min.X, max.Y - min.Y, max.Z - min.Z));
        }

        // ── GPU-accelerated surface operations ───────────────────────────────────

        /// <summary>
        /// Applies a 4×4 transformation matrix to a <see cref="CxSurface"/> on the GPU
        /// (OpenCL) and re-grids the result back into a new <see cref="CxSurface"/>.
        /// </summary>
        /// <param name="surface">Source height-map surface.</param>
        /// <param name="matrix">4×4 column-major transformation matrix.</param>
        /// <param name="sampleMode">
        /// Grid-cell aggregation mode when multiple source points map to the same output cell.
        /// </param>
        /// <returns>Transformed surface, or <c>null</c> if the input is invalid.</returns>
        public static CxSurface TransformSurface(CxSurface surface, CxMatrix4X4 matrix,
            SampleMode sampleMode = SampleMode.Average)
        {
            if (surface == null || matrix == null) return null;

            CxPoint3D[] transformedPoints;
            byte[]      intensities;
            using (var transformer = new CxTransformSurface(matrix))
            {
                (transformedPoints, intensities) = transformer.Transform(surface);
            }

            if (transformedPoints == null || transformedPoints.Length == 0) return null;

            var box = CalculateBoundingBoxSIMD(transformedPoints);
            if (box == null) return null;

            float xScale  = 0.01f;
            float yScale  = 0.01f;
            int   width   = (int)Math.Ceiling(box.Value.Size.Width  / xScale);
            int   height  = (int)Math.Ceiling(box.Value.Size.Height / yScale);
            float xOffset = box.Value.Center.X - width  * xScale / 2f;
            float yOffset = box.Value.Center.Y - height * yScale / 2f;
            float zOffset = box.Value.Center.Z;
            float zScale  = box.Value.Size.Depth / ushort.MaxValue;

            using (var sampler = new CxUniformSurface())
            {
                return sampler.Sample(
                    transformedPoints, intensities,
                    width, height,
                    xScale, yScale, zScale,
                    xOffset, yOffset, zOffset,
                    sampleMode);
            }
        }
        /// <summary>
        /// Projects a triangle mesh onto a uniform XY height map at the specified
        /// pose and resolution using GPU-accelerated rasterisation.
        /// Grid origin and Z scale are automatically derived from the projected bounding box.
        /// </summary>
        /// <param name="mesh">Source triangle mesh.</param>
        /// <param name="matrix">4×4 transformation matrix (world to target local).</param>
        /// <param name="xScale">Grid cell width along X.</param>
        /// <param name="yScale">Grid cell height along Y.</param>
        /// <param name="mode">Rasterisation aggregation mode (Max / Min).</param>
        /// <returns>Height-map surface (<see cref="SurfaceType.Surface"/>), or <c>null</c> on invalid input.</returns>
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
        /// <param name="mesh">Source triangle mesh.</param>
        /// <param name="matrix">4×4 transformation matrix (world to target local).</param>
        /// <param name="bounds">Target XY bounding box (grid origin and Z scale derived from this box).</param>
        /// <param name="xScale">Grid cell width along X.</param>
        /// <param name="yScale">Grid cell height along Y.</param>
        /// <param name="mode">Rasterisation aggregation mode (Max / Min).</param>
        /// <returns>Height-map surface, or <c>null</c> on invalid input.</returns>
        public static CxSurface MeshToSurface(CxMesh mesh, CxMatrix4X4 matrix,
            Box3D bounds,
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
        /// <param name="surface">Source surface. Supports both Surface and PointCloud data layouts.</param>
        /// <param name="generateUVs">
        /// When <c>true</c>, populates <see cref="CxMesh.UVs"/> with normalized UV coordinates
        /// and sets <see cref="CxMesh.TextureWidth"/>/<see cref="CxMesh.TextureHeight"/>.
        /// </param>
        /// <returns>A <see cref="CxMesh"/>, or <c>null</c> if the surface contains no valid cells.</returns>
        public static CxMesh SurfaceToMesh(CxSurface surface, bool generateUVs = false)
        {
            if (surface == null || surface.Data == null || surface.Data.Length == 0)
                return null;

            int W = surface.Width, H = surface.Length;
            int total = W * H;
            bool isPointCloud = surface.Type == SurfaceType.PointCloud;

            if (isPointCloud && surface.Data.Length < total * 3)
                throw new ArgumentException(
                    "PointCloud surface Data length must be Width × Length × 3.");

            // Phase 1a: parallel valid-cell marking
            bool[] valid = new bool[total];
            Parallel.For(0, total, i =>
            {
                valid[i] = isPointCloud
                    ? surface.Data[i * 3] != short.MinValue
                    : surface.Data[i] != short.MinValue;
            });

            // Phase 1b: prefix sum → indexMap
            int[] indexMap = new int[total];
            int validCount = 0;
            for (int i = 0; i < total; i++)
                indexMap[i] = valid[i] ? validCount++ : -1;

            if (validCount == 0) return null;

            // Phase 1c: parallel vertex + UV fill
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

                if (isPointCloud)
                {
                    vertices[vi] = new CxPoint3D(
                        surface.XOffset + surface.Data[i * 3]     * surface.XScale,
                        surface.YOffset + surface.Data[i * 3 + 1] * surface.YScale,
                        surface.ZOffset + surface.Data[i * 3 + 2] * surface.ZScale);
                }
                else
                {
                    vertices[vi] = new CxPoint3D(
                        surface.XOffset + col * surface.XScale,
                        surface.YOffset + row * surface.YScale,
                        surface.ZOffset + surface.Data[i] * surface.ZScale);
                }

                if (generateUVs)
                    uvs[vi] = new CxPoint2D(col / uDenom, row / vDenom);
            });

            // Intensity: compressed per-vertex for CxMeshItem, or full W×H grid for CxMeshAdvancedItem
            byte[] intensity = null;
            if (hasIntensity)
            {
                if (generateUVs)
                {
                    // W×H texture layout: preserve grid positions, fill invalid cells with 0
                    intensity = new byte[total];
                    Parallel.For(0, total, i =>
                    {
                        intensity[i] = valid[i] ? surface.Intensity[i] : (byte)0;
                    });
                }
                else
                {
                    // Compressed per-vertex layout
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

            // Phase 2: quad triangulation (CCW)
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
    }
}
