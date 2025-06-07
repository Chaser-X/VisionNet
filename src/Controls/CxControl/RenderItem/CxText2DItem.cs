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
                return; // û���ı���Ҫ����
            }

            // �ر���Ȳ���
            gl.Disable(OpenGL.GL_DEPTH_TEST);

            // ���� 2D ����ͶӰģʽ
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height, -1, 1); // 2D ͶӰ
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            foreach (var textItem in TextItems)
            {
                // �����ı���ɫ
                gl.Color(Color.R / 255.0f, Color.G / 255.0f, Color.B / 255.0f);

                // �����ı�
                gl.DrawText((int)textItem.Location.X, (int)(gl.RenderContextProvider.Height - textItem.Location.Y), 
                            Color.R / 255.0f, Color.G / 255.0f, Color.B / 255.0f,
                            "Arial", textItem.FontSize, textItem.Text);
            }

            // �ָ���������
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);

            // �ָ���Ȳ���
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }
    }
}
