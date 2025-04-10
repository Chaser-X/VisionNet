using SharpGL;
using SharpGL.SceneGraph;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;
using static System.Net.Mime.MediaTypeNames;

namespace VisionNet.Controls
{
    public class CxTextInfoItem : RenderAbstractItem
    {
        public Dictionary<TextInfo, Color> TextInfoColors { get; set; } = new Dictionary<TextInfo, Color>();
        public CxTextInfoItem(Dictionary<TextInfo, Color> textInfoColors)
        {
            this.TextInfoColors = textInfoColors;
        }
        public override void Draw(OpenGL gl)
        {
            if (TextInfoColors.Count == 0) return;

            foreach (var kvp in TextInfoColors)
            {
                var textInfo = kvp.Key;
                var color = kvp.Value;
                // 将3D坐标转换为屏幕坐标（包括深度信息）
                var objCoord = new Vertex(textInfo.Location.X, textInfo.Location.Y, textInfo.Location.Z);
                var screenCoord = gl.Project(objCoord);
                // 判断是否在屏幕范围内
                if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                    screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
                {
                    return;
                }
                // 如果需要考虑透视范围，可以检查 screenCoord.Z（假设其为归一化深度：0～1）
                if (screenCoord.Z < 0 || screenCoord.Z > 1)
                {
                    return;
                }
                gl.DrawText((int)screenCoord.X, (int)screenCoord.Y, (float)(color.R / 255.0), (float)(color.G / 255.0), (float)(color.B / 255.0), "Arial", textInfo.Size, textInfo.Text);
            }
        }
    }
}
