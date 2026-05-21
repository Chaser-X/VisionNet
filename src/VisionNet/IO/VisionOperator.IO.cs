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
        /// <see cref="LoadMesh"/> and <see cref="SaveMesh"/> also support <c>.obj</c>.
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

        /// <summary>Serializes a <see cref="CxPointCloud"/> to a binary file.</summary>
        /// <param name="cloud">The point cloud to save. Must not be null.</param>
        /// <param name="filePath">Destination file path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="cloud"/> is null.</exception>
        /// <exception cref="IOException">An I/O error occurred while writing the file.</exception>
        public static void SavePointCloud(CxPointCloud cloud, string filePath)
        {
            if (cloud == null) throw new ArgumentNullException(nameof(cloud));

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

        /// <summary>Deserializes a <see cref="CxPointCloud"/> from a binary file.</summary>
        /// <param name="filePath">Source file path.</param>
        /// <returns>The loaded point cloud, or <c>null</c> if the file does not exist.</returns>
        /// <exception cref="InvalidDataException">The file format or magic number is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred while reading the file.</exception>
        public static CxPointCloud LoadPointCloud(string filePath)
        {
            if (!File.Exists(filePath)) return null;

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

            if (string.Equals(Path.GetExtension(filePath), ".obj", StringComparison.OrdinalIgnoreCase))
            {
                SaveMeshToObj(mesh, filePath);
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

            if (string.Equals(Path.GetExtension(filePath), ".obj", StringComparison.OrdinalIgnoreCase))
                return LoadMeshFromObj(filePath);

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
    }
}
