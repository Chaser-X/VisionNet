namespace VisionNet.DataType
{
    /// <summary>
    /// Associates a text string with a world-space anchor point and a display size.
    /// Used to render labels that follow a 3D location.
    /// </summary>
    public struct TextInfo
    {
        /// <summary>World-space anchor position of the label.</summary>
        public CxPoint3D Location;

        /// <summary>Text string to display.</summary>
        public string Text;

        /// <summary>Font size used when rendering the label.</summary>
        public float Size;

        /// <summary>Initializes a text label at the given location.</summary>
        /// <param name="location">World-space anchor point.</param>
        /// <param name="text">Label text.</param>
        /// <param name="size">Font size (default 15).</param>
        public TextInfo(CxPoint3D location, string text, float size = 15f)
        {
            Location = location;
            Text     = text;
            Size     = size;
        }
    }
}
