using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using VisionNet.DataType;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace VisionNet.Controls
{
    internal sealed partial class ModernGLRenderer
    {

        /// <summary>
        /// Rasterizes (or reuses) a text texture and draws it at a lower-left-origin
        /// screen position. Public 3D text APIs pass point sizes, so the method keeps
        /// the historical 96-DPI point-to-pixel conversion.
        /// </summary>
        internal void DrawText(CxGlContext gl, int x, int y, Color color, float fontSize, string text)
        {
            if (string.IsNullOrEmpty(text) || gl == null || !gl.IsValid) return;
            // The public 3D text APIs historically pass point sizes, whose
            // FontBitmaps implementation converts points to pixels using
            // 96 DPI (fontSize * 96 / 72). Keep that visual contract for the
            // System.Drawing texture path as well.
            float effectiveFontSize = Math.Max(1f, fontSize * 4f / 3f);
            if (CanUseNumericGlyphAtlas(text))
            {
                DrawNumericGlyphText(gl, x, y, color, effectiveFontSize, text);
                return;
            }

            CachedText cached = GetOrCreateTextTexture(gl, effectiveFontSize, text);
            _textQuad[0] = x; _textQuad[1] = y; _textQuad[2] = 0; _textQuad[3] = 1;
            _textQuad[4] = x + cached.Width; _textQuad[5] = y; _textQuad[6] = 1; _textQuad[7] = 1;
            _textQuad[8] = x + cached.Width; _textQuad[9] = y + cached.Height;
            _textQuad[10] = 1; _textQuad[11] = 0;
            _textQuad[12] = x; _textQuad[13] = y + cached.Height;
            _textQuad[14] = 0; _textQuad[15] = 0;
            DrawTextQuad(gl, _textQuad, cached.Texture, color);
            if (!_frameStateCaptured)
            {
                // Standalone callers do not have a BeginFrame/EndFrame scope, so
                // enforce the same bounded cache after each independent draw.
                TrimTextCache(gl, MaxCachedTextTextures);
                _textTexturesUsedThisFrame.Clear();
            }
        }

        private static bool CanUseNumericGlyphAtlas(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
                if (NumericGlyphCharacters.IndexOf(text[i]) < 0) return false;
            return true;
        }

        private void DrawNumericGlyphText(CxGlContext gl, int x, int y, Color color,
            float fontSize, string text)
        {
            var atlas = GetOrCreateGlyphAtlas(gl, fontSize);
            int quadWidth = Math.Max(1, atlas.CellWidth - 2);
            int quadHeight = Math.Max(1, atlas.CellHeight - 2);
            int cursor = x + atlas.LegacyLeftBearing + 1;
            int requiredFloats = text.Length * 6 * 4;
            if (_glyphTriangles.Length < requiredFloats)
            {
                int newLength = Math.Max(requiredFloats,
                    Math.Max(24, _glyphTriangles.Length * 2));
                _glyphTriangles = new float[newLength];
            }

            int vertexOffset = 0;
            foreach (char ch in text)
            {
                GlyphCell cell;
                if (!atlas.Cells.TryGetValue(ch, out cell)) continue;

                // Match the former triangle-fan order: bottom-left, bottom-right,
                // top-right, then bottom-left, top-right, top-left.
                WriteTextVertex(_glyphTriangles, ref vertexOffset,
                    cursor, y, cell.U0, cell.V1);
                WriteTextVertex(_glyphTriangles, ref vertexOffset,
                    cursor + quadWidth, y, cell.U1, cell.V1);
                WriteTextVertex(_glyphTriangles, ref vertexOffset,
                    cursor + quadWidth, y + quadHeight, cell.U1, cell.V0);
                WriteTextVertex(_glyphTriangles, ref vertexOffset,
                    cursor, y, cell.U0, cell.V1);
                WriteTextVertex(_glyphTriangles, ref vertexOffset,
                    cursor + quadWidth, y + quadHeight, cell.U1, cell.V0);
                WriteTextVertex(_glyphTriangles, ref vertexOffset,
                    cursor, y + quadHeight, cell.U0, cell.V0);
                cursor += Math.Max(1, cell.Advance);
            }

            if (vertexOffset > 0)
                DrawTextTriangles(gl, _glyphTriangles, vertexOffset / 4,
                    atlas.Texture, color);
        }

        private static void WriteTextVertex(float[] vertices, ref int offset,
            float x, float y, float u, float v)
        {
            vertices[offset++] = x;
            vertices[offset++] = y;
            vertices[offset++] = u;
            vertices[offset++] = v;
        }

        private GlyphAtlas GetOrCreateGlyphAtlas(CxGlContext gl, float fontSize)
        {
            string key = fontSize.ToString("R", CultureInfo.InvariantCulture);
            GlyphAtlas atlas;
            if (_glyphAtlases.TryGetValue(key, out atlas)) return atlas;

            using (var font = new Font("Arial", fontSize, GraphicsUnit.Pixel))
            using (var measure = new Bitmap(1, 1, GdiPixelFormat.Format32bppArgb))
            using (var measuringGraphics = Graphics.FromImage(measure))
            {
                int cellWidth = 1;
                int cellHeight = 1;
                float legacyWidth = measuringGraphics.MeasureString(
                    "X", font).Width;
                float capAdvance = measuringGraphics.MeasureString(
                    "X", font, new PointF(0, 0), StringFormat.GenericTypographic).Width;
                float measuredLeading = (legacyWidth - capAdvance) / 2f;
                int legacyLeftBearing = measuredLeading > 0f
                    && measuredLeading <= fontSize / 3f
                    ? Math.Max(0, (int)Math.Round(measuredLeading))
                    : Math.Max(0, (int)Math.Round(fontSize / 6f));
                for (int i = 0; i < NumericGlyphCharacters.Length; i++)
                {
                    SizeF measured = measuringGraphics.MeasureString(
                        NumericGlyphCharacters[i].ToString(), font);
                    cellWidth = Math.Max(cellWidth, (int)Math.Ceiling(measured.Width) + 4);
                    cellHeight = Math.Max(cellHeight, (int)Math.Ceiling(measured.Height) + 4);
                }

                const int columns = 8;
                int rows = (NumericGlyphCharacters.Length + columns - 1) / columns;
                int atlasWidth = columns * cellWidth;
                int atlasHeight = rows * cellHeight;
                using (var bitmap = new Bitmap(atlasWidth, atlasHeight, GdiPixelFormat.Format32bppArgb))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var brush = new SolidBrush(Color.White))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    atlas = new GlyphAtlas
                    {
                        CellWidth = cellWidth,
                        CellHeight = cellHeight,
                        LegacyLeftBearing = legacyLeftBearing,
                    };

                    for (int i = 0; i < NumericGlyphCharacters.Length; i++)
                    {
                        char ch = NumericGlyphCharacters[i];
                        int cellX = (i % columns) * cellWidth;
                        int cellY = (i / columns) * cellHeight;
                        graphics.DrawString(ch.ToString(), font, brush, cellX + 2, cellY + 1,
                            StringFormat.GenericTypographic);
                        atlas.Cells[ch] = new GlyphCell
                        {
                            U0 = (float)(cellX + 1) / atlasWidth,
                            V0 = (float)(cellY + 1) / atlasHeight,
                            U1 = (float)(cellX + cellWidth - 1) / atlasWidth,
                            V1 = (float)(cellY + cellHeight - 1) / atlasHeight,
                            // Measure a surrounded glyph so spaces and punctuation retain
                            // their typographic advance; measuring each glyph in isolation
                            // includes GDI+'s side-bearing padding and makes the tooltip too wide.
                            Advance = Math.Max(1, (int)Math.Ceiling(
                                measuringGraphics.MeasureString(
                                    "X" + ch + "X", font,
                                    new PointF(0, 0), StringFormat.GenericTypographic).Width
                                - capAdvance * 2f)),
                        };
                    }

                    atlas.Texture = UploadGlyph(gl, bitmap);
                }
            }

            _glyphAtlases.Add(key, atlas);
            return atlas;
        }

        /// <summary>
        /// Looks up a complete-string texture or builds one with System.Drawing. New
        /// entries are recorded as active for the current frame and become eligible
        /// for deletion only after EndFrame has completed.
        /// </summary>
        private CachedText GetOrCreateTextTexture(CxGlContext gl, float fontSize, string text)
        {
            string key = fontSize.ToString("R", CultureInfo.InvariantCulture) + "\0" + text;
            CachedText cached;
            if (_textTextures.TryGetValue(key, out cached))
            {
                _textTexturesUsedThisFrame.Add(key);
                return cached;
            }

            using (var font = new Font("Arial", fontSize, GraphicsUnit.Pixel))
            using (var measure = new Bitmap(1, 1, GdiPixelFormat.Format32bppArgb))
            using (var mg = Graphics.FromImage(measure))
            {
                SizeF measured = mg.MeasureString(text, font);
                int width = Math.Max(1, (int)Math.Ceiling(measured.Width) + 4);
                int height = Math.Max(1, (int)Math.Ceiling(measured.Height) + 4);
                using (var bitmap = new Bitmap(width, height, GdiPixelFormat.Format32bppArgb))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (var brush = new SolidBrush(Color.White))
                        graphics.DrawString(text, font, brush, 2f, 1f);

                    cached = new CachedText
                    {
                        Texture = UploadGlyph(gl, bitmap),
                        Width = width,
                        Height = height,
                    };
                }
            }

            _textTextures.Add(key, cached);
            _textTextureOrder.Enqueue(key);
            _textTexturesUsedThisFrame.Add(key);
            return cached;
        }

        private void TrimTextCache(CxGlContext gl, int targetCount)
        {
            while (_textTextures.Count > targetCount && _textTextureOrder.Count > 0)
            {
                string evictedKey = _textTextureOrder.Dequeue();
                if (_textTexturesUsedThisFrame.Contains(evictedKey))
                {
                    _textTextureOrder.Enqueue(evictedKey);
                    continue;
                }

                CachedText evicted;
                if (_textTextures.TryGetValue(evictedKey, out evicted))
                {
                    _textTextures.Remove(evictedKey);
                    if (evicted.Texture != 0)
                        GL.DeleteTextures(1, new[] { evicted.Texture });
                }
            }
        }

        /// <summary>
        /// Projects a world-space point into viewport pixels using the same row-major
        /// matrix convention as the GLSL path. Returns false when the point is behind
        /// the camera or outside the normalized depth range.
        /// </summary>
        internal bool Project(CxGlContext gl, float x, float y, float z, out float sx, out float sy, out float sz)
        {
            sx = sy = sz = 0f;
            if (gl == null || !gl.IsValid) return false;
            EnsureSceneMatrices(gl);
            RefreshViewport(gl);
            // CxMatrix4X4 and the camera expose row-major matrices with column-vector
            // semantics. Keep CPU projection in the same convention as the GLSL
            // uniforms (uploaded with transpose=true); the previous column-major
            // multiply mirrored the projected X/Y coordinates for overlays and hit tests.
            float[] model = CurrentModel(gl).Data;
            Multiply4x4(_view, model, _projectView);
            Multiply4x4(_projection, _projectView, _projectScene);
            float tx = _projectScene[0] * x + _projectScene[1] * y + _projectScene[2] * z + _projectScene[3];
            float ty = _projectScene[4] * x + _projectScene[5] * y + _projectScene[6] * z + _projectScene[7];
            float tz = _projectScene[8] * x + _projectScene[9] * y + _projectScene[10] * z + _projectScene[11];
            float tw = _projectScene[12] * x + _projectScene[13] * y + _projectScene[14] * z + _projectScene[15];
            if (Math.Abs(tw) < 1e-6f) return false;
            tx /= tw; ty /= tw; tz /= tw;
            if (float.IsNaN(tx) || float.IsNaN(ty) || float.IsNaN(tz)) return false;
            sx = _viewport[0] + (tx + 1f) * _viewport[2] * 0.5f;
            sy = _viewport[1] + (ty + 1f) * _viewport[3] * 0.5f;
            sz = (tz + 1f) * 0.5f;
            return sz >= 0f && sz <= 1f;
        }

        /// <summary>
        /// Converts viewport pixels and a depth-buffer value back to world space by
        /// inverting the current projection/view product.
        /// </summary>
        internal bool Unproject(CxGlContext gl, float sx, float sy, float depth, out CxPoint3D point)
        {
            point = new CxPoint3D();
            if (gl == null || !gl.IsValid) return false;
            RefreshViewport(gl);
            if (_viewport[2] <= 0 || _viewport[3] <= 0) return false;
            float nx = (sx - _viewport[0]) / _viewport[2] * 2f - 1f;
            float ny = (sy - _viewport[1]) / _viewport[3] * 2f - 1f;
            float nz = depth * 2f - 1f;
            var combined = (new CxMatrix4X4(_projection) * new CxMatrix4X4(_view)).Inverse();
            point = combined.TransformPoint3D(new CxPoint3D(nx, ny, nz));
            return !(float.IsNaN(point.X) || float.IsNaN(point.Y) || float.IsNaN(point.Z));
        }

        /// <summary>Uploads one screen quad and composites a cached text alpha mask.</summary>
        private void DrawTextQuad(CxGlContext gl, float[] quad, uint texture, Color color)
        {
            DrawTextVertices(gl, quad, 4, PrimitiveType.TriangleFan, texture, color);
        }

        /// <summary>Uploads a batched text triangle list and composites a cached text alpha mask.</summary>
        private void DrawTextTriangles(CxGlContext gl, float[] vertices, int vertexCount,
            uint texture, Color color)
        {
            DrawTextVertices(gl, vertices, vertexCount, PrimitiveType.Triangles, texture, color);
        }

        private void DrawTextVertices(CxGlContext gl, float[] vertices, int vertexCount,
            PrimitiveType primitive, uint texture, Color color)
        {
            if (vertices == null || vertexCount <= 0
                || vertices.Length < vertexCount * 4)
                return;
            if (!EnsureTextProgram(gl)) return;
            EnsureTextBuffers(gl);
            RenderStateSnapshot previous = CaptureRenderState(gl);
            SetRenderState(false, true);
            GL.DepthMask(false);
            GL.BindVertexArray(_textVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _textVbo);
            GCHandle pinned = default(GCHandle);
            try
            {
                pinned = GCHandle.Alloc(vertices, GCHandleType.Pinned);
                GL.BufferData(BufferTarget.ArrayBuffer, vertexCount * 4 * sizeof(float),
                    pinned.AddrOfPinnedObject(), BufferUsageHint.StreamDraw);
            }
            finally
            {
                if (pinned.IsAllocated) pinned.Free();
            }
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), IntPtr.Zero);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), new IntPtr(2 * sizeof(float)));
            GL.EnableVertexAttribArray(1);
            GL.UseProgram(_textProgram);
            GL.UniformMatrix4(_textProjection, 1, true,
                Ortho(gl.Width, gl.Height));
            GL.Uniform4(_textColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.Uniform1(_textGlyph, 0);
            GL.DrawArrays(primitive, 0, vertexCount);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.UseProgram(0);
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            RestoreRenderState(previous);
        }

        /// <summary>
        /// Converts a GDI+ bitmap into an RGBA OpenGL texture. The source alpha is
        /// copied into all colour channels so the shader can sample the red channel.
        /// </summary>
        private uint UploadGlyph(CxGlContext gl, Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bits = bitmap.LockBits(rect, ImageLockMode.ReadOnly, GdiPixelFormat.Format32bppArgb);
            byte[] rgba = new byte[bitmap.Width * bitmap.Height * 4];
            Marshal.Copy(bits.Scan0, rgba, 0, rgba.Length);
            bitmap.UnlockBits(bits);
            for (int i = 0; i < rgba.Length; i += 4)
            {
                byte alpha = rgba[i + 3];
                rgba[i] = alpha;
                rgba[i + 1] = alpha;
                rgba[i + 2] = alpha;
                rgba[i + 3] = alpha;
            }
            IntPtr ptr = Marshal.AllocHGlobal(rgba.Length);
            try
            {
                Marshal.Copy(rgba, 0, ptr, rgba.Length);
                uint[] ids = new uint[1];
                GL.GenTextures(1, ids);
                GL.BindTexture(TextureTarget.Texture2D, ids[0]);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                    bitmap.Width, bitmap.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba,
                    PixelType.UnsignedByte, ptr);
                GL.BindTexture(TextureTarget.Texture2D, 0);
                return ids[0];
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
    }
}
