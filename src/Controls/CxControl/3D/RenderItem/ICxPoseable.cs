using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Implemented by render items that carry a model matrix (pose) and can therefore be
    /// driven by a <see cref="CxGroup"/> parent transform.
    /// </summary>
    /// <remarks>
    /// Surface, point-cloud, mesh, and world-space overlay items implement this interface.
    /// </remarks>
    public interface ICxPoseable
    {
        /// <summary>
        /// Gets or sets the model matrix applied to this item's geometry before the camera
        /// transform. When the item belongs to a <see cref="CxGroup"/>, this holds the
        /// composed world-space matrix (group world matrix × local matrix).
        /// </summary>
        CxMatrix4X4 ModelMatrix { get; set; }
    }
}
