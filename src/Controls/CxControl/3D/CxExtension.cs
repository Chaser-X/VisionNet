using System;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Platform;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Static utility methods shared across the CxControl rendering pipeline:
    /// bounding-box calculation, height-to-colour mapping, 3D/2D text rendering,
    /// and OpenGL capability detection.
    /// </summary>
    public static class CxExtension
    {
        /// <summary>
        /// Raw value in the compact surface / point-cloud arrays that marks a no-data point.
        /// Mirrors the convention documented on <see cref="CxSurface.Data"/> and
        /// <see cref="CxPointCloud.Data"/>.
        /// </summary>
        internal const short NoDataValue = -32768;

        /// <summary>
        /// Half-width of the grid neighbourhood scanned around a picked cell — 2 gives a 5×5 window.
        /// </summary>
        internal const int PickNeighbourhoodRadius = 2;

        /// <summary>
        /// A reconstructed candidate must land within this multiple of the squared grid spacing,
        /// otherwise it is too far from the depth-buffer hit to plausibly belong to this surface.
        /// </summary>
        internal const float PickNeighbourhoodTolerance = 5f;

        /// <summary>
        /// Searches the <see cref="PickNeighbourhoodRadius"/>-neighbourhood of the grid cell
        /// containing <paramref name="local"/> for the point closest to <paramref name="world"/>,
        /// updating the running best in place.
        /// </summary>
        /// <remarks>
        /// Pure geometry — no GL calls and no display state — so it is directly unit-testable.
        /// Each candidate is reconstructed in <b>world</b> space via <paramref name="model"/>
        /// before its distance is measured: the front-most surface (the one whose geometry
        /// actually contains the depth-buffer point) reconstructs nearest to
        /// <paramref name="world"/>, so overlapping surfaces are disambiguated by world-space
        /// distance rather than local coordinate overlap.
        /// </remarks>
        /// <param name="surface">Structured surface to search.</param>
        /// <param name="model">The owning item's model matrix (local → world).</param>
        /// <param name="local">Picking point transformed into the surface's local frame.</param>
        /// <param name="world">Picking point in world space.</param>
        /// <param name="useDiff">True in Diff colour mode: report Diff values instead of Intensity.</param>
        /// <param name="bestDist">Best squared distance so far; lowered when a closer candidate is found.</param>
        /// <param name="bestPoint">Best world-space point so far; updated with <paramref name="bestDist"/>.</param>
        /// <param name="bestIntensity">Value at the best point (diff or intensity, per <paramref name="useDiff"/>).</param>
        internal static void SearchSurfaceNeighbourhood(CxSurface surface, CxMatrix4X4 model,
            CxPoint3D local, CxPoint3D world, bool useDiff,
            ref float bestDist, ref CxPoint3D? bestPoint, ref float? bestIntensity)
        {
            int xi = (int)Math.Round((local.X - surface.XOffset) / surface.XScale);
            int yi = (int)Math.Round((local.Y - surface.YOffset) / surface.YScale);
            if (xi < 0 || xi >= surface.Width || yi < 0 || yi >= surface.Length) return;

            float threshold = PickNeighbourhoodTolerance * (surface.XScale * surface.XScale
                                                          + surface.YScale * surface.YScale
                                                          + surface.ZScale * surface.ZScale);

            for (int dy = -PickNeighbourhoodRadius; dy <= PickNeighbourhoodRadius; dy++)
            {
                for (int dx = -PickNeighbourhoodRadius; dx <= PickNeighbourhoodRadius; dx++)
                {
                    int nx = xi + dx, ny = yi + dy;
                    if (nx < 0 || nx >= surface.Width || ny < 0 || ny >= surface.Length) continue;

                    int idx = ny * surface.Width + nx;
                    float z = surface.Data[idx] == NoDataValue
                        ? float.NegativeInfinity
                        : surface.ZOffset + surface.Data[idx] * surface.ZScale;
                    if (float.IsInfinity(z)) continue;

                    float px = surface.XOffset + nx * surface.XScale;
                    float py = surface.YOffset + ny * surface.YScale;

                    var worldCand = model.TransformPoint3D(new CxPoint3D(px, py, z));
                    float d = (worldCand.X - world.X) * (worldCand.X - world.X)
                            + (worldCand.Y - world.Y) * (worldCand.Y - world.Y)
                            + (worldCand.Z - world.Z) * (worldCand.Z - world.Z);

                    if (d < bestDist && d < threshold)
                    {
                        bestDist  = d;
                        bestPoint = worldCand;
                        bestIntensity = useDiff
                            ? (surface.Diff != null && surface.Diff.Length > idx)
                                ? surface.Diff[idx] : (float?)null
                            : (surface.Intensity != null && surface.Intensity.Length > idx)
                                ? surface.Intensity[idx] : (float?)null;
                    }
                }
            }
        }

        /// <summary>
        /// Finalises a bounding box from accumulated per-axis extremes. Callers must have already
        /// skipped every invalid point, so the extremes describe at least one real point.
        /// </summary>
        internal static CxBox3D BuildBox(float minX, float minY, float minZ,
            float maxX, float maxY, float maxZ)
        {
            return new CxBox3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        /// <summary>
        /// Computes the axis-aligned bounding box of a set of 3D points.
        /// Points with any <see cref="float.IsInfinity"/> coordinate are ignored.
        /// </summary>
        /// <param name="points">Input points. May be <c>null</c> or empty.</param>
        /// <returns>
        /// A <see cref="CxBox3D"/> whose centre and size enclose all valid points,
        /// or <c>null</c> if no valid point exists.
        /// </returns>
        public static CxBox3D? CalculateBoundingBox(IEnumerable<CxPoint3D> points)
        {
            if (points == null) return null;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool hasValidPoint = false;

            foreach (var p in points)
            {
                if (float.IsInfinity(p.X) || float.IsInfinity(p.Y) || float.IsInfinity(p.Z))
                    continue;
                hasValidPoint = true;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            if (!hasValidPoint) return null;
            return BuildBox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        /// <summary>
        /// Computes a structured surface bounding box directly from its compact source array.
        /// This avoids materializing a full <see cref="CxPoint3D"/> array just for the scan.
        /// </summary>
        public static CxBox3D? CalculateBoundingBox(CxSurface surface)
        {
            if (surface == null || surface.Data == null
                || surface.Width <= 0 || surface.Length <= 0)
                return null;

            int width = surface.Width;
            int rowCount = Math.Min(surface.Length, surface.Data.Length / width);
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool hasValidPoint = false;

            for (int row = 0; row < rowCount; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    short rawZ = surface.Data[row * width + col];
                    if (rawZ == NoDataValue) continue;

                    float x = surface.XOffset + col * surface.XScale;
                    float y = surface.YOffset + row * surface.YScale;
                    float z = surface.ZOffset + rawZ * surface.ZScale;
                    hasValidPoint = true;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (z < minZ) minZ = z;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                    if (z > maxZ) maxZ = z;
                }
            }

            if (!hasValidPoint) return null;
            return BuildBox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        /// <summary>
        /// Computes an ordered point-cloud bounding box directly from its packed short array.
        /// Invalid triples are skipped without allocating a temporary point array.
        /// </summary>
        public static CxBox3D? CalculateBoundingBox(CxPointCloud cloud)
        {
            if (cloud == null || cloud.Data == null
                || cloud.Width <= 0 || cloud.Length <= 0)
                return null;

            int pointCount = Math.Min(cloud.Width * cloud.Length, cloud.Data.Length / 3);
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool hasValidPoint = false;

            for (int i = 0; i < pointCount; i++)
            {
                short rawX = cloud.Data[i * 3];
                short rawY = cloud.Data[i * 3 + 1];
                short rawZ = cloud.Data[i * 3 + 2];
                if (rawX == NoDataValue || rawY == NoDataValue || rawZ == NoDataValue) continue;

                float x = cloud.XOffset + rawX * cloud.XScale;
                float y = cloud.YOffset + rawY * cloud.YScale;
                float z = cloud.ZOffset + rawZ * cloud.ZScale;
                hasValidPoint = true;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (z < minZ) minZ = z;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (z > maxZ) maxZ = z;
            }

            if (!hasValidPoint) return null;
            return BuildBox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        /// <summary>
        /// Computes the world-space Z range of an axis-aligned box transformed by an affine matrix.
        /// The eight corners of the box are transformed; the resulting min/max Z bounds every
        /// transformed point (conservative). When the box is <c>null</c> or the range collapses,
        /// a small centred span is returned so colour mapping never divides by zero.
        /// </summary>
        /// <param name="box">Local-space axis-aligned bounding box.</param>
        /// <param name="matrix">Affine transform (model matrix / pose).</param>
        /// <param name="zMin">World-space minimum Z.</param>
        /// <param name="zMax">World-space maximum Z.</param>
        public static void ComputeWorldZRange(CxBox3D? box, CxMatrix4X4 matrix, out float zMin, out float zMax)
        {
            zMin = float.MaxValue;
            zMax = float.MinValue;

            if (box.HasValue)
            {
                float cx = box.Value.Center.X, cy = box.Value.Center.Y, cz = box.Value.Center.Z;
                float hw = box.Value.Size.Width / 2f;
                float hh = box.Value.Size.Height / 2f;
                float hd = box.Value.Size.Depth / 2f;

                for (int i = 0; i < 8; i++)
                {
                    float x = cx + ((i & 1) == 0 ? -hw : hw);
                    float y = cy + ((i & 2) == 0 ? -hh : hh);
                    float z = cz + ((i & 4) == 0 ? -hd : hd);
                    var wp = matrix.TransformPoint3D(new CxPoint3D(x, y, z));
                    if (wp.Z < zMin) zMin = wp.Z;
                    if (wp.Z > zMax) zMax = wp.Z;
                }
            }

            if (zMax - zMin < 1e-6f)
            {
                float mid = (zMin + zMax) * 0.5f;
                zMin = mid - 0.5f;
                zMax = mid + 0.5f;
            }
        }

        /// <summary>
        /// Computes per-vertex normals for an organized grid (structured surface or ordered
        /// point cloud) from its sampled vertex positions. Normals are the cross product of the
        /// two grid tangents (central differences, one-sided at the borders). A vertex whose
        /// neighbourhood contains an invalid (non-finite) position, or whose tangents are
        /// degenerate, receives the <c>+Z</c> fallback so lighting never receives a NaN.
        /// </summary>
        /// <param name="vertices">Row-major <c>width × height</c> positions as <c>float[n × 3]</c>.</param>
        /// <param name="width">Number of columns in the grid.</param>
        /// <param name="height">Number of rows in the grid.</param>
        /// <returns>
        /// Row-major <c>float[n × 3]</c> unit normals, or <c>null</c> when the input is unusable.
        /// </returns>
        internal static float[] ComputeGridNormals(float[] vertices, int width, int height)
        {
            if (vertices == null || width <= 0 || height <= 0
                || vertices.Length < width * height * 3)
                return null;

            var normals = new float[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                int yPrev = y > 0 ? y - 1 : y;
                int yNext = y < height - 1 ? y + 1 : y;
                for (int x = 0; x < width; x++)
                {
                    int xPrev = x > 0 ? x - 1 : x;
                    int xNext = x < width - 1 ? x + 1 : x;

                    int i = (y * width + x) * 3;
                    int iU0 = (y * width + xPrev) * 3;
                    int iU1 = (y * width + xNext) * 3;
                    int iV0 = (yPrev * width + x) * 3;
                    int iV1 = (yNext * width + x) * 3;

                    if (!IsFinite3(vertices, iU0) || !IsFinite3(vertices, iU1)
                        || !IsFinite3(vertices, iV0) || !IsFinite3(vertices, iV1))
                    {
                        normals[i] = 0f; normals[i + 1] = 0f; normals[i + 2] = 1f;
                        continue;
                    }

                    float ux = vertices[iU1] - vertices[iU0];
                    float uy = vertices[iU1 + 1] - vertices[iU0 + 1];
                    float uz = vertices[iU1 + 2] - vertices[iU0 + 2];
                    float vx = vertices[iV1] - vertices[iV0];
                    float vy = vertices[iV1 + 1] - vertices[iV0 + 1];
                    float vz = vertices[iV1 + 2] - vertices[iV0 + 2];

                    float nx = uy * vz - uz * vy;
                    float ny = uz * vx - ux * vz;
                    float nz = ux * vy - uy * vx;
                    float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len > 1e-6f)
                    {
                        normals[i] = nx / len; normals[i + 1] = ny / len; normals[i + 2] = nz / len;
                    }
                    else
                    {
                        normals[i] = 0f; normals[i + 1] = 0f; normals[i + 2] = 1f;
                    }
                }
            }
            return normals;
        }

        /// <summary>
        /// Computes smoothed per-vertex normals for an indexed triangle mesh: each triangle's
        /// face normal is accumulated into its three vertices, then the accumulated vectors are
        /// normalized. Vertices touched by no valid triangle receive the <c>+Z</c> fallback.
        /// </summary>
        /// <param name="vertices">Row-major positions as <c>float[n × 3]</c>.</param>
        /// <param name="indices">Triangle indices (three per triangle).</param>
        /// <returns>
        /// Row-major <c>float[n × 3]</c> unit normals, or <c>null</c> when no triangle produced a
        /// usable normal (so the caller falls back to the derivative-based normal).
        /// </returns>
        internal static float[] ComputeTriangleNormals(float[] vertices, uint[] indices)
        {
            if (vertices == null || indices == null || indices.Length < 3) return null;

            int vertexCount = vertices.Length / 3;
            if (vertexCount == 0) return null;

            var normals = new float[vertices.Length];
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                uint i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
                if (i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount) continue;

                int a = (int)i0 * 3, b = (int)i1 * 3, c = (int)i2 * 3;
                float e1x = vertices[b] - vertices[a];
                float e1y = vertices[b + 1] - vertices[a + 1];
                float e1z = vertices[b + 2] - vertices[a + 2];
                float e2x = vertices[c] - vertices[a];
                float e2y = vertices[c + 1] - vertices[a + 1];
                float e2z = vertices[c + 2] - vertices[a + 2];

                float nx = e1y * e2z - e1z * e2y;
                float ny = e1z * e2x - e1x * e2z;
                float nz = e1x * e2y - e1y * e2x;
                normals[a] += nx; normals[a + 1] += ny; normals[a + 2] += nz;
                normals[b] += nx; normals[b + 1] += ny; normals[b + 2] += nz;
                normals[c] += nx; normals[c + 1] += ny; normals[c + 2] += nz;
            }

            bool anyValid = false;
            for (int i = 0; i < vertexCount; i++)
            {
                int o = i * 3;
                float nx = normals[o], ny = normals[o + 1], nz = normals[o + 2];
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 1e-6f)
                {
                    normals[o] = nx / len; normals[o + 1] = ny / len; normals[o + 2] = nz / len;
                    anyValid = true;
                }
                else
                {
                    normals[o] = 0f; normals[o + 1] = 0f; normals[o + 2] = 1f;
                }
            }
            return anyValid ? normals : null;
        }

        private static bool IsFinite3(float[] v, int i)
            => !float.IsInfinity(v[i]) && !float.IsNaN(v[i])
            && !float.IsInfinity(v[i + 1]) && !float.IsNaN(v[i + 1])
            && !float.IsInfinity(v[i + 2]) && !float.IsNaN(v[i + 2]);

        /// <summary>
        /// Maps a Z height value to an RGB colour using a 7-band rainbow gradient
        /// (dark-blue → sky-blue → green → yellow → red → pink → white).
        /// </summary>
        /// <param name="z">Height value to map. Clamped to [<paramref name="zMin"/>, <paramref name="zMax"/>].</param>
        /// <param name="zMin">Minimum height (maps to dark blue).</param>
        /// <param name="zMax">Maximum height (maps to white).</param>
        /// <returns>RGB colour components in the range [0, 1].</returns>
        public static (float r, float g, float b) GetColorByHeight(double z, double zMin, double zMax)
        {
            float range = (float)(zMax - zMin);
            if (z > zMax) z = zMax;
            if (z < zMin) z = zMin;
            float n = (float)(z - zMin) / range;   // Normalised height in [0, 1].

            float r, g, b;
            if (n < 1f / 7)
            {
                r = 0f; g = 0f; b = 0.5f + n * 3.5f;                         // Dark blue
            }
            else if (n < 2f / 7)
            {
                r = 0f; g = (n - 1f / 7) * 7f; b = 1f;                        // Sky blue
            }
            else if (n < 3f / 7)
            {
                r = 0f; g = 1f; b = 1f - (n - 2f / 7) * 7f;                   // Green
            }
            else if (n < 4f / 7)
            {
                r = (n - 3f / 7) * 7f; g = 1f; b = 0f;                        // Yellow
            }
            else if (n < 5f / 7)
            {
                r = 1f; g = 1f - (n - 4f / 7) * 7f; b = 0f;                   // Red
            }
            else if (n < 6f / 7)
            {
                r = 1f; g = 0f; b = (n - 5f / 7) * 7f;                        // Pink
            }
            else
            {
                r = 1f; g = (n - 6f / 7) * 7f; b = 1f;                        // White
            }
            return (r, g, b);
        }

        /// <summary>
        /// Renders a text label at the given world-space position, scaled so it appears
        /// at a roughly constant pixel size regardless of depth.
        /// The label is skipped if it projects outside the viewport or behind the camera.
        /// </summary>
        /// <remarks>
        /// Binds <paramref name="gl"/> for the call, so it is safe to invoke outside a render frame.
        /// World-space projection needs a projection/view pair: outside a frame, call
        /// <c>ICamera.LookAtMatrix(gl)</c> (or <c>ModernGLRenderer.SetSceneMatrices</c>) first — the
        /// old fixed-function fallback no longer exists.
        /// </remarks>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="x">World X coordinate.</param>
        /// <param name="y">World Y coordinate.</param>
        /// <param name="z">World Z coordinate.</param>
        /// <param name="size">Base font size before depth scaling.</param>
        /// <param name="text">Text string to draw.</param>
        public static void DrawTextLabel3D(CxGlContext gl, float x, float y, float z, float size, string text)
        {
            if (gl == null) return;
            gl.RunOnContext(() =>
            {
                var renderer = ModernGLRenderer.For(gl);
                float sx, sy, sz;
                if (!renderer.Project(x, y, z, out sx, out sy, out sz)) return;
                renderer.DrawText((int)sx, (int)sy, System.Drawing.Color.White,
                    size * (1f + sz), text);
            });
        }

        /// <summary>
        /// Renders a text label at the screen position corresponding to the given world-space point.
        /// The label is skipped if it projects outside the viewport or behind the camera.
        /// </summary>
        /// <remarks>
        /// Binds <paramref name="gl"/> for the call; world-space projection needs a projection/view
        /// pair supplied via <c>ICamera.LookAtMatrix(gl)</c> or
        /// <c>ModernGLRenderer.SetSceneMatrices</c> when called outside a render frame.
        /// </remarks>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="x">World X coordinate.</param>
        /// <param name="y">World Y coordinate.</param>
        /// <param name="z">World Z coordinate.</param>
        /// <param name="size">Font size in points.</param>
        /// <param name="text">Text string to draw.</param>
        public static void DrawTextLabel2D(CxGlContext gl, float x, float y, float z, float size, string text)
        {
            if (gl == null) return;
            gl.RunOnContext(() =>
            {
                var renderer = ModernGLRenderer.For(gl);
                float sx, sy, sz;
                if (!renderer.Project(x, y, z, out sx, out sy, out sz)) return;
                renderer.DrawText((int)sx, (int)sy, System.Drawing.Color.White, size, text);
            });
        }

        /// <summary>
        /// Checks whether a hardware OpenGL context can be created on this machine.
        /// Logs version, renderer, and vendor information to the console.
        /// </summary>
        /// <returns>
        /// <c>true</c> if OpenGL initialised successfully (even on a software renderer);
        /// <c>false</c> if initialisation threw an exception.
        /// </returns>
        public static bool IsOpenGLAvailable()
        {
            try
            {
                using (var probe = OpenGlProbe.Open())
                {
                    if (probe == null) return false;

                    string ver  = GL.GetString(StringName.Version);
                    string rend = GL.GetString(StringName.Renderer);
                    string vend = GL.GetString(StringName.Vendor);

                    Console.WriteLine($"OpenGL version: {ver}");
                    Console.WriteLine($"Renderer: {rend}");
                    Console.WriteLine($"Vendor: {vend}");

                    bool isSoftware = rend.Contains("Software") ||
                                      rend.Contains("Microsoft") ||
                                      rend.Contains("GDI Generic");
                    if (isSoftware)
                        Console.WriteLine("Warning: software renderer detected — performance may be limited.");

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenGL initialisation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns a multi-line string with the OpenGL version, renderer, and vendor strings
        /// reported by the current driver.
        /// </summary>
        /// <returns>Formatted version information, or the exception message on failure.</returns>
        public static string GetOpenGLVersion()
        {
            try
            {
                using (var probe = OpenGlProbe.Open())
                {
                    if (probe == null) return "No OpenGL context could be created.";

                    string ver  = GL.GetString(StringName.Version);
                    string rend = GL.GetString(StringName.Renderer);
                    string vend = GL.GetString(StringName.Vendor);
                    return $"OpenGL version: {ver}\r\nRenderer: {rend}\r\nVendor: {vend}";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Creates a throwaway OpenGL context for capability probing, independent of any
        /// <see cref="CxDisplay"/>. OpenTK has no standalone context type, so this wraps a hidden
        /// 1×1 <see cref="GameWindow"/>. Dispose it to release the context.
        /// </summary>
        private sealed class OpenGlProbe : IDisposable
        {
            private readonly GameWindow _window;

            private OpenGlProbe(GameWindow window) { _window = window; }

            internal static OpenGlProbe Open()
            {
                Toolkit.Init(new ToolkitOptions { Backend = PlatformBackend.PreferNative });
                var window = new GameWindow(1, 1, GraphicsMode.Default, "CxControl GL probe",
                    GameWindowFlags.Default, DisplayDevice.Default, 3, 3, GraphicsContextFlags.Default);
                try
                {
                    window.Visible = false;
                    window.Context.MakeCurrent(window.WindowInfo);
                }
                catch
                {
                    window.Dispose();
                    throw;
                }
                return new OpenGlProbe(window);
            }

            public void Dispose() => _window?.Dispose();
        }
    }
}
