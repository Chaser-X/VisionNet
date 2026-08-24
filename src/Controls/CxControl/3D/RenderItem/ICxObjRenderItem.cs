using System;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Interface for primary renderable objects (point clouds, meshes).
    /// Item owns CPU data; CxDisplay owns GL resources.
    /// </summary>
    public interface ICxObjRenderItem : IDisposable
    {
        /// <summary>Raised when the item is disposed.</summary>
        event Action OnDisposed;

        /// <summary>
        /// Raised when cached render data becomes invalid (e.g., color mode change).
        /// CxDisplay sets the corresponding handle.NeedsUpdate to true.
        /// </summary>
        event Action OnRenderDataChanged;

        /// <summary>Gets whether the item has been disposed.</summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets or sets the lower bound of the active colour-mapping range. The value this
        /// holds depends on <see cref="SurfaceColorMode"/>: in Color / ColorWithIntensity it
        /// is a Z (height) range, in Diff it is a diff-value range, and in Intensity / Lit it
        /// is unused. It is normally maintained by <see cref="CxDisplay"/> — reset on
        /// <see cref="SurfaceColorMode"/> changes and propagated each frame via
        /// <see cref="SetGlobalZRange"/> / <see cref="SetGlobalDiffRange"/> from the
        /// auto-aggregated global range or a manual override (<c>CxDisplay.SetColorRange</c> /
        /// <c>SetDiffRange</c>). For the item's own auto-computed baseline, independent of any
        /// override, use <see cref="BaseZMin"/> / <see cref="BaseDiffMin"/>.
        /// </summary>
        float ColorMin { get; set; }

        /// <summary>See <see cref="ColorMin"/>.</summary>
        float ColorMax { get; set; }

        /// <summary>
        /// Gets the auto-computed base Z (height) range from the geometry, independent of
        /// any manually set or propagated range. Used by <see cref="CxDisplay"/> to aggregate
        /// the global colour range without being polluted by the active <see cref="ColorMin"/>/
        /// <see cref="ColorMax"/> (which may hold a manual override).
        /// </summary>
        float BaseZMin { get; }

        /// <summary>See <see cref="BaseZMin"/>.</summary>
        float BaseZMax { get; }

        /// <summary>
        /// Gets the auto-computed base diff value range from the data object's diff channel,
        /// independent of any manually set or propagated range. Used by
        /// <see cref="CxDisplay"/> to aggregate the global diff range.
        /// </summary>
        float BaseDiffMin { get; }

        /// <summary>See <see cref="BaseDiffMin"/>.</summary>
        float BaseDiffMax { get; }

        /// <summary>Gets the bounding box enclosing all geometry data.</summary>
        CxBox3D? BoundingBox { get; }

        /// <summary>Gets or sets the color rendering mode.</summary>
        SurfaceColorMode SurfaceColorMode { get; set; }

        /// <summary>Gets or sets the surface rendering mode (PointCloud or Mesh).</summary>
        SurfaceMode SurfaceMode { get; set; }

        /// <summary>
        /// Called by <see cref="CxDisplay"/> before each frame with the global diff range
        /// across all items (or the manually set diff range). Only items in
        /// <see cref="SurfaceColorMode.Diff"/> apply it; others ignore it. Symmetric to
        /// <see cref="SetGlobalZRange"/>: lightweight uniform / cache update only.
        /// </summary>
        void SetGlobalDiffRange(float min, float max);

        /// <summary>Prepares CPU-side render data. Does not make GL calls.</summary>
        RenderData PrepareRenderData();

        /// <summary>Renders the item using the provided GL resource handle.</summary>
        void Draw(OpenGL gl, GLResourceHandle handle);

        /// <summary>
        /// Called by CxDisplay before each frame with the global Z range across all items.
        /// Ensures color rendering is consistent with the color bar.
        /// </summary>
        void SetGlobalZRange(float zMin, float zMax);
    }
}
