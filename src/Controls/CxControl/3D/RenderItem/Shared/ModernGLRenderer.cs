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
    /// Per-OpenGL-context rendering services shared by every render item: the GLSL programs
    /// (geometry, text, surface, sphere) with their reusable VAO/VBO pairs, the text-texture and
    /// glyph-atlas caches, the GL state snapshot helpers, the camera matrices and model stack,
    /// and the CPU-side project/unproject helpers.
    /// </summary>
    /// <remarks>
    /// Items only provide CPU-side vertices; they never create or destroy GL objects and never
    /// call the legacy Begin/End or matrix-stack API. One instance is kept for each
    /// <see cref="OpenGL"/> wrapper because all GL object names are local to the wrapper's current
    /// context.
    /// <para>This type is deliberately not overlay-specific: the built-in surface items acquire
    /// their shared shader program from it (<see cref="EnsureSurfaceProgram"/>) and upload their
    /// matrices through it (<see cref="UploadSceneMatrices(OpenGL, GLResourceHandle, CxMatrix4X4)"/>),
    /// and it also owns the GL state scope both kinds of item use.</para>
    /// </remarks>
    internal sealed partial class ModernGLRenderer
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
        private static readonly Dictionary<OpenGL, ModernGLRenderer> Instances
            = new Dictionary<OpenGL, ModernGLRenderer>();

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
        private ModernGLRenderer() { }

        /// <summary>
        /// Returns the renderer associated with <paramref name="gl"/>, creating it
        /// lazily before the first overlay draw.
        /// </summary>
        internal static ModernGLRenderer For(OpenGL gl)
        {
            lock (Sync)
            {
                ModernGLRenderer renderer;
                if (!Instances.TryGetValue(gl, out renderer))
                {
                    renderer = new ModernGLRenderer();
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
                ModernGLRenderer renderer;
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
    }
}
