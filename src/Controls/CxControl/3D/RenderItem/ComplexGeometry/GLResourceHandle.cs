using System;
using System.Collections.Generic;
using SharpGL;

namespace VisionNet.Controls
{
    /// <summary>
    /// Stores all OpenGL resource IDs managed by <see cref="CxDisplay"/> on behalf of one render item.
    /// Items never hold or release these IDs directly; <c>CxDisplay</c> owns their lifecycle.
    /// </summary>
    /// <remarks>
    /// The type is public only because it appears in the <see cref="ICxObjRenderItem.Draw"/> and
    /// <see cref="ICxObjRenderItem.PrepareRenderData"/> contract. Its state is <c>internal</c>:
    /// <see cref="CxDisplay"/> creates and mutates handles, and items must not.
    /// </remarks>
    public class GLResourceHandle
    {
        /// <summary>
        /// VBO IDs for vertex data. Index 0 = positions, 1 = colours or UVs,
        /// 2 = difference values, 3 = per-vertex intensity.
        /// Allocated count is stored in <see cref="VboCount"/>.
        /// </summary>
        internal uint[] VboIds { get; set; } = new uint[4];

        /// <summary>Vertex Array Object ID. Valid only when <see cref="HasVAO"/> is <c>true</c>.</summary>
        internal uint VaoId { get; set; }

        /// <summary>Element Buffer Object ID. Valid only when <see cref="HasEBO"/> is <c>true</c>.</summary>
        internal uint ElementBufferId { get; set; }

        /// <summary>Linked shader program ID. Valid only when <see cref="HasShader"/> is <c>true</c>.</summary>
        internal uint ShaderProgram { get; set; }

        /// <summary>2D texture ID. Valid only when <see cref="HasTexture"/> is <c>true</c>.</summary>
        internal uint TextureId { get; set; }

        /// <summary>
        /// <c>true</c> once all GL resources have been successfully created and are ready to draw.
        /// </summary>
        internal bool IsValid { get; set; }

        /// <summary>
        /// Set to <c>true</c> when the item's CPU data changed and GPU buffers must be rebuilt.
        /// Cleared by <see cref="CxDisplay"/> after recreation.
        /// </summary>
        internal bool NeedsUpdate { get; set; }

        /// <summary>
        /// <c>true</c> when the item's shader failed to compile for the current data.
        /// Compilation failures are deterministic, so <see cref="CxDisplay"/> skips
        /// further attempts until <see cref="NeedsUpdate"/> is raised again.
        /// </summary>
        internal bool ShaderFailed { get; set; }

        /// <summary>Whether a VAO was created for this handle.</summary>
        internal bool HasVAO { get; set; }

        /// <summary>Whether a shader program was compiled and linked for this handle.</summary>
        internal bool HasShader { get; set; }

        /// <summary>
        /// <c>true</c> when this handle owns <see cref="ShaderProgram"/> and may delete it.
        /// The built-in ComplexGeometry items share one context-owned surface program.
        /// </summary>
        internal bool OwnsShader { get; set; }

        /// <summary>Whether a 2D texture was uploaded for this handle.</summary>
        internal bool HasTexture { get; set; }

        /// <summary>Whether an Element Buffer Object (index buffer) was created for this handle.</summary>
        internal bool HasEBO { get; set; }

        /// <summary>Handles are created by <see cref="CxDisplay"/> only.</summary>
        internal GLResourceHandle() { }

        /// <summary>
        /// The delegate subscribed to the owning item's <c>OnRenderDataChanged</c> event, so the
        /// display can unsubscribe exactly the same instance it subscribed.
        /// </summary>
        internal Action RenderDataChangedHandler { get; set; }

        /// <summary>Number of VBOs actually allocated in <see cref="VboIds"/>.</summary>
        internal int VboCount { get; set; }

        /// <summary>
        /// <c>true</c> when the second VBO slot contains UV coordinates rather than RGB colours.
        /// </summary>
        internal bool UseUVMode { get; set; }

        /// <summary>
        /// Caches uniform locations for the linked program. Locations are immutable
        /// until the program is rebuilt, so render loops can avoid repeated driver
        /// lookups.
        /// </summary>
        internal readonly Dictionary<string, int> UniformLocations
            = new Dictionary<string, int>(System.StringComparer.Ordinal);

        internal int GetUniformLocation(OpenGL gl, string name)
        {
            int location;
            if (!UniformLocations.TryGetValue(name, out location))
            {
                location = gl.GetUniformLocation(ShaderProgram, name);
                UniformLocations[name] = location;
            }
            return location;
        }

        /// <summary>
        /// Forgets resource names from a context that has already been destroyed or replaced.
        /// This deliberately makes no GL calls; the names are invalid outside their owning
        /// context and must not be deleted through the new context.
        /// </summary>
        internal void ResetForContextRecreate()
        {
            VaoId = 0;
            if (VboIds == null)
                VboIds = new uint[4];
            else
                Array.Clear(VboIds, 0, VboIds.Length);
            ElementBufferId = 0;
            ShaderProgram = 0;
            TextureId = 0;

            IsValid = false;
            NeedsUpdate = true;
            ShaderFailed = false;
            HasVAO = false;
            HasShader = false;
            OwnsShader = false;
            HasTexture = false;
            HasEBO = false;
            VboCount = 0;
            UseUVMode = false;
            UniformLocations.Clear();
        }
    }
}
