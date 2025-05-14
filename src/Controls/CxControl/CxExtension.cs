using SharpGL.SceneGraph;
using SharpGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionNet.Controls
{
    public class CxExtension
    {
        /// <summary>
        /// 根据给定范围的Zmin,zmax,对每个点Z返回和Z相关的颜色
        /// </summary>
        public static (float r, float g, float b) GetColorByHeight(double z, double zMin, double zMax)
        {
            float range = (float)(zMax - zMin);
            if (z > zMax) z = zMax;
            if (z < zMin) z = zMin;
            float normalizedZ = (float)(z - zMin) / range;

            float r, g, b;
            if (normalizedZ < 1.0f / 7.0f)
            {
                // 深蓝
                r = 0.0f;
                g = 0.0f;
                b = 0.5f + normalizedZ * 3.5f;
            }
            else if (normalizedZ < 2.0f / 7.0f)
            {
                // 天空蓝
                r = 0.0f;
                g = (normalizedZ - 1.0f / 7.0f) * 7.0f;
                b = 1.0f;
            }
            else if (normalizedZ < 3.0f / 7.0f)
            {
                // 绿
                r = 0.0f;
                g = 1.0f;
                b = 1.0f - (normalizedZ - 2.0f / 7.0f) * 7.0f;
            }
            else if (normalizedZ < 4.0f / 7.0f)
            {
                // 黄
                r = (normalizedZ - 3.0f / 7.0f) * 7.0f;
                g = 1.0f;
                b = 0.0f;
            }
            else if (normalizedZ < 5.0f / 7.0f)
            {
                // 红
                r = 1.0f;
                g = 1.0f - (normalizedZ - 4.0f / 7.0f) * 7.0f;
                b = 0.0f;
            }
            else if (normalizedZ < 6.0f / 7.0f)
            {
                // 粉
                r = 1.0f;
                g = 0.0f;
                b = (normalizedZ - 5.0f / 7.0f) * 7.0f;
            }
            else
            {
                // 白
                r = 1.0f;
                g = (normalizedZ - 6.0f / 7.0f) * 7.0f;
                b = 1.0f;
            }
            return (r, g, b);
        }

        public static void DrawTextLabel3D(OpenGL gl, float x, float y, float z, float size, string text)
        {
            // 将3D坐标转换为屏幕坐标（包括深度信息）
            var objCoord = new Vertex(x, y, z);
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

            float newScale = size * (1 + screenCoord.Z);//1 / (size / (screenCoord.Z + epsilon))* 100;

            // 保存当前矩阵
            gl.PushMatrix();

            // 移动到文字位置
            gl.Translate(x, y, z);

            // 使文字始终面向相机
            //gl.Rotate(-rotateY, 0.0f, 1.0f, 0.0f);
            //gl.Rotate(-rotateX, 1.0f, 0.0f, 0.0f);

            // 应用缩放，使文字在屏幕上保持固定像素大小
            gl.Scale(newScale, newScale, newScale);

            // 绘制文字
            gl.Color(1.0f, 1.0f, 1.0f);
            foreach (char c in text)
            {
                gl.DrawText3D("Arial", 0.1f, 0.0f, c.ToString());
            }

            gl.PopMatrix();
        }
        public static void DrawTextLabel2D(OpenGL gl, float x, float y, float z, float size, string text)
        {
            // 将3D坐标转换为屏幕坐标（包括深度信息）
            var objCoord = new Vertex(x, y, z);
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
            gl.DrawText((int)screenCoord.X, (int)screenCoord.Y, 1, 1, 1, "Arial", size, text);
        }

        public static bool IsOpenGLAvailable()
        {
            try
            {
                // 尝试创建一个临时的OpenGL上下文
                OpenGL gl = new OpenGL();

                // 获取OpenGL版本信息
                string version = gl.GetString(OpenGL.GL_VERSION);
                string renderer = gl.GetString(OpenGL.GL_RENDERER);
                string vendor = gl.GetString(OpenGL.GL_VENDOR);

                // 记录日志
                Console.WriteLine($"OpenGL版本: {version}");
                Console.WriteLine($"渲染器: {renderer}");
                Console.WriteLine($"供应商: {vendor}");

                // 检查是否为软件渲染器
                bool isSoftwareRenderer = renderer.Contains("Software") ||
                                         renderer.Contains("Microsoft") ||
                                         renderer.Contains("GDI Generic");

                // 如果是软件渲染，可以选择警告或拒绝
                if (isSoftwareRenderer)
                {
                    Console.WriteLine("警告：检测到软件渲染器，性能可能受限");
                }

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenGL初始化失败: {ex.Message}");
                return false;
            }
        }

        public static string GetOpenGLVersion()
        {
            try
            {
                // 尝试创建一个临时的OpenGL上下文
                OpenGL gl = new OpenGL();
                // 获取OpenGL版本信息
                string version = gl.GetString(OpenGL.GL_VERSION);
                string renderer = gl.GetString(OpenGL.GL_RENDERER);
                string vendor = gl.GetString(OpenGL.GL_VENDOR);

                // 记录日志
                var message = $"OpenGL版本: {version}\r\n渲染器: {renderer}\r\n供应商: {vendor}";
                return message;

            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
