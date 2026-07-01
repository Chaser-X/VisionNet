using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using VisionNet.DataType;

namespace VisionNet
{
    public static partial class VisionOperator
    {
        // ── File extension constants ──────────────────────────────────────────────

        /// <summary>File extension for <see cref="CxSurface"/> serialization.</summary>
        public const string SurfaceFileExtension    = ".cxsurface";

        /// <summary>File extension for <see cref="CxPointCloud"/> serialization.</summary>
        public const string PointCloudFileExtension = ".cxpc";

        /// <summary>
        /// Default file extension for <see cref="CxMesh"/> binary serialization.
        /// <see cref="LoadMesh"/> and <see cref="SaveMesh"/> also support
        /// <c>.obj</c>, <c>.stl</c> (binary) and <c>.stla</c> (ASCII).
        /// </summary>
        public const string MeshFileExtension       = ".cxmesh";

        // ── CxSurface ────────────────────────────────────────────────────────────

        /// <summary>Serializes a <see cref="CxSurface"/> to a binary file.</summary>
        /// <param name="surface">The surface to save. Must not be null.</param>
        /// <param name="filePath">Destination file path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
        /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
        public static void SaveSurface(CxSurface surface, string filePath)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                writer.Write("CXSRF1".ToCharArray());
                writer.Write(surface.Width);
                writer.Write(surface.Length);
                writer.Write(surface.XOffset);
                writer.Write(surface.YOffset);
                writer.Write(surface.ZOffset);
                writer.Write(surface.XScale);
                writer.Write(surface.YScale);
                writer.Write(surface.ZScale);

                if (surface.Data == null)
                    throw new InvalidOperationException("CxSurface.Data is null.");

                int dataCount = surface.Width * surface.Length;
                writer.Write(dataCount);
                byte[] buf = new byte[dataCount * sizeof(short)];
                Buffer.BlockCopy(surface.Data, 0, buf, 0, buf.Length);
                writer.Write(buf);

