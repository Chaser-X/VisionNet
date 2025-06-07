using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;
using static System.Net.Mime.MediaTypeNames;

namespace VisionNet.Controls
{
    public class CxText2DItem : AbstractRenderItem
    {
        public Text2D[] TextItems { get; private set; }

        public CxText2DItem(Text2D[] textItems, Color color, float size = 1f) : base(color, size)
        {
            if (textItems == null || textItems.Length == 0)
            {
                throw new ArgumentNullException(nameof(textItems));
            }
            TextItems = textItems;
        }

        public override void Draw(OpenGL gl)
        {
            if (TextItems == null || TextItems.Length == 0)
            {
                return; // 没有文本需要绘制
            }

            // 关闭深度测试
            gl.Disable(OpenGL.GL_DEPTH_TEST);

            // 设置 2D 正交投影模式
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height, -1, 1); // 2D 投影
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            foreach (var textItem in TextItems)
            {
                // 设置文本颜色
                gl.Color(Color.R / 255.0f, Color.G / 255.0f, Color.B / 255.0f);

                // 绘制文本
                gl.DrawText((int)textItem.Location.X, (int)(gl.RenderContextProvider.Height - textItem.Location.Y), 
                            Color.R / 255.0f, Color.G / 255.0f, Color.B / 255.0f,
                            "Arial", textItem.FontSize, textItem.Text);
            }

            // 恢复矩阵设置
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);

            // 恢复深度测试
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }
    }
}
