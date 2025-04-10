using SharpGL;
using System;

namespace VisionNet.Controls
{
    public class CxColorBarItem : RenderAbstractItem
    {
        private float zMin;
        private float zMax;
        public CxColorBarItem(float zMin = 0, float zMax = 0)
        {
            this.zMin = zMin;
            this.zMax = zMax;
        }
        public void SetRange(float zMin, float zMax)
        {
            this.zMin = zMin;
            this.zMax = zMax;
        }
        public override void Draw(OpenGL gl)
        {
            if (zMax - zMin <= 0)
                return;

            int colorBarWidth = 20; // ��ɫ���Ŀ��
            int colorBarHeight = gl.RenderContextProvider.Height / 2; // ��ɫ���ĸ߶�
            int startX = gl.RenderContextProvider.Width - colorBarWidth - 10; // ��ɫ������ʼλ�ã��Ҳ࣬����һЩ�߾ࣩ
            int startY = (gl.RenderContextProvider.Height - colorBarHeight) / 2; // ��ɫ����ֱ����

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

            // ������ɫ��
            gl.Begin(OpenGL.GL_QUADS);
           
            for (int i = 0; i < colorBarHeight; i++)
            {
                // �����һ��ֵ�͵�ǰ�߶�
                float normalizedValue = (float)i / colorBarHeight;
                double currentHeight = zMin + normalizedValue * (zMax - zMin);
                // ��ȡ��ɫ
                var (r, g, b) = CxExtension.GetColorByHeight(currentHeight, zMin, zMax);

                gl.Color(r, g, b);
                gl.Vertex(startX, startY + i, 0);                // ���½�
                gl.Vertex(startX + colorBarWidth, startY + i, 0);  // ���½�
                gl.Vertex(startX + colorBarWidth, startY + i + 1, 0); // ���Ͻ�
                gl.Vertex(startX, startY + i + 1, 0);           // ���Ͻ�
            }

            gl.End();

            // ���ƿ̶Ⱥ�����
            int numDivisions = 7; // 7 �ȷ�
            gl.Color(1.0f, 1.0f, 1.0f); // �̶Ⱥ������ð�ɫ
            gl.LineWidth(1.0f); // �����߿�
            for (int i = 0; i <= numDivisions; i++)
            {
                // ����̶�λ�úͶ�Ӧ�߶�
                int tickY = startY + (int)(i * (colorBarHeight / (float)numDivisions));
                double heightValue = zMin + i * (zMax - zMin) / numDivisions;
                gl.Begin(OpenGL.GL_LINES);
                // ���ƿ̶���
                gl.Vertex(startX - 5, tickY, 0); // �̶������
                gl.Vertex(startX, tickY, 0);     // �̶����Ҷ�
                gl.End();
                // ���Ƹ߶�����
                gl.DrawText(startX - 45, tickY - 5, 1, 1, 1, "", 10, $"{heightValue:F2}");
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
