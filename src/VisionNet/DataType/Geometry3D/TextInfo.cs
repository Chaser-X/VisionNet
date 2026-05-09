namespace VisionNet.DataType
{
    public struct TextInfo
    {
        public CxPoint3D Location; public string Text; public float Size;
        public TextInfo(CxPoint3D location, string text, float size = 15f) { Location = location; Text = text; Size = size; }
    }
}
