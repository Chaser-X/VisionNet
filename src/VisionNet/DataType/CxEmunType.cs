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
}
