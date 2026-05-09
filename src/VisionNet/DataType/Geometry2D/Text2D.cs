namespace VisionNet.DataType
{
    public struct Text2D
    {
        public CxPoint2D Location; public string Text; public int FontSize;
        public Text2D(CxPoint2D location, string text, int fontSize = 12) { Location = location; Text = text; FontSize = fontSize; }
    }
}