                bool hasIntensity = surface.Intensity != null && surface.Intensity.Length > 0;
                writer.Write(hasIntensity ? (byte)1 : (byte)0);
                if (hasIntensity)
                {
                    writer.Write(surface.Intensity.Length);
                    writer.Write(surface.Intensity);
                }
            }
        }

        /// <summary>Deserializes a <see cref="CxSurface"/> from a binary file.</summary>
        /// <param name="filePath">Source file path.</param>
        /// <returns>The loaded surface, or <c>null</c> if the file does not exist.</returns>
        /// <exception cref="InvalidDataException">The file format or magic number is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred while reading the file.</exception>
        public static CxSurface LoadSurface(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string magic = new string(reader.ReadChars(6));
                if (magic != "CXSRF1")
                    throw new InvalidDataException($"Invalid CxSurface file magic: '{magic}'");

                int width   = reader.ReadInt32();
                int length  = reader.ReadInt32();
                float xOffset = reader.ReadSingle();
                float yOffset = reader.ReadSingle();
                float zOffset = reader.ReadSingle();
                float xScale  = reader.ReadSingle();
                float yScale  = reader.ReadSingle();
                float zScale  = reader.ReadSingle();

                int dataCount = reader.ReadInt32();
                if (dataCount != width * length)
                    throw new InvalidDataException(
                        $"CxSurface data count mismatch: expected {width * length}, got {dataCount}.");

                byte[] buf = reader.ReadBytes(dataCount * sizeof(short));
                var data = new short[dataCount];
                Buffer.BlockCopy(buf, 0, data, 0, buf.Length);

                byte[] intensity = null;
                bool hasIntensity = reader.ReadByte() != 0;
                if (hasIntensity)
                {
                    int intensityCount = reader.ReadInt32();
                    intensity = reader.ReadBytes(intensityCount);
                }

                return new CxSurface(width, length, data, intensity,
                    xOffset, yOffset, zOffset, xScale, yScale, zScale);
            }
        }

        // ── CxPointCloud ─────────────────────────────────────────────────────────

        /// <summary>Serializes a <see cref="CxPointCloud"/> to a file.
        /// Supports <c>.cxpc</c> (binary) and <c>.pcd</c> (ASCII) formats.
        /// The format is chosen based on the file extension.</summary>
        /// <param name="cloud">The point cloud to save. Must not be null.</param>
        /// <param name="filePath">Destination file path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="cloud"/> is null.</exception>
        /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
        public static void SavePointCloud(CxPointCloud cloud, string filePath)
        {
            if (cloud == null) throw new ArgumentNullException(nameof(cloud));

            if (string.Equals(Path.GetExtension(filePath), ".pcd", StringComparison.OrdinalIgnoreCase))
            {
                SavePointCloudToPcd(cloud, filePath);
                return;
            }

            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                writer.Write("CXPC01".ToCharArray());
                writer.Write(cloud.Width);
                writer.Write(cloud.Length);
                writer.Write(cloud.XOffset);
                writer.Write(cloud.YOffset);
                writer.Write(cloud.ZOffset);
                writer.Write(cloud.XScale);
                writer.Write(cloud.YScale);
                writer.Write(cloud.ZScale);

                if (cloud.Data == null)
                    throw new InvalidOperationException("CxPointCloud.Data is null.");

                int dataCount = cloud.Width * cloud.Length * 3;
                writer.Write(dataCount);
                byte[] buf = new byte[dataCount * sizeof(short)];
                Buffer.BlockCopy(cloud.Data, 0, buf, 0, buf.Length);
                writer.Write(buf);

                bool hasIntensity = cloud.Intensity != null && cloud.Intensity.Length > 0;
                writer.Write(hasIntensity ? (byte)1 : (byte)0);
                if (hasIntensity)
                {
                    writer.Write(cloud.Intensity.Length);
                    writer.Write(cloud.Intensity);
                }
            }
        }

        /// <summary>Deserializes a <see cref="CxPointCloud"/> from a file.
        /// Supports <c>.cxpc</c> (binary) and <c>.pcd</c> (PCL Point Cloud Data, ASCII and binary) formats.
        /// The format is chosen based on the file extension.</summary>
        /// <param name="filePath">Source file path.</param>
        /// <returns>The loaded point cloud, or <c>null</c> if the file does not exist.</returns>
        /// <exception cref="InvalidDataException">The file format or magic number is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred while reading the file.</exception>
        public static CxPointCloud LoadPointCloud(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            if (string.Equals(Path.GetExtension(filePath), ".pcd", StringComparison.OrdinalIgnoreCase))
                return LoadPointCloudFromPcd(filePath);

            using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string magic = new string(reader.ReadChars(6));
                if (magic != "CXPC01")
                    throw new InvalidDataException($"Invalid CxPointCloud file magic: '{magic}'");

                int width   = reader.ReadInt32();
                int length  = reader.ReadInt32();
                float xOffset = reader.ReadSingle();
                float yOffset = reader.ReadSingle();
                float zOffset = reader.ReadSingle();
                float xScale  = reader.ReadSingle();
                float yScale  = reader.ReadSingle();
                float zScale  = reader.ReadSingle();

                int dataCount = reader.ReadInt32();
                if (dataCount != width * length * 3)
                    throw new InvalidDataException(
                        $"CxPointCloud data count mismatch: expected {width * length * 3}, got {dataCount}.");

                byte[] buf = reader.ReadBytes(dataCount * sizeof(short));
                var data = new short[dataCount];
                Buffer.BlockCopy(buf, 0, data, 0, buf.Length);

                byte[] intensity = null;
                bool hasIntensity = reader.ReadByte() != 0;
                if (hasIntensity)
                {
                    int intensityCount = reader.ReadInt32();
                    intensity = reader.ReadBytes(intensityCount);
                }

                return new CxPointCloud(width, length, data, intensity,
                    xOffset, yOffset, zOffset, xScale, yScale, zScale);
            }
        }

        // ── PCD format (PCL Point Cloud Data) ──────────────────────────────────

        private static void SavePointCloudToPcd(CxPointCloud cloud, string filePath)
        {
            int total = cloud.Width * cloud.Length;
            bool hasIntensity = cloud.Intensity != null && cloud.Intensity.Length == total;
            var inv = CultureInfo.InvariantCulture;

            using (var writer = new StreamWriter(filePath, append: false, new UTF8Encoding(false)))
            {
                writer.WriteLine("# .PCD v0.7 - exported by VisionNet");
                writer.WriteLine("VERSION 0.7");
                writer.WriteLine(hasIntensity ? "FIELDS x y z intensity" : "FIELDS x y z");
                writer.WriteLine(hasIntensity ? "SIZE 4 4 4 4" : "SIZE 4 4 4");
                writer.WriteLine(hasIntensity ? "TYPE F F F F" : "TYPE F F F");
                writer.WriteLine(hasIntensity ? "COUNT 1 1 1 1" : "COUNT 1 1 1");
                writer.WriteLine($"WIDTH {cloud.Width}");
                writer.WriteLine($"HEIGHT {cloud.Length}");
                writer.WriteLine("VIEWPOINT 0 0 0 1 0 0 0");
                writer.WriteLine($"POINTS {total}");
                writer.WriteLine("DATA ascii");

                for (int i = 0; i < total; i++)
                {
                    short sx = cloud.Data[i * 3];
                    short sy = cloud.Data[i * 3 + 1];
                    short sz = cloud.Data[i * 3 + 2];

                    float x = sx == short.MinValue ? float.NaN : cloud.XOffset + sx * cloud.XScale;
                    float y = sy == short.MinValue ? float.NaN : cloud.YOffset + sy * cloud.YScale;
                    float z = sz == short.MinValue ? float.NaN : cloud.ZOffset + sz * cloud.ZScale;

                    if (hasIntensity)
                        writer.WriteLine($"{x.ToString("G9", inv)} {y.ToString("G9", inv)} {z.ToString("G9", inv)} {((float)cloud.Intensity[i]).ToString("G9", inv)}");
                    else
                        writer.WriteLine($"{x.ToString("G9", inv)} {y.ToString("G9", inv)} {z.ToString("G9", inv)}");
                }
            }
        }

        private static CxPointCloud LoadPointCloudFromPcd(string filePath)
        {
            var allLines = File.ReadAllLines(filePath);
            int lineIdx = 0;

            // ── Parse header up to DATA ──
            string dataMode = null;
            string[] fields = null;
            int[] sizes = null;
            char[] types = null;
            int width = 0, height = 0, pointCount = 0;

            for (; lineIdx < allLines.Length; lineIdx++)
            {
                string line = allLines[lineIdx].Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                switch (parts[0])
                {
                    case "FIELDS":
                        fields = parts.Skip(1).ToArray();
                        break;
                    case "SIZE":
                        sizes = parts.Skip(1).Select(int.Parse).ToArray();
                        break;
                    case "TYPE":
                        types = parts.Skip(1).Select(s => s[0]).ToArray();
                        break;
                    case "WIDTH":
                        width = int.Parse(parts[1]);
                        break;
                    case "HEIGHT":
                        height = int.Parse(parts[1]);
                        break;
                    case "POINTS":
                        pointCount = int.Parse(parts[1]);
                        break;
                    case "DATA":
                        dataMode = parts[1];
                        lineIdx++; // move past DATA line
                        goto headerDone;
                }
            }
            headerDone:

            if (fields == null || dataMode == null || pointCount <= 0)
                throw new InvalidDataException("Invalid PCD header: missing FIELDS, DATA, or POINTS.");

            if (dataMode == "binary_compressed")
                throw new NotSupportedException("PCD binary_compressed format is not supported.");

            // ── Locate field indices ──
            int xIdx = -1, yIdx = -1, zIdx = -1, intIdx = -1;
            int xOff = 0, yOff = 0, zOff = 0, intOff = 0;
            int offset = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                string f = fields[i].ToLowerInvariant();
                int size = sizes != null && i < sizes.Length ? sizes[i] : 4;
                if (f == "x") { xIdx = i; xOff = offset; }
                else if (f == "y") { yIdx = i; yOff = offset; }
                else if (f == "z") { zIdx = i; zOff = offset; }
                else if (f == "intensity") { intIdx = i; intOff = offset; }
                offset += size;
            }

            if (xIdx < 0 || yIdx < 0 || zIdx < 0)
                throw new InvalidDataException("PCD file must contain x, y, z fields.");

            bool hasPcdIntensity = intIdx >= 0;
            int pointStride = offset;

            // ── Parse data ──
            var rawX = new float[pointCount];
            var rawY = new float[pointCount];
            var rawZ = new float[pointCount];
            var rawI = hasPcdIntensity ? new float[pointCount] : null;

            if (dataMode == "ascii")
            {
                for (int p = 0; p < pointCount && lineIdx < allLines.Length; p++, lineIdx++)
                {
                    string line = allLines[lineIdx].Trim();
                    if (line.Length == 0 || line[0] == '#') { p--; continue; }

                    string[] vals = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (vals.Length < fields.Length) { p--; continue; }

                    rawX[p] = float.Parse(vals[xIdx], CultureInfo.InvariantCulture);
                    rawY[p] = float.Parse(vals[yIdx], CultureInfo.InvariantCulture);
                    rawZ[p] = float.Parse(vals[zIdx], CultureInfo.InvariantCulture);
                    if (hasPcdIntensity)
                        rawI[p] = float.Parse(vals[intIdx], CultureInfo.InvariantCulture);
                }
            }
            else // binary
            {
                // Scan file byte-by-byte to find the exact offset after DATA line
                long dataStartOffset = 0;
                using (var fs2 = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var lineBuf = new List<byte>(128);
                    int b;
                    while ((b = fs2.ReadByte()) != -1)
                    {
                        if (b == '\n')
                        {
                            string lineStr = Encoding.UTF8.GetString(lineBuf.ToArray()).Trim();
                            lineBuf.Clear();
                            if (lineStr.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                            {
                                dataStartOffset = fs2.Position;
                                break;
                            }
                        }
                        else if (b != '\r')
                        {
                            lineBuf.Add((byte)b);
                        }
                    }
                }

                using (var fs = File.OpenRead(filePath))
                {
                    fs.Seek(dataStartOffset, SeekOrigin.Begin);
                    byte[] pointBuf = new byte[pointStride];
                    for (int p = 0; p < pointCount; p++)
                    {
                        int read = fs.Read(pointBuf, 0, pointStride);
                        if (read < pointStride) break;

                        rawX[p] = DecodePcdField(pointBuf, xOff, sizes[xIdx], types[xIdx]);
                        rawY[p] = DecodePcdField(pointBuf, yOff, sizes[yIdx], types[yIdx]);
                        rawZ[p] = DecodePcdField(pointBuf, zOff, sizes[zIdx], types[zIdx]);
                        if (hasPcdIntensity)
                            rawI[p] = DecodePcdField(pointBuf, intOff, sizes[intIdx], types[intIdx]);
                    }
                }
            }

            // ── Compute encode parameters ──
            float xMin = float.MaxValue, xMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            float zMin = float.MaxValue, zMax = float.MinValue;
            int validCount = 0;

            for (int i = 0; i < pointCount; i++)
            {
                if (float.IsNaN(rawX[i]) || float.IsInfinity(rawX[i]) ||
                    float.IsNaN(rawY[i]) || float.IsInfinity(rawY[i]) ||
                    float.IsNaN(rawZ[i]) || float.IsInfinity(rawZ[i]))
                    continue;
                validCount++;
                if (rawX[i] < xMin) xMin = rawX[i];
                if (rawX[i] > xMax) xMax = rawX[i];
                if (rawY[i] < yMin) yMin = rawY[i];
                if (rawY[i] > yMax) yMax = rawY[i];
                if (rawZ[i] < zMin) zMin = rawZ[i];
                if (rawZ[i] > zMax) zMax = rawZ[i];
            }

            if (validCount == 0) return null;

            float xScale = (xMax - xMin) > 1e-12f ? (xMax - xMin) / 65534f : 1e-6f;
            float yScale = (yMax - yMin) > 1e-12f ? (yMax - yMin) / 65534f : 1e-6f;
            float zScale = (zMax - zMin) > 1e-12f ? (zMax - zMin) / 65534f : 1e-6f;

            // ── Encode ──
            var data = new short[pointCount * 3];
            byte[] intensity = hasPcdIntensity ? new byte[pointCount] : null;

            for (int i = 0; i < pointCount; i++)
            {
                data[i * 3]     = EncodePcdCoord(rawX[i], xMin, xScale);
                data[i * 3 + 1] = EncodePcdCoord(rawY[i], yMin, yScale);
                data[i * 3 + 2] = EncodePcdCoord(rawZ[i], zMin, zScale);

                if (hasPcdIntensity && intensity != null)
                {
                    float fi = rawI[i];
                    if (float.IsNaN(fi) || float.IsInfinity(fi))
                        intensity[i] = 0;
                    else
                        intensity[i] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(fi)));
                }
            }

            return new CxPointCloud(width, height, data, intensity,
                xMin, yMin, zMin, xScale, yScale, zScale);
        }

        private static short EncodePcdCoord(float v, float offset, float scale)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                return short.MinValue;
            return (short)Math.Max(short.MinValue + 1,
                Math.Min(short.MaxValue, (int)Math.Round((v - offset) / scale)));
        }

        private static float DecodePcdField(byte[] buf, int off, int size, char type)
        {
            if (type == 'F' && size == 4) return BitConverter.ToSingle(buf, off);
            if (type == 'U')
            {
                if (size == 1) return buf[off];
                if (size == 2) return BitConverter.ToUInt16(buf, off);
                if (size == 4) return BitConverter.ToUInt32(buf, off);
            }
            if (type == 'I')
            {
                if (size == 1) return (sbyte)buf[off];
                if (size == 2) return BitConverter.ToInt16(buf, off);
                if (size == 4) return BitConverter.ToInt32(buf, off);
            }
            return BitConverter.ToSingle(buf, off);
        }

        // ── CxMesh ───────────────────────────────────────────────────────────────

        /// <summary>Serializes a <see cref="CxMesh"/> to a file.
        /// Supports <c>.cxmesh</c> (binary) and <c>.obj</c> (Wavefront) formats.
        /// The format is chosen based on the file extension.</summary>
        /// <param name="mesh">The mesh to save. Must not be null.</param>
        /// <param name="filePath">Destination file path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="mesh"/> is null.</exception>
        /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
        public static void SaveMesh(CxMesh mesh, string filePath)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));

            string ext = Path.GetExtension(filePath);
            if (string.Equals(ext, ".obj", StringComparison.OrdinalIgnoreCase))
            {
                SaveMeshToObj(mesh, filePath);
                return;
            }
            if (string.Equals(ext, ".stl", StringComparison.OrdinalIgnoreCase))
            {
                SaveMeshToStlBinary(mesh, filePath);
                return;
            }
            if (string.Equals(ext, ".stla", StringComparison.OrdinalIgnoreCase))
            {
                SaveMeshToStlAscii(mesh, filePath);
                return;
            }

            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                writer.Write("CXMSH1".ToCharArray());

                int vertexCount = mesh.Vertices?.Length ?? 0;
                writer.Write(vertexCount);
                if (vertexCount > 0)
                {
                    foreach (var v in mesh.Vertices)
                    {
                        writer.Write(v.X);
                        writer.Write(v.Y);
                        writer.Write(v.Z);
                    }
                }

                bool hasIndices = mesh.Indices != null && mesh.Indices.Length > 0;
                writer.Write(hasIndices ? (byte)1 : (byte)0);
                if (hasIndices)
                {
                    writer.Write(mesh.Indices.Length);
                    byte[] buf = new byte[mesh.Indices.Length * sizeof(uint)];
                    Buffer.BlockCopy(mesh.Indices, 0, buf, 0, buf.Length);
                    writer.Write(buf);
                }

                bool hasIntensity = mesh.Intensity != null && mesh.Intensity.Length > 0;
                writer.Write(hasIntensity ? (byte)1 : (byte)0);
                if (hasIntensity)
                {
                    writer.Write(mesh.Intensity.Length);
                    writer.Write(mesh.Intensity);
                    writer.Write(mesh.TextureWidth);
                    writer.Write(mesh.TextureHeight);
                }

                bool hasUVs = mesh.UVs != null && mesh.UVs.Length > 0;
                writer.Write(hasUVs ? (byte)1 : (byte)0);
                if (hasUVs)
                {
                    writer.Write(mesh.UVs.Length);
                    foreach (var uv in mesh.UVs)
                    {
                        writer.Write(uv.X);
                        writer.Write(uv.Y);
                    }
                }
            }
        }

        /// <summary>Deserializes a <see cref="CxMesh"/> from a file.
        /// Supports <c>.cxmesh</c> (binary) and <c>.obj</c> (Wavefront) formats.
        /// The format is chosen based on the file extension.</summary>
        /// <param name="filePath">Source file path.</param>
        /// <returns>The loaded mesh, or <c>null</c> if the file does not exist.</returns>
        /// <exception cref="InvalidDataException">The file format or magic number is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred while reading the file.</exception>
        public static CxMesh LoadMesh(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            string ext = Path.GetExtension(filePath);
            if (string.Equals(ext, ".obj", StringComparison.OrdinalIgnoreCase))
                return LoadMeshFromObj(filePath);
            if (string.Equals(ext, ".stl", StringComparison.OrdinalIgnoreCase))
                return LoadMeshFromStlBinary(filePath);
            if (string.Equals(ext, ".stla", StringComparison.OrdinalIgnoreCase))
                return LoadMeshFromStlAscii(filePath);

            using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                string magic = new string(reader.ReadChars(6));
                if (magic != "CXMSH1")
                    throw new InvalidDataException($"Invalid CxMesh file magic: '{magic}'");

                var mesh = new CxMesh();

                int vertexCount = reader.ReadInt32();
                if (vertexCount > 0)
                {
                    var vertices = new CxPoint3D[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        vertices[i] = new CxPoint3D(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle());
                    mesh.Vertices = vertices;
                }

                bool hasIndices = reader.ReadByte() != 0;
                if (hasIndices)
                {
                    int indexCount = reader.ReadInt32();
                    byte[] buf = reader.ReadBytes(indexCount * sizeof(uint));
                    var indices = new uint[indexCount];
                    Buffer.BlockCopy(buf, 0, indices, 0, buf.Length);
                    mesh.Indices = indices;
                }

                bool hasIntensity = reader.ReadByte() != 0;
                if (hasIntensity)
                {
                    int intensityCount = reader.ReadInt32();
                    mesh.Intensity = reader.ReadBytes(intensityCount);
                    mesh.TextureWidth  = reader.ReadInt32();
                    mesh.TextureHeight = reader.ReadInt32();
                }

                bool hasUVs = reader.ReadByte() != 0;
                if (hasUVs)
                {
                    int uvCount = reader.ReadInt32();
                    var uvs = new CxPoint2D[uvCount];
                    for (int i = 0; i < uvCount; i++)
                        uvs[i] = new CxPoint2D(
                            reader.ReadSingle(),
                            reader.ReadSingle());
                    mesh.UVs = uvs;
                }

                return mesh;
            }
        }

        // ── Wavefront OBJ loader ────────────────────────────────────────────────

        private static CxMesh LoadMeshFromObj(string filePath)
        {
            var positions = new List<CxPoint3D>();
            var uvCoords  = new List<CxPoint2D>();
            var faces     = new List<(int v, int vt)[]>();

            var invariant = CultureInfo.InvariantCulture;

            foreach (string rawLine in File.ReadLines(filePath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                try
                {
                    switch (parts[0])
                    {
                        case "v":
                            positions.Add(new CxPoint3D(
                                float.Parse(parts[1], invariant),
                                float.Parse(parts[2], invariant),
                                float.Parse(parts[3], invariant)));
                            break;

                        case "vt":
                            uvCoords.Add(new CxPoint2D(
                                float.Parse(parts[1], invariant),
                                parts.Length > 2 ? float.Parse(parts[2], invariant) : 0f));
                            break;

                        case "f":
                            var faceVerts = new (int v, int vt)[parts.Length - 1];
                            for (int i = 1; i < parts.Length; i++)
                            {
                                string[] tokens = parts[i].Split('/');
                                int v = ResolveObjIndex(int.Parse(tokens[0]), positions.Count);
                                int vt = -1;
                                if (tokens.Length > 1 && tokens[1].Length > 0)
                                    vt = ResolveObjIndex(int.Parse(tokens[1]), uvCoords.Count);
                                faceVerts[i - 1] = (v, vt);
                            }
                            faces.Add(faceVerts);
                            break;
                    }
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is IndexOutOfRangeException)
                {
                    throw new InvalidDataException($"Malformed OBJ line: '{rawLine}'", ex);
                }
            }

            if (positions.Count == 0 || faces.Count == 0) return null;

            bool hasUVs = uvCoords.Count > 0 && faces.Any(f => f.Any(c => c.vt >= 0));

            // Build indices (fan triangulation)
            var indices = new List<uint>();
            if (hasUVs)
            {
                var vertDict = new Dictionary<(int v, int vt), int>();
                var outVerts = new List<CxPoint3D>();
                var outUVs   = new List<CxPoint2D>();

                foreach (var face in faces)
                {
                    for (int i = 2; i < face.Length; i++)
                    {
                        AddFaceCorner(face[0], vertDict, outVerts, outUVs, positions, uvCoords, indices);
                        AddFaceCorner(face[i - 1], vertDict, outVerts, outUVs, positions, uvCoords, indices);
                        AddFaceCorner(face[i], vertDict, outVerts, outUVs, positions, uvCoords, indices);
                    }
                }

                return new CxMesh
                {
                    Vertices = outVerts.ToArray(),
                    Indices  = indices.ToArray(),
                    UVs      = outUVs.ToArray(),
                };
            }
            else
            {
                // No UV: share vertices directly
                foreach (var face in faces)
                {
                    for (int i = 2; i < face.Length; i++)
                    {
                        indices.Add((uint)face[0].v);
                        indices.Add((uint)face[i - 1].v);
                        indices.Add((uint)face[i].v);
                    }
                }

                return new CxMesh
                {
                    Vertices = positions.ToArray(),
                    Indices  = indices.ToArray(),
                };
            }
        }

        private static void SaveMeshToObj(CxMesh mesh, string filePath)
        {
            if (mesh.Vertices == null || mesh.Vertices.Length == 0)
                throw new ArgumentException("CxMesh has no vertices.", nameof(mesh));

            bool hasUVs = mesh.UVs != null && mesh.UVs.Length == mesh.Vertices.Length;
            var inv = CultureInfo.InvariantCulture;

            using (var writer = new StreamWriter(filePath, append: false, new UTF8Encoding(false)))
            {
                writer.WriteLine("# Exported by VisionNet");

                foreach (var v in mesh.Vertices)
                    writer.WriteLine($"v {v.X.ToString("G9", inv)} {v.Y.ToString("G9", inv)} {v.Z.ToString("G9", inv)}");

                if (hasUVs)
                    foreach (var uv in mesh.UVs)
                        writer.WriteLine($"vt {uv.X.ToString("G9", inv)} {uv.Y.ToString("G9", inv)}");

                if (mesh.Indices != null && mesh.Indices.Length >= 3)
                {
                    int triCount = mesh.Indices.Length / 3;
                    for (int t = 0; t < triCount; t++)
                    {
                        uint i0 = mesh.Indices[t * 3]     + 1;
                        uint i1 = mesh.Indices[t * 3 + 1] + 1;
                        uint i2 = mesh.Indices[t * 3 + 2] + 1;

                        writer.WriteLine(hasUVs
                            ? $"f {i0}/{i0} {i1}/{i1} {i2}/{i2}"
                            : $"f {i0} {i1} {i2}");
                    }
                }
            }
        }

        private static int ResolveObjIndex(int raw, int count)
        {
            return raw > 0 ? raw - 1 : count + raw;
        }

        private static void AddFaceCorner(
            (int v, int vt) corner,
            Dictionary<(int v, int vt), int> dict,
            List<CxPoint3D> outVerts,
            List<CxPoint2D> outUVs,
            List<CxPoint3D> positions,
            List<CxPoint2D> uvCoords,
            List<uint> indices)
        {
            if (!dict.TryGetValue(corner, out int idx))
            {
                idx = outVerts.Count;
                dict[corner] = idx;
                outVerts.Add(positions[corner.v]);
                outUVs.Add(corner.vt >= 0 ? uvCoords[corner.vt] : new CxPoint2D(0f, 0f));
            }
            indices.Add((uint)idx);
        }

        // ── STL (stereolithography) ──────────────────────────────────────────────

        private static void SaveMeshToStlBinary(CxMesh mesh, string filePath)
        {
            if (mesh.Vertices == null || mesh.Vertices.Length == 0)
                throw new ArgumentException("CxMesh has no vertices.", nameof(mesh));
            if (mesh.Indices == null || mesh.Indices.Length < 3 || mesh.Indices.Length % 3 != 0)
                throw new ArgumentException("CxMesh must have valid triangle indices (multiple of 3).", nameof(mesh));

            int triCount = mesh.Indices.Length / 3;
            var vertices = mesh.Vertices;
            var indices  = mesh.Indices;

            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                writer.Write(new byte[80]);
                writer.Write((uint)triCount);

                for (int t = 0; t < triCount; t++)
                {
                    int i0 = (int)indices[t * 3];
                    int i1 = (int)indices[t * 3 + 1];
                    int i2 = (int)indices[t * 3 + 2];

                    var v0 = vertices[i0];
                    var v1 = vertices[i1];
                    var v2 = vertices[i2];

                    float nx = (v1.Y - v0.Y) * (v2.Z - v0.Z) - (v1.Z - v0.Z) * (v2.Y - v0.Y);
                    float ny = (v1.Z - v0.Z) * (v2.X - v0.X) - (v1.X - v0.X) * (v2.Z - v0.Z);
                    float nz = (v1.X - v0.X) * (v2.Y - v0.Y) - (v1.Y - v0.Y) * (v2.X - v0.X);
                    float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len > 1e-10f) { nx /= len; ny /= len; nz /= len; }

                    writer.Write(nx); writer.Write(ny); writer.Write(nz);
                    writer.Write(v0.X); writer.Write(v0.Y); writer.Write(v0.Z);
                    writer.Write(v1.X); writer.Write(v1.Y); writer.Write(v1.Z);
                    writer.Write(v2.X); writer.Write(v2.Y); writer.Write(v2.Z);
                    writer.Write((ushort)0);
                }
            }
        }

        private static void SaveMeshToStlAscii(CxMesh mesh, string filePath)
        {
            if (mesh.Vertices == null || mesh.Vertices.Length == 0)
                throw new ArgumentException("CxMesh has no vertices.", nameof(mesh));
            if (mesh.Indices == null || mesh.Indices.Length < 3 || mesh.Indices.Length % 3 != 0)
                throw new ArgumentException("CxMesh must have valid triangle indices (multiple of 3).", nameof(mesh));

            int triCount = mesh.Indices.Length / 3;
            var vertices = mesh.Vertices;
            var indices  = mesh.Indices;
            var inv = CultureInfo.InvariantCulture;

            using (var writer = new StreamWriter(filePath, append: false, new UTF8Encoding(false)))
            {
                writer.WriteLine("solid CxMesh");

                for (int t = 0; t < triCount; t++)
                {
                    int i0 = (int)indices[t * 3];
                    int i1 = (int)indices[t * 3 + 1];
                    int i2 = (int)indices[t * 3 + 2];

                    var v0 = vertices[i0];
                    var v1 = vertices[i1];
                    var v2 = vertices[i2];

                    float nx = (v1.Y - v0.Y) * (v2.Z - v0.Z) - (v1.Z - v0.Z) * (v2.Y - v0.Y);
                    float ny = (v1.Z - v0.Z) * (v2.X - v0.X) - (v1.X - v0.X) * (v2.Z - v0.Z);
                    float nz = (v1.X - v0.X) * (v2.Y - v0.Y) - (v1.Y - v0.Y) * (v2.X - v0.X);
                    float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len > 1e-10f) { nx /= len; ny /= len; nz /= len; }

                    writer.WriteLine($"  facet normal {nx.ToString("G9", inv)} {ny.ToString("G9", inv)} {nz.ToString("G9", inv)}");
                    writer.WriteLine("    outer loop");
                    writer.WriteLine($"      vertex {v0.X.ToString("G9", inv)} {v0.Y.ToString("G9", inv)} {v0.Z.ToString("G9", inv)}");
                    writer.WriteLine($"      vertex {v1.X.ToString("G9", inv)} {v1.Y.ToString("G9", inv)} {v1.Z.ToString("G9", inv)}");
                    writer.WriteLine($"      vertex {v2.X.ToString("G9", inv)} {v2.Y.ToString("G9", inv)} {v2.Z.ToString("G9", inv)}");
                    writer.WriteLine("    endloop");
                    writer.WriteLine("  endfacet");
                }

                writer.WriteLine("endsolid CxMesh");
            }
        }

        private static CxMesh LoadMeshFromStlBinary(string filePath)
        {
            using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                reader.ReadBytes(80);
                uint triCount = reader.ReadUInt32();

                var vertices = new CxPoint3D[triCount * 3];
                var indices  = new uint[triCount * 3];

                for (int t = 0; t < triCount; t++)
                {
                    reader.ReadBytes(12);
                    int baseIdx = t * 3;
                    vertices[baseIdx]     = new CxPoint3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    vertices[baseIdx + 1] = new CxPoint3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    vertices[baseIdx + 2] = new CxPoint3D(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    reader.ReadBytes(2);

                    indices[baseIdx]     = (uint)baseIdx;
                    indices[baseIdx + 1] = (uint)(baseIdx + 1);
                    indices[baseIdx + 2] = (uint)(baseIdx + 2);
                }

                return new CxMesh { Vertices = vertices, Indices = indices };
            }
        }

        private static CxMesh LoadMeshFromStlAscii(string filePath)
        {
            var vertList = new List<CxPoint3D>();
            var idxList  = new List<uint>();
            var inv = CultureInfo.InvariantCulture;

            int vertexCountInFacet = 0;

            foreach (string rawLine in File.ReadLines(filePath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts[0] == "vertex" && parts.Length >= 4)
                {
                    vertList.Add(new CxPoint3D(
                        float.Parse(parts[1], inv),
                        float.Parse(parts[2], inv),
                        float.Parse(parts[3], inv)));
                    vertexCountInFacet++;
                }
                else if (parts[0] == "endfacet")
                {
                    if (vertexCountInFacet != 3)
                        throw new InvalidDataException($"Expected 3 vertices per facet, got {vertexCountInFacet}.");
                    int count = vertList.Count;
                    idxList.Add((uint)(count - 3));
                    idxList.Add((uint)(count - 2));
                    idxList.Add((uint)(count - 1));
                    vertexCountInFacet = 0;
                }
            }

            if (vertList.Count == 0 || idxList.Count == 0)
                return null;

            return new CxMesh
            {
                Vertices = vertList.ToArray(),
                Indices  = idxList.ToArray(),
            };
        }
    }
}
