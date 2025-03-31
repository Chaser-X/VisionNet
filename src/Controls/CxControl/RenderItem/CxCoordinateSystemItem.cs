using SharpGL;
using SharpGL.SceneGraph.Primitives;
using SharpGL.SceneGraph.Quadrics;
using System;

namespace VisionNet.Controls
{
    /// <summary>
    /// 坐标系管理类
    /// </summary>
    public class CxCoordinateSystemItem : RenderAbstractItem
    {
        private readonly float axisLength; 
        private readonly float axisRadius;
        private readonly float coneHeight;
        private readonly float coneRadius;
        public CxCoordinateSystemItem(float axisLength = 5.0f, float axisRadius = 0.1f, float coneHeight = 0.5f, float coneRadius = 0.2f)
        {
            this.axisLength = axisLength;
            this.axisRadius = axisRadius;
            this.coneHeight = coneHeight;
            this.coneRadius = coneRadius;
        }

        public override void Draw(OpenGL gl)
        {
            // 绘制X轴
            gl.PushMatrix();
            gl.Rotate(0.0f, 90.0f, 0.0f);
            DrawAxis(gl, axisLength, axisRadius, coneHeight, coneRadius, 1.0f, 0.0f, 0.0f);
            gl.PopMatrix();

            // 绘制Y轴
            gl.PushMatrix();
            gl.Rotate(-90.0f, 0.0f, 0.0f);
            DrawAxis(gl, axisLength, axisRadius, coneHeight, coneRadius, 0.0f, 1.0f, 0.0f);
            gl.PopMatrix();

            // 绘制Z轴
            DrawAxis(gl, axisLength, axisRadius, coneHeight, coneRadius, 0.0f, 0.0f, 1.0f);
        }
        /// <summary>
        /// 绘制固定在屏幕左下角但方向随视图旋转的坐标轴
        /// </summary>
        public void DrawScreenPositionedAxes(OpenGL gl)
        {
            // 保存当前矩阵状态
            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);

            //// 保存当前模型视图矩阵
            //gl.MatrixMode(OpenGL.GL_MODELVIEW);
            //gl.PushMatrix();

            //// 保存当前投影矩阵
            //gl.MatrixMode(OpenGL.GL_PROJECTION);
            //gl.PushMatrix();

            // 获取当前模型视图矩阵，我们只需要旋转部分
            float[] modelViewMatrix = new float[16];
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, modelViewMatrix);

            // 创建只包含旋转的矩阵（移除平移部分）
            modelViewMatrix[12] = 0.0f;
            modelViewMatrix[13] = 0.0f;
            modelViewMatrix[14] = 0.0f;

            // 切换到正交投影
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height, -100, 100);

            // 设置模型视图矩阵
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            // 将坐标轴放在屏幕左下角
            float margin = 50.0f; // 边距
            gl.Translate(margin, margin, 0.0f);

            // 设置坐标轴大小
            float axisScale = 10.0f;
            gl.Scale(axisScale, axisScale, axisScale);

            // 应用旋转（但不应用平移）
            gl.MultMatrix(modelViewMatrix);

            Draw(gl);

            // 恢复投影矩阵
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();

            // 恢复模型视图矩阵
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PopMatrix();

            // 恢复属性
            gl.PopAttrib();

        }
        private void DrawAxis(OpenGL gl, float length, float radius, float coneHeight, float coneRadius, float r, float g, float b)
        {
            gl.Color(r, g, b);
          
            // 绘制圆柱
            Cylinder cylinder = new Cylinder();
            cylinder.BaseRadius = radius;
            cylinder.TopRadius = radius;
            cylinder.Height = length - coneHeight;
            cylinder.CreateInContext(gl);
            cylinder.Render(gl, SharpGL.SceneGraph.Core.RenderMode.Render);

            // 移动到圆锥位置
            gl.Translate(0.0f, 0.0f, length - coneHeight);

            // 绘制圆锥
            DrawCone(gl, coneHeight, coneRadius);

            // 复位
            gl.Translate(0.0f, 0.0f, -(length - coneHeight));
        }
        private void DrawCone(OpenGL gl, float height, float radius)
        {
            int slices = 20; // 圆锥的切片数
            // 绘制圆锥的侧面
            gl.Begin(OpenGL.GL_TRIANGLE_FAN);
            gl.Vertex(0.0f, 0.0f, height); // 圆锥顶点
            for (int i = 0; i <= slices; i++)
            {
                double angle = 2 * Math.PI * i / slices;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);
                gl.Vertex(x, y, 0.0f);
            }
            gl.End();

            // 绘制圆锥的底面
            gl.Begin(OpenGL.GL_TRIANGLE_FAN);
            gl.Vertex(0.0f, 0.0f, 0.0f); // 圆心
            for (int i = 0; i <= slices; i++)
            {
                double angle = 2 * Math.PI * i / slices;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);
                gl.Vertex(x, y, 0.0f);
            }
            gl.End();
        }
    }
}
