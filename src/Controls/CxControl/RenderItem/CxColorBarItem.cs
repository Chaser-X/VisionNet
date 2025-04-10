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

            int colorBarWidth = 20; // 颜色条的宽度
            int colorBarHeight = gl.RenderContextProvider.Height / 2; // 颜色条的高度
            int startX = gl.RenderContextProvider.Width - colorBarWidth - 10; // 颜色条的起始位置（右侧，留出一些边距）
            int startY = (gl.RenderContextProvider.Height - colorBarHeight) / 2; // 颜色条竖直居中

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

            // 绘制颜色条
            gl.Begin(OpenGL.GL_QUADS);
           
            for (int i = 0; i < colorBarHeight; i++)
            {
                // 计算归一化值和当前高度
                float normalizedValue = (float)i / colorBarHeight;
                double currentHeight = zMin + normalizedValue * (zMax - zMin);
                // 获取颜色
                var (r, g, b) = CxExtension.GetColorByHeight(currentHeight, zMin, zMax);

                gl.Color(r, g, b);
                gl.Vertex(startX, startY + i, 0);                // 左下角
                gl.Vertex(startX + colorBarWidth, startY + i, 0);  // 右下角
                gl.Vertex(startX + colorBarWidth, startY + i + 1, 0); // 右上角
                gl.Vertex(startX, startY + i + 1, 0);           // 左上角
            }

            gl.End();

            // 绘制刻度和文字
            int numDivisions = 7; // 7 等分
            gl.Color(1.0f, 1.0f, 1.0f); // 刻度和文字用白色
            gl.LineWidth(1.0f); // 设置线宽
            for (int i = 0; i <= numDivisions; i++)
            {
                // 计算刻度位置和对应高度
                int tickY = startY + (int)(i * (colorBarHeight / (float)numDivisions));
                double heightValue = zMin + i * (zMax - zMin) / numDivisions;
                gl.Begin(OpenGL.GL_LINES);
                // 绘制刻度线
                gl.Vertex(startX - 5, tickY, 0); // 刻度线左端
                gl.Vertex(startX, tickY, 0);     // 刻度线右端
                gl.End();
                // 绘制高度文字
                gl.DrawText(startX - 45, tickY - 5, 1, 1, 1, "", 10, $"{heightValue:F2}");
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
