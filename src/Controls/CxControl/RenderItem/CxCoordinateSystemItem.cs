using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Primitives;
using SharpGL.SceneGraph.Quadrics;
using System;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// 坐标系管理类
    /// </summary>
    public class CxCoordinateSystemItem : AbstractRenderItem
    {
        private readonly float axisLength;
        private readonly float axisRadius;
        private readonly float coneHeight;
        private readonly float coneRadius;

        private readonly CxCoordination3D coordination;
        public CxCoordinateSystemItem(
            float axisLength = 5.0f, float axisRadius = 0.1f, float coneHeight = 0.5f, float coneRadius = 0.2f,
            CxCoordination3D? coordination = null)
        {
            this.axisLength = axisLength;
            this.axisRadius = axisRadius;
            this.coneHeight = coneHeight;
            this.coneRadius = coneRadius;

            // 默认世界坐标系
            this.coordination = coordination ?? new CxCoordination3D(
                new CxPoint3D(0, 0, 0),
                new CxVector3D(1, 0, 0),
                new CxVector3D(0, 1, 0),
                new CxVector3D(0, 0, 1));
        }

        public override void Draw(OpenGL gl)
        {
            /*
            // X轴
            gl.PushMatrix();
            gl.Rotate(0.0f, 90.0f, 0.0f);
            DrawAxis(gl, axisLength, axisRadius, coneHeight, coneRadius, 1.0f, 0.0f, 0.0f);
            DrawLabel(gl, "X", 0, 0, axisLength, 1.0f, 0.0f, 0.0f);
            gl.PopMatrix();

            // Y轴
            gl.PushMatrix();
            gl.Rotate(-90.0f, 0.0f, 0.0f);
            DrawAxis(gl, axisLength, axisRadius, coneHeight, coneRadius, 0.0f, 1.0f, 0.0f);
            DrawLabel(gl, "Y", 0, 0, axisLength, 0.0f, 1.0f, 0.0f);
            gl.PopMatrix();

            // Z轴
            DrawAxis(gl, axisLength, axisRadius, coneHeight, coneRadius, 0.0f, 0.0f, 1.0f);
            DrawLabel(gl, "Z", 0, 0, axisLength, 0.0f, 0.0f, 1.0f);
            */
            // X轴
            DrawAxisWithLabel(gl, coordination.Origin, coordination.XAxis, "X", 1.0f, 0.0f, 0.0f);
            // Y轴
            DrawAxisWithLabel(gl, coordination.Origin, coordination.YAxis, "Y", 0.0f, 1.0f, 0.0f);
            // Z轴
            DrawAxisWithLabel(gl, coordination.Origin, coordination.ZAxis, "Z", 0.0f, 0.0f, 1.0f);

        }
        /// <summary>
        /// 绘制固定在屏幕左下角但方向随视图旋转的坐标轴
        /// </summary>
        public void DrawScreenPositionedAxes(OpenGL gl)
        {
            //关闭深度测试
            gl.Disable(OpenGL.GL_DEPTH_TEST);
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
            // 移除缩放部分：归一化旋转矩阵的列向量
            NormalizeColumn(modelViewMatrix, 0); // 归一化第 0 列
            NormalizeColumn(modelViewMatrix, 1); // 归一化第 1 列
            NormalizeColumn(modelViewMatrix, 2); // 归一化第 2 列

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
            // 恢复深度测试
            gl.Enable(OpenGL.GL_DEPTH_TEST);

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
        // 归一化矩阵的列向量
        private void NormalizeColumn(float[] matrix, int columnIndex)
        {
            float x = matrix[columnIndex];
            float y = matrix[4 + columnIndex];
            float z = matrix[8 + columnIndex];

            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            if (length > 0.0f)
            {
                matrix[columnIndex] /= length;
                matrix[4 + columnIndex] /= length;
                matrix[8 + columnIndex] /= length;
            }
        }
        private void DrawLabel(OpenGL gl, string label, float x, float y, float z, float r, float g, float b)
        {
            // 获取当前模型视图和投影矩阵
            double[] modelview = new double[16];
            double[] projection = new double[16];
            int[] viewport = new int[4];
            gl.GetDouble(OpenGL.GL_MODELVIEW_MATRIX, modelview);
            gl.GetDouble(OpenGL.GL_PROJECTION_MATRIX, projection);
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);

            // 3D -> 2D 投影
            var objCoord = new Vertex(x, y, z);
            var screenCoord = gl.Project(objCoord);
            // 判断是否在屏幕范围内
            if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
            {
                return;
            }
            //// 如果需要考虑透视范围，可以检查 screenCoord.Z（假设其为归一化深度：0～1）
            if (screenCoord.Z < 0 || screenCoord.Z > 1)
            {
                return;
            }

            // 绘制2D文本
            gl.DrawText((int)screenCoord.X, (int)screenCoord.Y, r, g, b, "Arial", 20, label);
        }
        private void DrawAxisWithLabel(OpenGL gl, CxPoint3D origin, CxVector3D direction, string label, float r, float g, float b)
        {
            // 归一化方向向量
            var dir = direction.Normalize();

            gl.PushMatrix();
            gl.Translate(origin.X, origin.Y, origin.Z);

            var defaultDir = new CxVector3D(0, 0, 1); // OpenGL默认z轴
            float dot = defaultDir.Dot(dir);
            if (Math.Abs(dot - 1.0f) < 1e-3)
            {
                // 同向，无需旋转
            }
            else if (Math.Abs(dot + 1.0f) < 1e-3)
            {
                // 反向，绕X轴或Y轴旋转180度都可以
                gl.Rotate(180, 1, 0, 0);
            }
            else
            {
                var axis = defaultDir.Cross(dir);
                float angle = (float)(Math.Acos(dot) * 180.0 / Math.PI);
                gl.Rotate(angle, axis.X, axis.Y, axis.Z);
            }

            // 绘制圆柱和圆锥
            DrawAxis(gl, axisLength, axisRadius, coneHeight, coneRadius, r, g, b);

            // 绘制标签
            DrawLabel(gl, label, 0, 0, axisLength, r, g, b);

            gl.PopMatrix();
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
