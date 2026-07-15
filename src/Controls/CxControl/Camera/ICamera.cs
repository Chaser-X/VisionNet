using SharpGL;
using System;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Predefined orthographic viewing directions for the 3D camera.</summary>
    public enum ViewMode
    {
        /// <summary>No preset direction; the camera is free to rotate.</summary>
        None,

        /// <summary>Top-down view (looking along the −Z axis).</summary>
        Top,

        /// <summary>Front view (looking along the +Y axis).</summary>
        Front,

        /// <summary>Left-side view (looking along the +X axis).</summary>
        Left,

        /// <summary>Right-side view (looking along the −X axis).</summary>
        Right,
    }

    /// <summary>
    /// Defines the contract for a 3D camera that drives the OpenGL view and projection matrices.
    /// </summary>
    public interface ICamera : IDisposable
    {
        /// <summary>Gets or sets the current preset view direction.</summary>
        ViewMode ViewMode { get; set; }

        /// <summary>
        /// Gets or sets whether the camera operates in 2D orthographic mode.
        /// When <c>true</c>, perspective projection is replaced by an orthographic one
        /// and rotation is disabled.
        /// </summary>
        bool Enable2DView { get; set; }

        /// <summary>
        /// Gets or sets the world-space point around which the trackball rotates.
        /// Set to <c>null</c> to rotate around the scene centre.
        /// </summary>
        CxPoint3D? RotationPoint { get; set; }

        /// <summary>
        /// Adjusts position and zoom so that the given bounding box fills the viewport.
        /// Applies the current <see cref="ViewMode"/> rotation preset.
        /// </summary>
        /// <param name="viewBox">The bounding box to fit, or <c>null</c> to reset to the default view.</param>
        void FitView(CxBox3D? viewBox);

        /// <summary>
        /// Sets the OpenGL projection and modelview matrices for the current frame.
        /// Must be called every render frame before issuing draw calls.
        /// </summary>
        /// <param name="gl">Active OpenGL context.</param>
        void LookAtMatrix(OpenGL gl);
    }
}
