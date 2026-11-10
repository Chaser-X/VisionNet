using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Answers picking queries for <see cref="CxDisplay"/>: turns a screen pixel into a world-space
    /// point through the depth buffer and finds the surface point nearest to it across the
    /// registered items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The depth value comes from <see cref="CxMsaaRenderTarget"/> — a single-sample resolve of the
    /// multisampled scene, so MSAA on the display does not corrupt it. When no render target is
    /// available (fallback path) the window's own depth buffer is read instead.
    /// </para>
    /// <para>
    /// Reading depth needs the GL context to be current, which makes the read methods
    /// <b>UI-thread only</b> and valid only while the context is bound (inside a frame, or wrapped
    /// in <see cref="CxGlContext.RunOnContext{T}"/>). <see cref="FindNearest(CxItemRegistry, CxPoint3D, SurfaceColorMode)"/>
    /// is pure CPU and has no such restriction.
    /// </para>
    /// </remarks>
    internal sealed class CxPickingService
    {
        // Reused so a query allocates nothing.
        private readonly List<ICxObjRenderItem> _snapshot = new List<ICxObjRenderItem>();

        /// <summary>
        /// Reads the depth at the given screen pixel and un-projects it to a world-space point.
        /// Returns <c>null</c> when the pixel hits the far plane (no geometry present).
        /// </summary>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="mouseX">Pixel X in control coordinates.</param>
        /// <param name="mouseY">Pixel Y in control coordinates (top-down).</param>
        public CxPoint3D? ScreenToWorld(CxGlContext gl, int mouseX, int mouseY)
        {
            if (gl == null || !gl.IsValid) return null;
            // Runs outside the render frame when called from a mouse handler, so the context is
            // bound explicitly (OpenTK does not make it current implicitly).
            return gl.RunOnContext<CxPoint3D?>(() =>
            {
                float? depth = CxMsaaRenderTarget.ReadDepthAtPixel(gl, mouseX, mouseY);
                return depth.HasValue ? WorldFromDepth(gl, mouseX, mouseY, depth.Value) : null;
            });
        }

        /// <summary>
        /// Finds the surface point nearest to the given screen pixel across all active surface items.
        /// </summary>
        public (CxPoint3D? Location, float? Intensity) FindNearest(CxItemRegistry registry,
            CxGlContext gl, int mouseX, int mouseY, SurfaceColorMode colorMode)
        {
            var world = ScreenToWorld(gl, mouseX, mouseY);
            return world.HasValue
                ? FindNearest(registry, world.Value, colorMode)
                : (null, null);
        }

        /// <summary>
        /// Same as the pixel overload but with the depth value already read (e.g. from the
        /// asynchronous pixel-pack read), so no second readback is needed.
        /// </summary>
        public (CxPoint3D? Location, float? Intensity) FindNearestFromDepth(CxItemRegistry registry,
            CxGlContext gl, int mouseX, int mouseY, float depth, SurfaceColorMode colorMode)
        {
            var world = WorldFromDepth(gl, mouseX, mouseY, depth);
            return world.HasValue
                ? FindNearest(registry, world.Value, colorMode)
                : (null, null);
        }

        /// <summary>
        /// Finds the surface point nearest to an already-unprojected world position.
        /// </summary>
        /// <remarks>
        /// Structured surfaces use a 5×5 neighbourhood grid search; mesh-type items and unordered
        /// point clouds contribute the un-projected world coordinate directly. Each structured
        /// surface is searched in its <b>local</b> frame — the world-space picking point is
        /// transformed by the inverse model matrix before grid indexing — so surfaces rendered with
        /// a pose (model matrix) still report the correct grid cell and intensity.
        /// </remarks>
        public (CxPoint3D? Location, float? Intensity) FindNearest(CxItemRegistry registry,
            CxPoint3D world, SurfaceColorMode colorMode)
        {
            // Reuse a scratch buffer: this runs on every hover frame.
            registry.CopyItemsInto(_snapshot);
            if (_snapshot.Count == 0)
            {
                _snapshot.Clear();
                return (null, null);
            }

            CxPoint3D? bestPoint     = null;
            float?     bestIntensity = null;
            float      bestDist      = float.MaxValue;
            bool       useDiff       = colorMode == SurfaceColorMode.Diff;

            foreach (var cur in _snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                // Each item knows how it answers a pick query (grid search vs. the query point
                // itself), so this loop needs no per-type branching. Only the built-in surface
                // items implement the contract; anything else contributes nothing.
                if (!(cur is AbstractCxObjRenderItem item)) continue;
                item.PickAt(world, useDiff, ref bestDist, ref bestPoint, ref bestIntensity);
            }

            // Do not keep references to (possibly disposed) items in the scratch buffer.
            _snapshot.Clear();
            return (bestPoint, bestIntensity);
        }

        /// <summary>
        /// Un-projects a depth-buffer value at a control-space pixel to world space. Returns
        /// <c>null</c> at the far plane.
        /// </summary>
        private static CxPoint3D? WorldFromDepth(CxGlContext gl, int mouseX, int mouseY, float depth)
        {
            if (Math.Abs(depth - 1.0f) < 0.00001f) return null;

            // Control space is top-down; GL viewport space is bottom-up. Use the pixel centre so the
            // un-projection matches the texel that was read.
            int glY = gl.Height - 1 - mouseY;

            CxPoint3D world;
            if (!ModernGLRenderer.For(gl).Unproject(mouseX + 0.5f, glY + 0.5f, depth, out world))
                return null;
            return world;
        }
    }
}
