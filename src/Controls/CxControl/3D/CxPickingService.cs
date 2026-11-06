using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Answers picking queries for <see cref="CxDisplay"/>: un-projects a screen pixel through the
    /// depth buffer and finds the surface point nearest to it across the registered items.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="CxDisplay"/> so the picking maths is independent of the control's
    /// frame pipeline and event plumbing. Holds per-query scratch buffers, so an instance is not
    /// thread-safe: the depth readback needs the GL context to be current, which makes every
    /// method <b>UI-thread only</b> and valid only inside a frame.
    /// </remarks>
    internal sealed class CxPickingService
    {
        // Reused so a hover query allocates nothing before the synchronous ReadPixels.
        private readonly int[] _viewport = new int[4];
        private readonly float[] _depth = new float[1];
        private readonly List<ICxObjRenderItem> _snapshot = new List<ICxObjRenderItem>();

        /// <summary>
        /// Reads the depth buffer at the given screen pixel and un-projects it to a world-space
        /// point. Returns <c>null</c> when the pixel hits the far plane (no geometry present).
        /// </summary>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="mouseX">Pixel X in control coordinates.</param>
        /// <param name="mouseY">Pixel Y in control coordinates (top-down).</param>
        public CxPoint3D? ScreenToWorld(CxGlContext gl, int mouseX, int mouseY)
        {
            if (gl == null || !gl.IsValid) return null;
            // OpenTK's GL entry points act on the current context and do not make it current
            // implicitly (unlike the old SharpGL wrapper). A mouse handler may call this outside a
            // frame, so bind the context first.
            gl.MakeCurrent();

            GL.GetInteger(GetPName.Viewport, _viewport);
            int adjustedY = _viewport[3] - mouseY;   // OpenGL Y is bottom-up.

            GL.ReadPixels(mouseX, adjustedY, 1, 1,
                PixelFormat.DepthComponent, PixelType.Float, _depth);
            float depth = _depth[0];

            if (Math.Abs(depth - 1.0f) < 0.00001f) return null;

            CxPoint3D world;
            if (!ModernGLRenderer.For(gl).Unproject(gl, mouseX, adjustedY, depth, out world))
                return null;
            return world;
        }

        /// <summary>
        /// Finds the surface point nearest to the given screen pixel across all active surface items.
        /// </summary>
        /// <param name="registry">Registry holding the items to search.</param>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="mouseX">Pixel X in control coordinates.</param>
        /// <param name="mouseY">Pixel Y in control coordinates (top-down).</param>
        /// <param name="colorMode">Display colour mode; Diff selects diff values over intensity.</param>
        /// <returns>Nearest world-space location and its intensity, or <c>(null, null)</c> if no hit.</returns>
        public (CxPoint3D? Location, float? Intensity) FindNearest(CxItemRegistry registry,
            CxGlContext gl, int mouseX, int mouseY, SurfaceColorMode colorMode)
        {
            var world = ScreenToWorld(gl, mouseX, mouseY);
            return world.HasValue
                ? FindNearest(registry, world.Value, colorMode)
                : (null, null);
        }

        /// <summary>
        /// Finds the surface point nearest to an already-unprojected world position, so a caller
        /// that has already paid for the depth readback at this pixel does not repeat it.
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
    }
}
