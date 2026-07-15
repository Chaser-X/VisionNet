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

        /// <summary>Gets or sets the minimum Z value used for color mapping.</summary>
        float ZMin { get; set; }

        /// <summary>Gets or sets the maximum Z value used for color mapping.</summary>
        float ZMax { get; set; }

        /// <summary>Gets the bounding box enclosing all geometry data.</summary>
        CxBox3D? BoundingBox { get; }

        /// <summary>Gets or sets the color rendering mode.</summary>
        SurfaceColorMode SurfaceColorMode { get; set; }

        /// <summary>Gets or sets the surface rendering mode (PointCloud or Mesh).</summary>
        SurfaceMode SurfaceMode { get; set; }

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
