using SharpGL.SceneGraph;
using SharpGL;
using System;
using System.Collections.Generic;
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
        /// Computes the axis-aligned bounding box of a set of 3D points.
        /// Points with any <see cref="float.IsInfinity"/> coordinate are ignored.
        /// </summary>
        /// <param name="points">Input points. May be <c>null</c> or empty.</param>
        /// <returns>
        /// A <see cref="Box3D"/> whose centre and size enclose all valid points,
        /// or <c>null</c> if no valid point exists.
        /// </returns>
        public static Box3D? CalculateBoundingBox(IEnumerable<CxPoint3D> points)
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
            return new Box3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

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
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="x">World X coordinate.</param>
        /// <param name="y">World Y coordinate.</param>
        /// <param name="z">World Z coordinate.</param>
        /// <param name="size">Base font size before depth scaling.</param>
        /// <param name="text">Text string to draw.</param>
        public static void DrawTextLabel3D(OpenGL gl, float x, float y, float z, float size, string text)
        {
            var objCoord    = new Vertex(x, y, z);
            var screenCoord = gl.Project(objCoord);

            if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
                return;

            if (screenCoord.Z < 0 || screenCoord.Z > 1)
                return;

            float scaledSize = size * (1 + screenCoord.Z);

            gl.PushMatrix();
            gl.Translate(x, y, z);
            gl.Scale(scaledSize, scaledSize, scaledSize);
            gl.Color(1.0f, 1.0f, 1.0f);
            foreach (char c in text)
                gl.DrawText3D("Arial", 0.1f, 0.0f, c.ToString());
            gl.PopMatrix();
        }

        /// <summary>
        /// Renders a text label at the screen position corresponding to the given world-space point.
        /// The label is skipped if it projects outside the viewport or behind the camera.
        /// </summary>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="x">World X coordinate.</param>
        /// <param name="y">World Y coordinate.</param>
        /// <param name="z">World Z coordinate.</param>
        /// <param name="size">Font size in points.</param>
        /// <param name="text">Text string to draw.</param>
        public static void DrawTextLabel2D(OpenGL gl, float x, float y, float z, float size, string text)
        {
            var objCoord    = new Vertex(x, y, z);
            var screenCoord = gl.Project(objCoord);

            if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
                return;

            if (screenCoord.Z < 0 || screenCoord.Z > 1)
                return;

            gl.DrawText((int)screenCoord.X, (int)screenCoord.Y, 1, 1, 1, "Arial", size, text);
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
                var gl       = new OpenGL();
                string ver   = gl.GetString(OpenGL.GL_VERSION);
                string rend  = gl.GetString(OpenGL.GL_RENDERER);
                string vend  = gl.GetString(OpenGL.GL_VENDOR);

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
                var gl      = new OpenGL();
                string ver  = gl.GetString(OpenGL.GL_VERSION);
                string rend = gl.GetString(OpenGL.GL_RENDERER);
                string vend = gl.GetString(OpenGL.GL_VENDOR);
                return $"OpenGL version: {ver}\r\nRenderer: {rend}\r\nVendor: {vend}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
