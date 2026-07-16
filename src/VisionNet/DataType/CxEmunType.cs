namespace VisionNet.DataType
{
    /// <summary>
    /// Specifies how multiple source points that map to the same grid cell
    /// are aggregated when re-sampling a point cloud onto a uniform grid.
    /// </summary>
    public enum SampleMode
    {
        /// <summary>Keep the highest Z value in each cell.</summary>
        Max = 0,

        /// <summary>Keep the lowest Z value in each cell.</summary>
        Min = 1,

        /// <summary>Store the arithmetic mean of all Z values in each cell.</summary>
        Average = 2,
    }

    /// <summary>Grid-cell aggregation mode for GPU rasterisation.</summary>
    public enum ProjectionMode
    {
        /// <summary>Keep the highest Z value in each cell.</summary>
        Max = 0,

        /// <summary>Keep the lowest Z value in each cell.</summary>
        Min = 1,
    }

    /// <summary>Pixel element data type for <see cref="CxImage"/>.</summary>
    public enum PlainType
    {
        /// <summary>Unsigned 8-bit integer — 1 byte per element.</summary>
        UInt8 = 0,

        /// <summary>Signed 16-bit integer — 2 bytes per element.</summary>
        Int16 = 1,

        /// <summary>Signed 32-bit integer — 4 bytes per element.</summary>
        Int32 = 2,

        /// <summary>Single-precision floating-point — 4 bytes per element.</summary>
        Real  = 3,
    }
}
