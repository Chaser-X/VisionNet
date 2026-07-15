namespace VisionNet.DataType
{
    /// <summary>
    /// Associates a text string with a 2D screen-space position and a font size.
    /// Used to render fixed screen-space labels via <c>CxText2DItem</c>.
    /// </summary>
    public struct CxText2D
    {
        /// <summary>
        /// Screen-space anchor position of the label, in pixels.
        /// Y is measured from the top of the window (UI convention).
        /// </summary>
        public CxPoint2D Location;

        /// <summary>Text string to display.</summary>
        public string Text;

        /// <summary>Font size in points.</summary>
        public int FontSize;

        /// <summary>Initializes a 2D text label at the given screen position.</summary>
        /// <param name="location">Screen-space position (top-left origin).</param>
        /// <param name="text">Label text.</param>
        /// <param name="fontSize">Font size in points (default 12).</param>
        public CxText2D(CxPoint2D location, string text, int fontSize = 12)
        {
            Location = location;
            Text     = text;
            FontSize = fontSize;
        }
    }
}
