using System;
using VisionNet.Compute;

namespace VisionNet
{
    /// <summary>
    /// Static vision-processing operations. Methods are organised by algorithm category
    /// across partial-class files in subdirectories:
    /// <c>Filter/</c> (filtering &amp; sampling), <c>Transform/</c> (coordinate transforms),
    /// <c>Surface/</c> (surface reconstruction &amp; mesh conversion),
    /// <c>Analysis/</c> (measurement &amp; statistics), <c>IO/</c> (file serialisation),
    /// <c>Geometry/</c> (geometric construction), <c>Feature/</c> (feature extraction),
    /// <c>Segmentation/</c> (segmentation &amp; clustering), <c>Detection/</c> (defect detection).
    /// </summary>
    public static partial class VisionOperator
    {
        // ── OpenCL lifecycle ─────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the shared OpenCL environment (platform + device + command queue).
        /// Must be called once before any GPU-accelerated operation.
        /// </summary>
        /// <returns><c>true</c> if the OpenCL context was created successfully.</returns>
        public static bool InitialLib()
        {
            bool ok = OpenCLEnvironment.Instance.Initialize();
            if (!ok)
                Console.WriteLine("Failed to initialize OpenCL environment.");
            return ok;
        }

        /// <summary>
        /// Releases all OpenCL resources (kernels, programs, command queue, context).
        /// Call once when the application exits.
        /// </summary>
        public static void DestroyLib()
        {
            OpenCLEnvironment.Instance.Cleanup();
        }
    }
}
