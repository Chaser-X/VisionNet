using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Shared renderer for the small, immediate-data overlays used by <see cref="CxDisplay"/>.
    ///
    /// The renderer owns the GLSL programs and reusable VAO/VBO pairs used by geometry
    /// and text. Overlay items only provide CPU-side vertices; they do not create or
    /// destroy per-item GL objects and never call the legacy Begin/End or matrix-stack API.
    /// One instance is kept for each <see cref="OpenGL"/> wrapper because all GL object
    /// names are local to the wrapper's current context.
    /// </summary>
    internal sealed class ModernOverlayRenderer
    {
        /// <summary>Stores the model-matrix stack for one GL wrapper.</summary>
        private sealed class State
        {
            internal readonly Stack<CxMatrix4X4> Models = new Stack<CxMatrix4X4>();
            internal CxMatrix4X4 Current = CxMatrix4X4.Identity();
        }

        /// <summary>
        /// Captures the small subset of mutable GL state changed by an overlay draw,
        /// allowing the next item to observe the same state as before the draw.
        /// </summary>
        internal struct RenderStateSnapshot
        {
            internal bool DepthTest;
            internal bool DepthWrite;
            internal bool Blend;
            internal float PointSize;
            internal float LineWidth;
        }

        // Access to the wrapper-to-renderer map is synchronized because resource
        // disposal can happen on a UI/lifecycle callback while drawing is in progress.
        private static readonly object Sync = new object();
        private static readonly Dictionary<OpenGL, ModernOverlayRenderer> Instances
            = new Dictionary<OpenGL, ModernOverlayRenderer>();

        // The stack is kept per wrapper even though the normal CxDisplay path uses one
        // wrapper per renderer. This also keeps custom overlay callers isolated.
        private readonly Dictionary<OpenGL, State> _states = new Dictionary<OpenGL, State>();

        /// <summary>GPU texture and pixel dimensions for one rasterized text string.</summary>
        private sealed class CachedText
        {
            internal uint Texture;
            internal int Width;
            internal int Height;
        }

        private struct GlyphCell
        {
            internal float U0;
            internal float V0;
            internal float U1;
            internal float V1;
            internal int Advance;
        }

        private sealed class GlyphAtlas
        {
            internal uint Texture;
            internal int CellWidth;
            internal int CellHeight;
            internal int LegacyLeftBearing;
            internal readonly Dictionary<char, GlyphCell> Cells
                = new Dictionary<char, GlyphCell>();
        }

        // Textures are keyed by effective pixel size plus the complete string. The
        // frame residency set prevents a FIFO cache from evicting its own working set.
        private readonly Dictionary<string, CachedText> _textTextures
            = new Dictionary<string, CachedText>(StringComparer.Ordinal);
        private readonly Queue<string> _textTextureOrder = new Queue<string>();
        private readonly HashSet<string> _textTexturesUsedThisFrame
            = new HashSet<string>(StringComparer.Ordinal);
        private const int MaxCachedTextTextures = 256;
        private const string NumericGlyphCharacters = "0123456789XYZI:.,-+ ";
        private readonly Dictionary<string, GlyphAtlas> _glyphAtlases
            = new Dictionary<string, GlyphAtlas>(StringComparer.Ordinal);

        // Shared geometry resources. Geometry uploads stream into one reusable VBO;
        // the text path has a separate VAO/VBO because its attributes are vec2/UV.
        private uint _geometryProgram;
        private uint _textProgram;
        private uint _surfaceProgram;
        // Set when a shader failed to compile so draws skip instead of recompiling each call.
        private bool _geometryProgramFailed;
        private bool _textProgramFailed;
        private bool _surfaceProgramFailed;
        private uint _geometryVao;
        private uint _geometryVbo;
        private uint _textVao;
        private uint _textVbo;
        private int _geometryProjection = -1;
        private int _geometryView = -1;
        private int _geometryModel = -1;
        private int _geometryUseVertexColor = -1;
        private int _geometryColor = -1;
        private int _textProjection = -1;
        private int _textColor = -1;
        private int _textGlyph = -1;

        // Sphere overlays share one unit-sphere mesh and program per GL context. The instance
        // buffer is streamed each draw because point positions may be edited interactively.
        private uint _sphereProgram;
        private bool _sphereProgramFailed;
        private uint _sphereVao;
        private uint _sphereVbo;
        private uint _sphereEbo;
        private uint _sphereInstanceVbo;
        private int _sphereIndexCount;
        private int _sphereProjection = -1;
        private int _sphereView = -1;
        private int _sphereModel = -1;
        private int _sphereColor = -1;
        private int _sphereRadius = -1;
        // CPU-side row-major camera matrices. They are uploaded with transpose=true
        // to match the row-major convention used by CxMatrix4X4 and the camera code.
        private float[] _projection = IdentityData();
        private float[] _view = IdentityData();
        private readonly float[] _projectionQuery = new float[16];
        private readonly float[] _viewQuery = new float[16];
        private bool _hasSceneMatrices;

        // Overlay draws are issued between BeginFrame and EndFrame. Capture the
        // caller's GL state once for that interval instead of synchronously querying
        // the driver for every small geometry or text draw.
        private bool _frameStateCaptured;
        private RenderStateSnapshot _frameState;
        private readonly float[] _pointSizeQuery = new float[1];
        private readonly float[] _lineWidthQuery = new float[1];
        private readonly byte[] _depthWriteQuery = new byte[1];

        // Reusable CPU upload buffers for the screen-space paths.
        private float[] _screenPositions3D = new float[0];
        private float[] _coloredInterleaved = new float[0];
        private readonly float[] _identity = IdentityData();
        private float[] _ortho = IdentityData();
        private readonly float[] _textQuad = new float[16];
        private float[] _glyphTriangles = new float[0];

        // Viewport is stable for the duration of a frame. Project/Unproject reuse
        // this storage and only refresh it when the viewport changes.
        private readonly int[] _viewport = new int[4];
        private int _viewportWidth = -1;
        private int _viewportHeight = -1;
        private readonly float[] _projectView = new float[16];
        private readonly float[] _projectScene = new float[16];

        // Geometry shader: supports a uniform colour or an interleaved per-vertex colour.
        private const string GeometryVertex = @"#version 330 core
layout(location=0) in vec3 aPosition;
layout(location=1) in vec4 aVertexColor;
uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;
out vec4 vertexColor;
void main() { vertexColor = aVertexColor; gl_Position = projection * view * model * vec4(aPosition, 1.0); }";

        private const string GeometryFragment = @"#version 330 core
uniform vec4 color;
uniform int useVertexColor;
in vec4 vertexColor;
out vec4 FragColor;
void main() { FragColor = useVertexColor == 1 ? vertexColor : color; }";

        // Text shader: renders an alpha mask produced by System.Drawing onto a screen quad.
        private const string TextVertex = @"#version 330 core
layout(location=0) in vec2 aPosition;
layout(location=1) in vec2 aTexCoord;
out vec2 texCoord;
uniform mat4 projection;
void main() { texCoord = aTexCoord; gl_Position = projection * vec4(aPosition, 0.0, 1.0); }";

        private const string TextFragment = @"#version 330 core
in vec2 texCoord;
uniform sampler2D glyph;
uniform vec4 color;
out vec4 FragColor;
void main() { float alpha = texture(glyph, texCoord).r; FragColor = vec4(color.rgb, color.a * alpha); }";

        private const string SphereVertex = @"#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aInstancePos;
uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;
uniform float radius;
uniform vec4 aColor;
out vec4 vertexColor;
void main()
{
    vec3 localPos = aInstancePos + aPos * radius;
    gl_Position = projection * view * model * vec4(localPos, 1.0);
    vertexColor = aColor;
}";

        private const string SphereFragment = @"#version 330 core
in vec4 vertexColor;
out vec4 FragColor;
void main() { FragColor = vertexColor; }";

        // Instances are created through For so a caller cannot accidentally share
        // GL handles between unrelated contexts.
        private ModernOverlayRenderer() { }

        /// <summary>
        /// Returns the renderer associated with <paramref name="gl"/>, creating it
        /// lazily before the first overlay draw.
        /// </summary>
        internal static ModernOverlayRenderer For(OpenGL gl)
        {
            lock (Sync)
            {
                ModernOverlayRenderer renderer;
                if (!Instances.TryGetValue(gl, out renderer))
                {
                    renderer = new ModernOverlayRenderer();
                    Instances.Add(gl, renderer);
                }
                return renderer;
            }
        }

        /// <summary>
        /// Forgets the renderer for a context that has been recreated. The old GL names are
        /// invalid in the new context, so this path must not MakeCurrent or call Delete*.
        /// </summary>
        internal static void ResetForContextRecreate(OpenGL gl)
        {
            if (gl == null) return;
            lock (Sync)
                Instances.Remove(gl);
        }

        /// <summary>
        /// Drops the renderer associated with a GL context and releases its programs.
        /// Program handles are context-owned, so this must run while the context is current.
        /// </summary>
        internal static void Release(OpenGL gl)
        {
            if (gl == null) return;
            lock (Sync)
            {
                ModernOverlayRenderer renderer;
                if (!Instances.TryGetValue(gl, out renderer)) return;
                Instances.Remove(gl);
                // Program names are only meaningful in the context in which they
                // were created.  OpenGLControl may invoke disposal while another
                // hidden context is current, so explicitly bind this wrapper's
                // context before deleting its programs.
                if (gl.RenderContextProvider == null)
                    return;
                try
                {
                    gl.MakeCurrent();
                }
                catch
                {
                    // The provider may already be torn down.  In that case there
                    // is no valid context in which a delete could be issued.
                    return;
                }
                renderer.ReleasePrograms(gl);
            }
        }

        /// <summary>Pushes the current overlay model matrix and selects <paramref name="model"/>.</summary>
        internal void PushModel(OpenGL gl, CxMatrix4X4 model)
        {
            State state = GetState(gl);
            state.Models.Push(state.Current);
            state.Current = model ?? CxMatrix4X4.Identity();
        }

        /// <summary>Restores the model matrix saved by the matching <see cref="PushModel"/> call.</summary>
        internal void PopModel(OpenGL gl)
        {
            State state = GetState(gl);
            state.Current = state.Models.Count == 0
                ? CxMatrix4X4.Identity() : state.Models.Pop();
        }

        /// <summary>Gets the model matrix currently applied to world-space overlay vertices.</summary>
        internal CxMatrix4X4 CurrentModel(OpenGL gl) => GetState(gl).Current;

        /// <summary>
        /// Stores the camera matrices for subsequent world-space draws and CPU-side
        /// projection/unprojection. The arrays are copied by reference for the frame.
        /// </summary>
        internal void SetSceneMatrices(float[] projection, float[] view)
        {
            _projection = projection ?? IdentityData();
            _view = view ?? IdentityData();
            _hasSceneMatrices = true;
        }

        /// <summary>Returns the current view matrix for camera-relative screen indicators.</summary>
        internal float[] SceneView => _view;

        /// <summary>
        /// Starts a frame-local text-cache residency window. Textures used by the
        /// current frame are kept together so a scene with slightly more than the
        /// nominal cache size does not evict its own working set while it is drawn.
        /// </summary>
        internal void BeginFrame(OpenGL gl)
        {
            _frameState = CaptureRenderStateUncached(gl);
            _frameStateCaptured = true;
            RefreshViewport(gl);
            _textTexturesUsedThisFrame.Clear();
        }

        /// <summary>
        /// Removes stale text textures after the frame has finished. The active
        /// frame working set is allowed to exceed the nominal limit; only entries
        /// not used by this frame are evicted.
        /// </summary>
        internal void EndFrame(OpenGL gl)
        {
            int targetCount = Math.Max(MaxCachedTextTextures, _textTexturesUsedThisFrame.Count);
            TrimTextCache(gl, targetCount);
            _textTexturesUsedThisFrame.Clear();
            if (_frameStateCaptured)
            {
                RestoreRenderState(gl, _frameState);
                _frameStateCaptured = false;
            }
            // Explicit camera matrices belong only to the CxDisplay frame.  Public
            // text helpers called afterwards must fall back to the caller's active
            // fixed-function matrices rather than reusing this frame's camera.
            _hasSceneMatrices = false;
        }

        /// <summary>
        /// Draws world-space positions with the shared geometry shader. The current
        /// model matrix is combined with the camera matrices before the draw.
        /// </summary>
        internal void Draw(OpenGL gl, uint primitive, float[] positions, Color color,
            float size = 1f, bool depthTest = true)
        {
            if (positions == null || positions.Length < 3) return;
            EnsureSceneMatrices(gl);
            if (!EnsureGeometryProgram(gl)) return;
            EnsureGeometryBuffers(gl);
            RenderStateSnapshot previous = CaptureRenderState(gl);
            SetRenderState(gl, depthTest, color.A < 255);

            gl.BindVertexArray(_geometryVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _geometryVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, positions, OpenGL.GL_STREAM_DRAW);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
            gl.EnableVertexAttribArray(0);

            gl.UseProgram(_geometryProgram);
            gl.UniformMatrix4(_geometryProjection, 1, true, _projection);
            gl.UniformMatrix4(_geometryView, 1, true, _view);
            gl.UniformMatrix4(_geometryModel, 1, true, CurrentModel(gl).Data);
            gl.Uniform1(_geometryUseVertexColor, 0);
            gl.Uniform4(_geometryColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            if (primitive == OpenGL.GL_LINES || primitive == OpenGL.GL_LINE_STRIP ||
                primitive == OpenGL.GL_LINE_LOOP) gl.LineWidth(Math.Max(1f, size));
            if (primitive == OpenGL.GL_POINTS) gl.PointSize(Math.Max(1f, size));
            gl.DrawArrays(primitive, 0, positions.Length / 3);

            gl.DisableVertexAttribArray(0);
            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.UseProgram(0);
            RestoreRenderState(gl, previous);
        }

        /// <summary>
        /// Draws 2D pixel coordinates using an orthographic projection. Depth testing
        /// and depth writes are disabled for the call, then restored to their prior state.
        /// </summary>
        internal void DrawScreenGeometry(OpenGL gl, uint primitive, float[] positions, Color color,
            float width = 1f)
        {
            if (positions == null || positions.Length < 2) return;
            if (!EnsureGeometryProgram(gl)) return;
            EnsureGeometryBuffers(gl);
            RenderStateSnapshot previous = CaptureRenderState(gl);
            SetRenderState(gl, false, color.A < 255);
            gl.DepthMask(0);

            gl.BindVertexArray(_geometryVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _geometryVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, To3D(positions), OpenGL.GL_STREAM_DRAW);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
            gl.EnableVertexAttribArray(0);
            gl.UseProgram(_geometryProgram);
            float[] projection = Ortho(gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
            gl.UniformMatrix4(_geometryProjection, 1, true, projection);
            gl.UniformMatrix4(_geometryView, 1, true, _identity);
            gl.UniformMatrix4(_geometryModel, 1, true, _identity);
            gl.Uniform1(_geometryUseVertexColor, 0);
            gl.Uniform4(_geometryColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            if (primitive == OpenGL.GL_LINES || primitive == OpenGL.GL_LINE_STRIP ||
                primitive == OpenGL.GL_LINE_LOOP) gl.LineWidth(Math.Max(1f, width));
            gl.DrawArrays(primitive, 0, positions.Length / 2);
            gl.DisableVertexAttribArray(0);
            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.UseProgram(0);
            RestoreRenderState(gl, previous);
        }

        /// <summary>
        /// Draws screen-space geometry with a per-vertex RGBA colour. The interleaved
        /// position/colour data is uploaded to the shared geometry VBO for one draw.
        /// </summary>
        internal void DrawScreenGeometryColored(OpenGL gl, uint primitive, float[] positions,
            float[] colors, float width = 1f)
        {
            if (positions == null || positions.Length < 2 || colors == null
                || colors.Length < (positions.Length / 2) * 4) return;

            if (!EnsureGeometryProgram(gl)) return;
            EnsureGeometryBuffers(gl);
            RenderStateSnapshot previous = CaptureRenderState(gl);
            SetRenderState(gl, false, true);
            gl.DepthMask(0);

            int vertexCount = positions.Length / 2;
            EnsureCapacity(ref _coloredInterleaved, vertexCount * 7);
            var interleaved = _coloredInterleaved;
            for (int i = 0; i < vertexCount; i++)
            {
                interleaved[i * 7] = positions[i * 2];
                interleaved[i * 7 + 1] = positions[i * 2 + 1];
                interleaved[i * 7 + 2] = 0f;
                interleaved[i * 7 + 3] = colors[i * 4];
                interleaved[i * 7 + 4] = colors[i * 4 + 1];
                interleaved[i * 7 + 5] = colors[i * 4 + 2];
                interleaved[i * 7 + 6] = colors[i * 4 + 3];
            }

            gl.BindVertexArray(_geometryVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _geometryVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, interleaved, OpenGL.GL_STREAM_DRAW);
            int stride = 7 * sizeof(float);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, stride, IntPtr.Zero);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 4, OpenGL.GL_FLOAT, false, stride, new IntPtr(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            gl.UseProgram(_geometryProgram);
            float[] projection = Ortho(gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
            gl.UniformMatrix4(_geometryProjection, 1, true, projection);
            gl.UniformMatrix4(_geometryView, 1, true, _identity);
            gl.UniformMatrix4(_geometryModel, 1, true, _identity);
            gl.Uniform4(_geometryColor, 1f, 1f, 1f, 1f);
            gl.Uniform1(_geometryUseVertexColor, 1);
            if (primitive == OpenGL.GL_LINES || primitive == OpenGL.GL_LINE_STRIP
                || primitive == OpenGL.GL_LINE_LOOP)
                gl.LineWidth(Math.Max(1f, width));
            gl.DrawArrays(primitive, 0, vertexCount);
            gl.Uniform1(_geometryUseVertexColor, 0);
            gl.DisableVertexAttribArray(1);
            gl.DisableVertexAttribArray(0);
            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.UseProgram(0);
            RestoreRenderState(gl, previous);
        }

        /// <summary>
        /// Rasterizes (or reuses) a text texture and draws it at a lower-left-origin
        /// screen position. Public 3D text APIs pass point sizes, so the method keeps
        /// the historical 96-DPI point-to-pixel conversion.
        /// </summary>
        internal void DrawText(OpenGL gl, int x, int y, Color color, float fontSize, string text)
        {
            if (string.IsNullOrEmpty(text) || gl.RenderContextProvider == null) return;
            // The public 3D text APIs historically pass point sizes to SharpGL,
            // whose FontBitmaps implementation converts points to pixels using
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

        private void DrawNumericGlyphText(OpenGL gl, int x, int y, Color color,
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

        private GlyphAtlas GetOrCreateGlyphAtlas(OpenGL gl, float fontSize)
        {
            string key = fontSize.ToString("R", CultureInfo.InvariantCulture);
            GlyphAtlas atlas;
            if (_glyphAtlases.TryGetValue(key, out atlas)) return atlas;

            using (var font = new Font("Arial", fontSize, GraphicsUnit.Pixel))
            using (var measure = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
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
                using (var bitmap = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppArgb))
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
        private CachedText GetOrCreateTextTexture(OpenGL gl, float fontSize, string text)
        {
            string key = fontSize.ToString("R", CultureInfo.InvariantCulture) + "\0" + text;
            CachedText cached;
            if (_textTextures.TryGetValue(key, out cached))
            {
                _textTexturesUsedThisFrame.Add(key);
                return cached;
            }

            using (var font = new Font("Arial", fontSize, GraphicsUnit.Pixel))
            using (var measure = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            using (var mg = Graphics.FromImage(measure))
            {
                SizeF measured = mg.MeasureString(text, font);
                int width = Math.Max(1, (int)Math.Ceiling(measured.Width) + 4);
                int height = Math.Max(1, (int)Math.Ceiling(measured.Height) + 4);
                using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
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

        private void TrimTextCache(OpenGL gl, int targetCount)
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
                        gl.DeleteTextures(1, new[] { evicted.Texture });
                }
            }
        }

        /// <summary>
        /// Projects a world-space point into viewport pixels using the same row-major
        /// matrix convention as the GLSL path. Returns false when the point is behind
        /// the camera or outside the normalized depth range.
        /// </summary>
        internal bool Project(OpenGL gl, float x, float y, float z, out float sx, out float sy, out float sz)
        {
            sx = sy = sz = 0f;
            if (gl.RenderContextProvider == null) return false;
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
        internal bool Unproject(OpenGL gl, float sx, float sy, float depth, out CxPoint3D point)
        {
            point = new CxPoint3D();
            if (gl.RenderContextProvider == null) return false;
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
        private void DrawTextQuad(OpenGL gl, float[] quad, uint texture, Color color)
        {
            DrawTextVertices(gl, quad, 4, OpenGL.GL_TRIANGLE_FAN, texture, color);
        }

        /// <summary>Uploads a batched text triangle list and composites a cached text alpha mask.</summary>
        private void DrawTextTriangles(OpenGL gl, float[] vertices, int vertexCount,
            uint texture, Color color)
        {
            DrawTextVertices(gl, vertices, vertexCount, OpenGL.GL_TRIANGLES, texture, color);
        }

        private void DrawTextVertices(OpenGL gl, float[] vertices, int vertexCount,
            uint primitive, uint texture, Color color)
        {
            if (vertices == null || vertexCount <= 0
                || vertices.Length < vertexCount * 4)
                return;
            if (!EnsureTextProgram(gl)) return;
            EnsureTextBuffers(gl);
            RenderStateSnapshot previous = CaptureRenderState(gl);
            SetRenderState(gl, false, true);
            gl.DepthMask(0);
            gl.BindVertexArray(_textVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _textVbo);
            GCHandle pinned = default(GCHandle);
            try
            {
                pinned = GCHandle.Alloc(vertices, GCHandleType.Pinned);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertexCount * 4 * sizeof(float),
                    pinned.AddrOfPinnedObject(), OpenGL.GL_STREAM_DRAW);
            }
            finally
            {
                if (pinned.IsAllocated) pinned.Free();
            }
            gl.VertexAttribPointer(0, 2, OpenGL.GL_FLOAT, false, 4 * sizeof(float), IntPtr.Zero);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 2, OpenGL.GL_FLOAT, false, 4 * sizeof(float), new IntPtr(2 * sizeof(float)));
            gl.EnableVertexAttribArray(1);
            gl.UseProgram(_textProgram);
            gl.UniformMatrix4(_textProjection, 1, true,
                Ortho(gl.RenderContextProvider.Width, gl.RenderContextProvider.Height));
            gl.Uniform4(_textColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, texture);
            gl.Uniform1(_textGlyph, 0);
            gl.DrawArrays(primitive, 0, vertexCount);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.UseProgram(0);
            gl.DisableVertexAttribArray(0);
            gl.DisableVertexAttribArray(1);
            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            RestoreRenderState(gl, previous);
        }

        /// <summary>
        /// Converts a GDI+ bitmap into an RGBA OpenGL texture. The source alpha is
        /// copied into all colour channels so the shader can sample the red channel.
        /// </summary>
        private uint UploadGlyph(OpenGL gl, Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bits = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
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
                gl.GenTextures(1, ids);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, ids[0]);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
                gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
                gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA,
                    bitmap.Width, bitmap.Height, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, ptr);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
                return ids[0];
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        /// <summary>Uploads camera matrices and one item model matrix to a shader program.</summary>
        internal void UploadSceneMatrices(OpenGL gl, uint program, CxMatrix4X4 model)
        {
            EnsureSceneMatrices(gl);
            gl.UniformMatrix4(gl.GetUniformLocation(program, "projection"), 1, true, _projection);
            gl.UniformMatrix4(gl.GetUniformLocation(program, "view"), 1, true, _view);
            gl.UniformMatrix4(gl.GetUniformLocation(program, "model"), 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
        }

        /// <summary>Uploads scene matrices using locations cached with the resource handle.</summary>
        internal void UploadSceneMatrices(OpenGL gl, GLResourceHandle handle, CxMatrix4X4 model)
        {
            EnsureSceneMatrices(gl);
            gl.UniformMatrix4(handle.GetUniformLocation(gl, "projection"), 1, true, _projection);
            gl.UniformMatrix4(handle.GetUniformLocation(gl, "view"), 1, true, _view);
            gl.UniformMatrix4(handle.GetUniformLocation(gl, "model"), 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
        }

        /// <summary>Gets or creates the model-stack state for a GL wrapper.</summary>
        private State GetState(OpenGL gl)
        {
            State state;
            if (!_states.TryGetValue(gl, out state))
            {
                state = new State();
                _states.Add(gl, state);
            }
            return state;
        }

        /// <summary>Gets the context-owned shader shared by the built-in surface items.</summary>
        internal uint SurfaceProgram => _surfaceProgram;

        /// <summary>Compiles the built-in surface shader once per GL context.</summary>
        internal bool EnsureSurfaceProgram(OpenGL gl)
        {
            if (_surfaceProgram != 0) return true;
            if (_surfaceProgramFailed) return false;

            _surfaceProgram = ShaderProgramCompiler.CompileProgram(
                gl,
                AbstractCxObjRenderItem.SharedVertexShaderSource,
                AbstractCxObjRenderItem.SharedFragmentShaderSource);
            if (_surfaceProgram == 0)
            {
                _surfaceProgramFailed = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Draws instanced unit spheres for a point overlay. The sphere mesh and shader are
        /// context-owned; only the instance positions are streamed for each draw.
        /// </summary>
        internal void DrawSpheres(OpenGL gl, float[] positions, Color color, float radius,
            CxMatrix4X4 model)
        {
            if (positions == null || positions.Length < 3) return;
            if (!EnsureSphereProgram(gl)) return;
            EnsureSphereBuffers(gl);

            EnsureSceneMatrices(gl);
            gl.BindVertexArray(_sphereVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _sphereInstanceVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, positions, OpenGL.GL_STREAM_DRAW);

            gl.UseProgram(_sphereProgram);
            gl.UniformMatrix4(_sphereProjection, 1, true, _projection);
            gl.UniformMatrix4(_sphereView, 1, true, _view);
            gl.UniformMatrix4(_sphereModel, 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
            gl.Uniform1(_sphereRadius, radius);
            gl.Uniform4(_sphereColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            gl.DrawElementsInstanced(OpenGL.GL_TRIANGLES, _sphereIndexCount,
                OpenGL.GL_UNSIGNED_SHORT, IntPtr.Zero, positions.Length / 3);

            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.UseProgram(0);
        }

        private bool EnsureSphereProgram(OpenGL gl)
        {
            if (_sphereProgram != 0) return true;
            if (_sphereProgramFailed) return false;

            _sphereProgram = ShaderProgramCompiler.CompileProgram(gl, SphereVertex, SphereFragment);
            if (_sphereProgram == 0)
            {
                _sphereProgramFailed = true;
                return false;
            }
            _sphereProjection = gl.GetUniformLocation(_sphereProgram, "projection");
            _sphereView = gl.GetUniformLocation(_sphereProgram, "view");
            _sphereModel = gl.GetUniformLocation(_sphereProgram, "model");
            _sphereColor = gl.GetUniformLocation(_sphereProgram, "aColor");
            _sphereRadius = gl.GetUniformLocation(_sphereProgram, "radius");
            return true;
        }

        private void EnsureSphereBuffers(OpenGL gl)
        {
            if (_sphereVao != 0) return;

            var ids = new uint[1];
            gl.GenVertexArrays(1, ids);
            _sphereVao = ids[0];
            gl.BindVertexArray(_sphereVao);

            gl.GenBuffers(1, ids);
            _sphereVbo = ids[0];
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _sphereVbo);
            var vertices = new List<float>();
            var indices = new List<ushort>();
            GenerateUnitSphereData(10, 10, vertices, indices);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices.ToArray(), OpenGL.GL_STATIC_DRAW);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false,
                3 * sizeof(float), IntPtr.Zero);

            gl.GenBuffers(1, ids);
            _sphereEbo = ids[0];
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, _sphereEbo);
            gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indices.ToArray(), OpenGL.GL_STATIC_DRAW);
            _sphereIndexCount = indices.Count;

            gl.GenBuffers(1, ids);
            _sphereInstanceVbo = ids[0];
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _sphereInstanceVbo);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, OpenGL.GL_FLOAT, false,
                3 * sizeof(float), IntPtr.Zero);
            gl.VertexAttribDivisor(1, 1);

            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
        }

        private static void GenerateUnitSphereData(int slices, int stacks,
            List<float> vertices, List<ushort> indices)
        {
            for (int stack = 0; stack <= stacks; stack++)
            {
                float phi = (float)(Math.PI / 2 - stack * Math.PI / stacks);
                float y = (float)Math.Sin(phi);
                float scale = (float)Math.Cos(phi);

                for (int slice = 0; slice <= slices; slice++)
                {
                    float theta = (float)(slice * 2 * Math.PI / slices);
                    vertices.Add(scale * (float)Math.Cos(theta));
                    vertices.Add(y);
                    vertices.Add(scale * (float)Math.Sin(theta));
                }
            }

            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    ushort first = (ushort)(stack * (slices + 1) + slice);
                    ushort second = (ushort)(first + (ushort)(slices + 1));
                    indices.Add(first);
                    indices.Add(second);
                    indices.Add((ushort)(first + 1));
                    indices.Add(second);
                    indices.Add((ushort)(second + 1));
                    indices.Add((ushort)(first + 1));
                }
            }
        }

        /// <summary>Compiles the shared geometry program once per GL context.</summary>
        private bool EnsureGeometryProgram(OpenGL gl)
        {
            if (_geometryProgram != 0) return true;
            if (_geometryProgramFailed) return false;
            _geometryProgram = ShaderProgramCompiler.CompileProgram(gl, GeometryVertex, GeometryFragment);
            if (_geometryProgram == 0)
            {
                _geometryProgramFailed = true;
                return false;
            }
            _geometryProjection = gl.GetUniformLocation(_geometryProgram, "projection");
            _geometryView = gl.GetUniformLocation(_geometryProgram, "view");
            _geometryModel = gl.GetUniformLocation(_geometryProgram, "model");
            _geometryUseVertexColor = gl.GetUniformLocation(_geometryProgram, "useVertexColor");
            _geometryColor = gl.GetUniformLocation(_geometryProgram, "color");
            return true;
        }

        /// <summary>Allocates the reusable geometry VAO/VBO on first use.</summary>
        private void EnsureGeometryBuffers(OpenGL gl)
        {
            if (_geometryVao == 0)
            {
                var ids = new uint[1];
                gl.GenVertexArrays(1, ids);
                _geometryVao = ids[0];
            }
            if (_geometryVbo == 0)
            {
                var ids = new uint[1];
                gl.GenBuffers(1, ids);
                _geometryVbo = ids[0];
            }
        }

        /// <summary>Compiles the screen-text program once per GL context.</summary>
        private bool EnsureTextProgram(OpenGL gl)
        {
            if (_textProgram != 0) return true;
            if (_textProgramFailed) return false;
            _textProgram = ShaderProgramCompiler.CompileProgram(gl, TextVertex, TextFragment);
            if (_textProgram == 0)
            {
                _textProgramFailed = true;
                return false;
            }
            _textProjection = gl.GetUniformLocation(_textProgram, "projection");
            _textColor = gl.GetUniformLocation(_textProgram, "color");
            _textGlyph = gl.GetUniformLocation(_textProgram, "glyph");
            return true;
        }

        /// <summary>Allocates the reusable text quad VAO/VBO on first use.</summary>
        private void EnsureTextBuffers(OpenGL gl)
        {
            if (_textVao == 0)
            {
                var ids = new uint[1];
                gl.GenVertexArrays(1, ids);
                _textVao = ids[0];
            }
            if (_textVbo == 0)
            {
                var ids = new uint[1];
                gl.GenBuffers(1, ids);
                _textVbo = ids[0];
            }
        }

        /// <summary>Reads mutable GL state that overlay draws temporarily change.</summary>
        private RenderStateSnapshot CaptureRenderState(OpenGL gl)
        {
            return _frameStateCaptured ? _frameState : CaptureRenderStateUncached(gl);
        }

        /// <summary>
        /// Captures the GL state a draw may change, so the caller can restore it afterwards.
        /// During a <see cref="BeginFrame"/> window this returns the frame-start snapshot, which
        /// is the state every item in the frame should observe; outside a frame it queries GL.
        /// </summary>
        internal RenderStateSnapshot CaptureState(OpenGL gl) => CaptureRenderState(gl);

        /// <summary>Restores state captured by <see cref="CaptureState"/>.</summary>
        internal void RestoreState(OpenGL gl, RenderStateSnapshot state)
            => RestoreRenderState(gl, state);

        private RenderStateSnapshot CaptureRenderStateUncached(OpenGL gl)
        {
            gl.GetFloat(OpenGL.GL_POINT_SIZE, _pointSizeQuery);
            gl.GetFloat(OpenGL.GL_LINE_WIDTH, _lineWidthQuery);
            return new RenderStateSnapshot
            {
                DepthTest = gl.IsEnabled(OpenGL.GL_DEPTH_TEST),
                DepthWrite = GetDepthWrite(gl),
                Blend = gl.IsEnabled(OpenGL.GL_BLEND),
                PointSize = _pointSizeQuery[0],
                LineWidth = _lineWidthQuery[0]
            };
        }

        /// <summary>
        /// Applies the depth/blend state required by one overlay draw. Translucent draws
        /// (<paramref name="blend"/>) also stop writing depth, otherwise the translucent
        /// surface would occlude geometry drawn later and could not blend with what is
        /// already in the framebuffer. <see cref="RestoreRenderState"/> puts it back.
        /// </summary>
        private static void SetRenderState(OpenGL gl, bool depth, bool blend)
        {
            if (depth) gl.Enable(OpenGL.GL_DEPTH_TEST); else gl.Disable(OpenGL.GL_DEPTH_TEST);
            if (blend)
            {
                gl.Enable(OpenGL.GL_BLEND);
                gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
                gl.DepthMask(0);
            }
            else
            {
                gl.Disable(OpenGL.GL_BLEND);
            }
        }

        /// <summary>Restores state captured before an overlay draw.</summary>
        private static void RestoreRenderState(OpenGL gl, RenderStateSnapshot state)
        {
            if (state.DepthTest) gl.Enable(OpenGL.GL_DEPTH_TEST); else gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.DepthMask((byte)(state.DepthWrite ? 1 : 0));
            if (state.Blend) gl.Enable(OpenGL.GL_BLEND); else gl.Disable(OpenGL.GL_BLEND);
            gl.PointSize(state.PointSize);
            gl.LineWidth(state.LineWidth);
        }

        /// <summary>Reads the depth write mask through SharpGL's boolean query API.</summary>
        private bool GetDepthWrite(OpenGL gl)
        {
            gl.GetBooleanv(OpenGL.GL_DEPTH_WRITEMASK, _depthWriteQuery);
            return _depthWriteQuery[0] != 0;
        }

        /// <summary>
        /// Deletes programs, buffers, textures, and model-stack state owned by this
        /// renderer. The caller must have made the owning context current.
        /// </summary>
        private void ReleasePrograms(OpenGL gl)
        {
            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            if (_geometryProgram != 0)
            {
                gl.DeleteProgram(_geometryProgram);
                _geometryProgram = 0;
            }
            if (_surfaceProgram != 0)
            {
                gl.DeleteProgram(_surfaceProgram);
                _surfaceProgram = 0;
            }
            _surfaceProgramFailed = false;
            _geometryProgramFailed = false;
            _textProgramFailed = false;
            _geometryProjection = _geometryView = _geometryModel = -1;
            _geometryUseVertexColor = _geometryColor = -1;
            if (_textProgram != 0)
            {
                gl.DeleteProgram(_textProgram);
                _textProgram = 0;
            }
            _textProjection = _textColor = _textGlyph = -1;
            if (_sphereProgram != 0)
            {
                gl.DeleteProgram(_sphereProgram);
                _sphereProgram = 0;
            }
            _sphereProgramFailed = false;
            _sphereProjection = _sphereView = _sphereModel = -1;
            _sphereColor = _sphereRadius = -1;
            if (_geometryVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _geometryVbo });
                _geometryVbo = 0;
            }
            if (_geometryVao != 0)
            {
                gl.DeleteVertexArrays(1, new[] { _geometryVao });
                _geometryVao = 0;
            }
            if (_textVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _textVbo });
                _textVbo = 0;
            }
            if (_textVao != 0)
            {
                gl.DeleteVertexArrays(1, new[] { _textVao });
                _textVao = 0;
            }
            if (_sphereInstanceVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _sphereInstanceVbo });
                _sphereInstanceVbo = 0;
            }
            if (_sphereEbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _sphereEbo });
                _sphereEbo = 0;
            }
            if (_sphereVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _sphereVbo });
                _sphereVbo = 0;
            }
            if (_sphereVao != 0)
            {
                gl.DeleteVertexArrays(1, new[] { _sphereVao });
                _sphereVao = 0;
            }
            _sphereIndexCount = 0;
            foreach (var cached in _textTextures.Values)
                if (cached.Texture != 0)
                    gl.DeleteTextures(1, new[] { cached.Texture });
            _textTextures.Clear();
            _textTextureOrder.Clear();
            _textTexturesUsedThisFrame.Clear();
            foreach (var atlas in _glyphAtlases.Values)
                if (atlas.Texture != 0)
                    gl.DeleteTextures(1, new[] { atlas.Texture });
            _glyphAtlases.Clear();
            _states.Clear();
            _frameStateCaptured = false;
            _hasSceneMatrices = false;
            _viewportWidth = _viewportHeight = -1;
        }

        /// <summary>
        /// Preserves the standalone public text helpers' historical behaviour by
        /// using the caller's active fixed-function matrices when no CxDisplay frame
        /// has supplied explicit scene matrices.
        /// </summary>
        private void ReadCurrentGLMatrices(OpenGL gl)
        {
            gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, _projectionQuery);
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, _viewQuery);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    _projection[row * 4 + col] = _projectionQuery[col * 4 + row];
                    _view[row * 4 + col] = _viewQuery[col * 4 + row];
                }
        }

        /// <summary>
        /// Supplies the caller's active fixed-function matrices for standalone draws.
        /// CxDisplay sets explicit matrices for a frame, so its shader path does not
        /// depend on whatever legacy matrix state happens to be active.
        /// </summary>
        internal void EnsureSceneMatrices(OpenGL gl)
        {
            if (!_hasSceneMatrices)
                ReadCurrentGLMatrices(gl);
        }

        /// <summary>Expands 2D screen vertices to z=0 positions for the geometry shader.</summary>
        private float[] To3D(float[] values)
        {
            int required = values.Length / 2 * 3;
            EnsureCapacity(ref _screenPositions3D, required);
            float[] result = _screenPositions3D;
            for (int i = 0; i < values.Length / 2; i++)
            {
                result[i * 3] = values[i * 2];
                result[i * 3 + 1] = values[i * 2 + 1];
                result[i * 3 + 2] = 0f;
            }
            return result;
        }

        private static void EnsureCapacity(ref float[] buffer, int required)
        {
            if (buffer.Length < required)
                buffer = new float[required];
        }

        private static void Multiply4x4(float[] left, float[] right, float[] result)
        {
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                    result[row * 4 + col] = left[row * 4] * right[col]
                        + left[row * 4 + 1] * right[4 + col]
                        + left[row * 4 + 2] * right[8 + col]
                        + left[row * 4 + 3] * right[12 + col];
        }

        private void RefreshViewport(OpenGL gl)
        {
            if (gl.RenderContextProvider == null) return;
            int width = gl.RenderContextProvider.Width;
            int height = gl.RenderContextProvider.Height;
            if (width == _viewportWidth && height == _viewportHeight) return;
            gl.GetInteger(OpenGL.GL_VIEWPORT, _viewport);
            _viewportWidth = width;
            _viewportHeight = height;
        }

        /// <summary>Returns an identity matrix in the renderer's row-major layout.</summary>
        private static float[] IdentityData() => new float[]
        {
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1
        };

        /// <summary>Builds the pixel-to-NDC orthographic matrix for a viewport.</summary>
        private float[] Ortho(int width, int height)
        {
            // Row-major matrix with the translation in the last column. The
            // renderer uploads row-major arrays with transpose=true, so this
            // maps pixel coordinates (0,0) to the lower-left NDC corner.
            _ortho[0] = 2f / Math.Max(1, width); _ortho[1] = 0; _ortho[2] = 0; _ortho[3] = -1;
            _ortho[4] = 0; _ortho[5] = 2f / Math.Max(1, height); _ortho[6] = 0; _ortho[7] = -1;
            _ortho[8] = 0; _ortho[9] = 0; _ortho[10] = -1; _ortho[11] = 0;
            _ortho[12] = 0; _ortho[13] = 0; _ortho[14] = 0; _ortho[15] = 1;
            return _ortho;
        }

    }
}
