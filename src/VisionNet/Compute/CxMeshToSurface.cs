using OpenCL.Net;
using System;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Compute
{
    /// <summary>
    /// GPU-accelerated mesh-to-height-map projection.
    /// Rasterises a triangle mesh onto a regular XY grid at a given pose and resolution,
    /// producing a structured <see cref="CxSurface"/> height map.
    /// </summary>
    public class CxMeshToSurface : OpenCLComputation
    {
        private const string TransformKernel = "TransformVertices";
        private const string RasterizeKernel = "RasterizeTriangles";

        private readonly CxMesh _mesh;
        private readonly int _vertexCount;
        private readonly int _triangleCount;
        private readonly int _intensityMode;

        private IMem _vertexBuf;
        private IMem _indexBuf;
        private IMem _intensityBuf;    // mode 1: per-vertex; mode 2: texture pixels; mode 0: dummy
        private IMem _uvBuf;           // mode 2: UV coordinates; otherwise dummy
        private readonly object _meshLock = new object();

        public CxMeshToSurface(CxMesh mesh) : base("CxMeshToSurfaceProgram")
        {
            _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
            _vertexCount = mesh.Vertices?.Length ?? 0;
            _triangleCount = (mesh.Indices?.Length ?? 0) / 3;

            if (mesh.UVs != null && mesh.UVs.Length > 0
                && mesh.TextureWidth > 0 && mesh.TextureHeight > 0
                && mesh.Intensity != null && mesh.Intensity.Length > 0)
                _intensityMode = 2;
            else if (mesh.Intensity != null && mesh.Intensity.Length == _vertexCount)
                _intensityMode = 1;
            else
                _intensityMode = 0;
        }

        protected override string[] GetKernelNames() => new[] { TransformKernel, RasterizeKernel };

        protected override string GetKernelSource() =>
            LoadEmbeddedResource("VisionNet.Compute.Kernels.MeshToSurface.cl");

        private void EnsureMeshBuffers()
        {
            if (_vertexBuf != null) return;
            lock (_meshLock)
            {
                if (_vertexBuf != null) return;

                float[] flatVerts = new float[_vertexCount * 3];
                Parallel.For(0, _vertexCount, i =>
                {
                    flatVerts[i * 3]     = _mesh.Vertices[i].X;
                    flatVerts[i * 3 + 1] = _mesh.Vertices[i].Y;
                    flatVerts[i * 3 + 2] = _mesh.Vertices[i].Z;
                });

                _vertexBuf = AllocatePersistent<float>(
                    MemFlags.ReadOnly | MemFlags.CopyHostPtr, flatVerts);
                _indexBuf = AllocatePersistent<uint>(
                    MemFlags.ReadOnly | MemFlags.CopyHostPtr, _mesh.Indices);

                if (_intensityMode == 1)
                {
                    _intensityBuf = AllocatePersistent<byte>(
                        MemFlags.ReadOnly | MemFlags.CopyHostPtr, _mesh.Intensity);
                    _uvBuf = AllocatePersistent<float>(
                        MemFlags.ReadOnly | MemFlags.CopyHostPtr, new float[2]);
                }
                else if (_intensityMode == 2)
                {
                    _intensityBuf = AllocatePersistent<byte>(
                        MemFlags.ReadOnly | MemFlags.CopyHostPtr, _mesh.Intensity);

                    float[] flatUVs = new float[_mesh.UVs.Length * 2];
                    Parallel.For(0, _mesh.UVs.Length, i =>
                    {
                        flatUVs[i * 2]     = _mesh.UVs[i].X;
                        flatUVs[i * 2 + 1] = _mesh.UVs[i].Y;
                    });
                    _uvBuf = AllocatePersistent<float>(
                        MemFlags.ReadOnly | MemFlags.CopyHostPtr, flatUVs);
                }
                else
                {
                    _intensityBuf = AllocatePersistent<byte>(
                        MemFlags.ReadOnly | MemFlags.CopyHostPtr, new byte[1]);
                    _uvBuf = AllocatePersistent<float>(
                        MemFlags.ReadOnly | MemFlags.CopyHostPtr, new float[2]);
                }
            }
        }

        /// <summary>
        /// Transforms the mesh bounding-box corners by the given matrix on CPU
        /// and returns the axis-aligned bounds of the transformed corners.
        /// </summary>
        public CxBox3D? ComputeTransformedBounds(CxMatrix4X4 matrix)
        {
            if (_vertexCount == 0) return null;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            var m = matrix.Data;

            for (int i = 0; i < _vertexCount; i++)
            {
                float x = _mesh.Vertices[i].X;
                float y = _mesh.Vertices[i].Y;
                float z = _mesh.Vertices[i].Z;

                float tx = m[0]*x + m[1]*y + m[2]*z  + m[3];
                float ty = m[4]*x + m[5]*y + m[6]*z  + m[7];
                float tz = m[8]*x + m[9]*y + m[10]*z + m[11];
                float tw = m[12]*x+ m[13]*y+ m[14]*z + m[15];

                if (Math.Abs(tw) > 1e-9f) { tx /= tw; ty /= tw; tz /= tw; }

                if (tx < minX) minX = tx; if (tx > maxX) maxX = tx;
                if (ty < minY) minY = ty; if (ty > maxY) maxY = ty;
                if (tz < minZ) minZ = tz; if (tz > maxZ) maxZ = tz;
            }

            return new CxBox3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        /// <summary>
        /// Projects the mesh onto a uniform XY grid with full control over grid parameters.
        /// </summary>
        public CxSurface Project(
            CxMatrix4X4 matrix,
            float xOffset, float yOffset, float zOffset,
            float xScale,  float yScale,  float zScale,
            int   width,   int   height,
            ProjectionMode mode = ProjectionMode.Max)
        {
            if (width <= 0 || height <= 0 || xScale <= 0 || yScale <= 0 || zScale <= 0)
                throw new ArgumentException("Grid dimensions and scales must be positive.");
            if (_vertexCount == 0 || _triangleCount == 0)
                throw new InvalidOperationException("Mesh has no vertices or indices.");

            int cellCount = width * height;

            int[] heightRaw = new int[cellCount];
            int sentinel = (mode == ProjectionMode.Max) ? int.MinValue : int.MaxValue;
            Parallel.For(0, cellCount, i => heightRaw[i] = sentinel);

            int[] intensityRaw = new int[cellCount];

            if (!EnsureInitialized())
                throw new InvalidOperationException("OpenCL environment could not be initialised.");

            EnsureMeshBuffers();

            float[] matrixData = matrix.Data;
            var matrixBuf = AllocateTransient<float>(
                MemFlags.ReadOnly | MemFlags.CopyHostPtr, matrixData);
            var transformedBuf = AllocateTransientWithSize<float>(
                MemFlags.WriteOnly, _vertexCount * 3);
            var heightBuf = AllocateTransient<int>(
                MemFlags.ReadWrite | MemFlags.CopyHostPtr, heightRaw);
            var intensMapBuf = AllocateTransient<int>(
                MemFlags.ReadWrite | MemFlags.CopyHostPtr, intensityRaw);

            bool ok = true;

            ok &= SetKernelArgs(TransformKernel,
                _vertexBuf, matrixBuf, _vertexCount, transformedBuf);
            ok &= ExecuteKernel(TransformKernel, new[] { new IntPtr(_vertexCount) });

            ok &= SetKernelArgs(RasterizeKernel,
                transformedBuf, _indexBuf, _triangleCount,
                width, height,
                xOffset, yOffset, zOffset, xScale, yScale, zScale,
                heightBuf, intensMapBuf,
                _intensityBuf, _uvBuf,
                _mesh.TextureWidth, _mesh.TextureHeight,
                _intensityMode, (int)mode);
            ok &= ExecuteKernel(RasterizeKernel, new[] { new IntPtr(_triangleCount) });

            ok &= ReadBuffer(heightBuf, heightRaw);

            bool hasIntensity = _intensityMode > 0;
            if (hasIntensity)
                ok &= ReadBuffer(intensMapBuf, intensityRaw);

            ReleaseTransient();

            if (!ok)
                throw new InvalidOperationException("OpenCL kernel execution failed.");

            return BuildSurface(heightRaw, hasIntensity ? intensityRaw : null,
                width, height, xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }

        /// <summary>
        /// Projects the mesh with automatic grid parameter calculation.
        /// Grid origin is centered on the projected bounding box.
        /// </summary>
        public CxSurface Project(
            CxMatrix4X4 matrix,
            float xScale, float yScale,
            ProjectionMode mode = ProjectionMode.Max)
        {
            var bounds = ComputeTransformedBounds(matrix);
            if (!bounds.HasValue)
                throw new InvalidOperationException("Failed to compute transformed bounds.");

            var b = bounds.Value;
            int width  = Math.Max(1, (int)Math.Ceiling(b.Size.Width  / xScale));
            int height = Math.Max(1, (int)Math.Ceiling(b.Size.Height / yScale));
            float xOffset = b.Center.X - width  * xScale / 2f;
            float yOffset = b.Center.Y - height * yScale / 2f;
            float zOffset = b.Center.Z;
            float zScale  = Math.Max(b.Size.Depth / ushort.MaxValue, 1e-6f);

            return Project(matrix, xOffset, yOffset, zOffset,
                xScale, yScale, zScale, width, height, mode);
        }

        private static CxSurface BuildSurface(
            int[] heightMap, int[] intensityMap,
            int width, int height,
            float xOffset, float yOffset, float zOffset,
            float xScale, float yScale, float zScale)
        {
            int cellCount = width * height;
            short[] data = new short[cellCount];
            byte[] intensityOutput = new byte[cellCount];
            bool hasIntensity = intensityMap != null;

            Parallel.For(0, cellCount, i =>
            {
                int raw = heightMap[i];

                // 无三角形覆盖的哨兵，或 Z 超出 Box 范围 → 均标记为无效
                if (raw == int.MinValue || raw == int.MaxValue
                    || raw < (short.MinValue + 1) || raw > short.MaxValue)
                {
                    data[i] = short.MinValue;
                    return;
                }

                data[i] = (short)raw;

                if (hasIntensity)
                    intensityOutput[i] = (byte)Math.Max(0, Math.Min(255, intensityMap[i]));
            });

            return new CxSurface(width, height, data,
                hasIntensity ? intensityOutput : new byte[0],
                xOffset, yOffset, zOffset, xScale, yScale, zScale);
        }
    }
}
