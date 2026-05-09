using SharpGL;
using SharpGL.SceneGraph;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxCoordinationTagItem : AbstractRenderItem
    {
        public CxPoint3D Point { get; set; } = new CxPoint3D();
        public byte? Intensity { get; set; } = null; // 点的强度值
        public bool Visible { get; set; } = false; // 控制标签是否可见

        public Color TextColor { get; set; } = Color.White;

        public void SetCoordinates(CxPoint3D point, byte? intesity = null)
        {
            Point = point;
            Intensity = intesity;
        }

        public override void Draw(OpenGL gl)
        {
            if (!Visible) return; // 如果标签不可见，则不绘制

            // 将3D坐标转换为屏幕坐标（包括深度信息）
            var objCoord = new Vertex(Point.X, Point.Y, Point.Z);
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

            int rectWidth = 80; // 矩形宽度
            int rectHeight = Intensity.HasValue ? 90 : 80; // 矩形高度
            int startX = (int)screenCoord.X; // 矩形左上角X坐标
            int startY = (int)screenCoord.Y - 10; // 矩形左上角Y坐标

            //关闭深度测试
            gl.Disable(OpenGL.GL_DEPTH_TEST);

            // 保存当前矩阵模式和矩阵
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();

            // 设置正交投影
            int width = gl.RenderContextProvider.Width;
            int height = gl.RenderContextProvider.Height;
            gl.Ortho(0, width, 0, height, -1, 1);

            // 切换到模型视图矩阵
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            //绘制白色边框的半透明黑色矩形看板
            // 设置半透明黑色填充
            gl.Color(0.0f, 0.0f, 0.0f, 0.5f); // RGBA，黑色，50%透明
            // 绘制填充矩形
            gl.Begin(OpenGL.GL_QUADS);
            gl.Vertex(startX, startY); // 左上角
            gl.Vertex(startX + rectWidth, startY); // 右上角
            gl.Vertex(startX + rectWidth, startY - rectHeight); // 右下角
            gl.Vertex(startX, startY - rectHeight); // 左下角
            gl.End();
            // 设置白色边框
            gl.LineWidth(1.0f); // 设置线宽
            gl.Color(1.0f, 1.0f, 1.0f, 1.0f); // RGBA，白色，不透明
            // 绘制矩形边框
            gl.Begin(OpenGL.GL_LINE_LOOP);
            gl.Vertex(startX - 1, startY + 1); // 左上角
            gl.Vertex(startX + rectWidth + 1, startY + 1); // 右上角
            gl.Vertex(startX + rectWidth + 1, startY - rectHeight - 1); // 右下角
            gl.Vertex(startX - 1, startY - rectHeight - 1); // 左下角
            gl.End();

            int textOffsetX = 10; // 文本的X偏移
            int textOffsetY = 20; // 文本的Y偏移
            var (R, G, B) = (TextColor.R / 255.0f, TextColor.G / 255.0f, TextColor.B / 255.0f);
            gl.DrawText(startX + textOffsetX, startY - textOffsetY, R, G, B, "Helvetica", 12, $"X: {Point.X:F3}");
            gl.DrawText(startX + textOffsetX, startY - textOffsetY * 2, R, B, B, "Helvetica", 12, $"Y: {Point.Y:F3}");
            gl.DrawText(startX + textOffsetX, startY - textOffsetY * 3, R, G, B, "Helvetica", 12, $"Z: {Point.Z:F3}");
            if (Intensity.HasValue)
                gl.DrawText(startX + textOffsetX, startY - textOffsetY * 4, R, G, B, "Helvetica", 12, $"I: {Intensity.Value}");

            // 恢复模型视图矩阵
            gl.PopMatrix();

            // 恢复投影矩阵
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();

            // 切换回默认矩阵模式
            gl.MatrixMode(OpenGL.GL_MODELVIEW);

            // 恢复深度测试
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 释放托管资源
            }
            // 释放非托管资源
            base.Dispose(disposing);
        }
    }
}
