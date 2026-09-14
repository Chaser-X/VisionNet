using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// A render object that can participate in <see cref="CxGroup"/> motion: it exposes a
    /// lifetime flag and a settable model matrix (pose).
    /// </summary>
    /// <remarks>
    /// Implemented by the Advanced surface items (mesh / point cloud / structured surface)
    /// and by the world-space geometry overlay items. Screen-space HUD overlays
    /// (colour bar, coordinate tag, 2D text) implement this interface but leave
    /// <see cref="SupportsGroupTransform"/> at <c>false</c> so <see cref="CxDisplay.AddToGroup(CxGroup, ICxGroupMember)"/>
    /// rejects them.
    /// </remarks>
    public interface ICxGroupMember : ICxPoseable
    {
        /// <summary>Gets whether the underlying object has been disposed.</summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets whether this object's rendering honours <see cref="ICxPoseable.ModelMatrix"/>.
        /// <c>false</c> for screen-space overlays that are anchored to the viewport.
        /// </summary>
        bool SupportsGroupTransform { get; }
    }
}
