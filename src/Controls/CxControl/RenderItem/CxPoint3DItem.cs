using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Quadrics;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Drawing;
using System.Text;
using VisionNet.DataType;
using GLSphere = SharpGL.SceneGraph.Quadrics;

namespace VisionNet.Controls
{
    public enum PointShape
    {
        Point,
        Sphere,
    }
    public class CxPoint3DItem : AbstractRenderItem
    {
        public CxPoint3D[] Point3Ds { get; private set; }
        public PointShape Shape { get; set; } = PointShape.Point;
        public CxPoint3DItem(CxPoint3D[] points, Color color, float size = 1.0f, PointShape shape = PointShape.Point) : base(color, size)
        {
            this.Point3Ds = points;
            this.Shape = shape;
        }
        public override void Draw(OpenGL gl)
        {
            if (Point3Ds == null || Point3Ds.Length == 0) return;

            if (Shape == PointShape.Point)
            {
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0); // 设置颜色
                gl.PointSize(Size);
                gl.Begin(OpenGL.GL_POINTS);
                foreach (var point in Point3Ds)
                {
                    gl.Vertex(point.X, point.Y, point.Z);
                }
                gl.End();
            }
            else
            {
                uint[] buffers = new uint[3];
                int indicesCount = 0;
                var vao = GenSphereVAO(gl,ref buffers, ref indicesCount);
                // 创建着色器程序
                uint shaderProgram = CreateShaderProgram(gl);
                // 使用着色器程序
                gl.UseProgram(shaderProgram);
                // 设置投影和视图矩阵
                float[] projectionMatrix = new float[16]; // 假设您已经计算了投影矩阵
                gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, projectionMatrix); // 获取当前的投影矩阵
                float[] viewMatrix = new float[16];       // 假设您已经计算了视图矩阵
                gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, viewMatrix); // 获取当前的模型视图矩阵
                // 获取uniform位置
                int viewLoc = gl.GetUniformLocation(shaderProgram, "view");
                int projectionLoc = gl.GetUniformLocation(shaderProgram, "projection");
                int colorLoc = gl.GetUniformLocation(shaderProgram, "aColor");
                // 设置uniform值
                gl.UniformMatrix4(viewLoc, 1, false, viewMatrix);
                gl.UniformMatrix4(projectionLoc, 1, false, projectionMatrix);
                gl.Uniform4(colorLoc, Color.R / 255.0f, Color.G / 255.0f, Color.B / 255.0f, 1f);

                // 4. 使用 gl.DrawElementsInstanced 绘制
                gl.BindVertexArray(vao[0]);
                gl.DrawElementsInstanced(OpenGL.GL_TRIANGLES, indicesCount, OpenGL.GL_UNSIGNED_SHORT, IntPtr.Zero, Point3Ds.Length);
                gl.BindVertexArray(0);
                // 解绑着色器程序
                gl.UseProgram(0);

                // 删除着色器程序
                gl.DeleteProgram(shaderProgram);
                // 清理缓冲区
                gl.DeleteBuffers(3, buffers);
                gl.DeleteVertexArrays(1, vao);
            }
        }
        // 生成球体的顶点和索引数据
        private void GenerateSphereData(float radius, int slices, int stacks, List<float> vertices, List<ushort> indices)
        {
            for (int stack = 0; stack <= stacks; stack++)
            {
                float phi = (float)(Math.PI / 2 - stack * Math.PI / stacks); // 纬度角
                float y = radius * (float)Math.Sin(phi);
                float scale = radius * (float)Math.Cos(phi);

                for (int slice = 0; slice <= slices; slice++)
                {
                    float theta = (float)(slice * 2 * Math.PI / slices); // 经度角
                    float x = scale * (float)Math.Cos(theta);
                    float z = scale * (float)Math.Sin(theta);

                    vertices.Add(x);
                    vertices.Add(y);
                    vertices.Add(z);
                }
            }

            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    ushort first = (ushort)(stack * (slices + 1) + slice);
                    ushort second = (ushort)(first + (ushort)(slices + 1));

                    indices.Add(first);
                    indices.Add(second);
                    indices.Add((ushort)(first + 1));

                    indices.Add(second);
                    indices.Add((ushort)(second + 1));
                    indices.Add((ushort)(first + 1));
                }
            }
        }
        // 创建和编译着色器
        private uint CreateShaderProgram(OpenGL gl)
        {
            // 顶点着色器源码 - 使用普通字符串连接而非逐字字符串
            string vertexShaderSource =
                "#version 330 core\n" +
                "layout (location = 0) in vec3 aPos;\n" +
                "layout (location = 1) in vec3 aInstancePos;\n" +
                "\n" +
                "uniform mat4 view;\n" +
                "uniform mat4 projection;\n" +
                "uniform vec4 aColor;\n" +
                "out vec4 vertexColor;\n" +
                "\n" +
                "void main()\n" +
                "{\n" +
                "    mat4 model = mat4(1.0);\n" +
                "    model[3] = vec4(aInstancePos, 1.0);\n" +
                "    gl_Position = projection * view * model * vec4(aPos, 1.0);\n" +
                "    vertexColor = aColor;\n" +
                "}\n";

            // 片段着色器源码
            string fragmentShaderSource =
                "#version 330 core\n" +
                "in vec4 vertexColor;\n" +
                "out vec4 FragColor;\n" +
                "\n" +
                "void main()\n" +
                "{\n" +
                "    FragColor = vertexColor;\n" +
                "}\n";

            // 创建顶点着色器
            uint vertexShader = gl.CreateShader(OpenGL.GL_VERTEX_SHADER);
            gl.ShaderSource(vertexShader, vertexShaderSource);
            gl.CompileShader(vertexShader);

            // 检查编译错误
            int[] success = new int[1];
            gl.GetShader(vertexShader, OpenGL.GL_COMPILE_STATUS, success);
            if (success[0] == 0)
            {
                StringBuilder infoLog = new StringBuilder(512);
                gl.GetShaderInfoLog(vertexShader, 512, IntPtr.Zero, infoLog);
                Console.WriteLine("ERROR::SHADER::VERTEX::COMPILATION_FAILED\n" + infoLog.ToString());
                return 0; // 返回0表示失败
            }

            // 创建片段着色器
            uint fragmentShader = gl.CreateShader(OpenGL.GL_FRAGMENT_SHADER);
            gl.ShaderSource(fragmentShader, fragmentShaderSource);
            gl.CompileShader(fragmentShader);

            // 检查编译错误
            gl.GetShader(fragmentShader, OpenGL.GL_COMPILE_STATUS, success);
            if (success[0] == 0)
            {
                StringBuilder infoLog = new StringBuilder(512);
                gl.GetShaderInfoLog(fragmentShader, 512, IntPtr.Zero, infoLog);
                Console.WriteLine("ERROR::SHADER::FRAGMENT::COMPILATION_FAILED\n" + infoLog.ToString());
                return 0; // 返回0表示失败
            }

            // 创建着色器程序
            uint shaderProgram = gl.CreateProgram();
            gl.AttachShader(shaderProgram, vertexShader);
            gl.AttachShader(shaderProgram, fragmentShader);
            gl.LinkProgram(shaderProgram);

            // 检查链接错误
            gl.GetProgram(shaderProgram, OpenGL.GL_LINK_STATUS, success);
            if (success[0] == 0)
            {
                StringBuilder infoLog = new StringBuilder(512);
                gl.GetProgramInfoLog(shaderProgram, 512, IntPtr.Zero, infoLog);
                Console.WriteLine("ERROR::SHADER::PROGRAM::LINKING_FAILED\n" + infoLog.ToString());
                return 0; // 返回0表示失败
            }

            // 删除着色器，它们已经链接到程序中，不再需要
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);

            return shaderProgram;
        }
        private uint[] GenSphereVAO(OpenGL gl,ref uint[] buffers, ref int indicesCount)
        {
            var vao = new uint[1];
            // 创建VAO
            gl.GenVertexArrays(1, vao);
            gl.BindVertexArray(vao[0]);

            // 1. 准备球体的顶点和索引数据
            List<float> vertices = new List<float>();
            List<ushort> indices = new List<ushort>();
            GenerateSphereData(Size / 2, 10, 10, vertices, indices);
            // 创建 VBO 和 EBO
            gl.GenBuffers(3, buffers);
            uint vbo = buffers[0];
            uint ebo = buffers[1];
            uint instanceVBO = buffers[2];
            // 绑定 VBO
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices.ToArray(), OpenGL.GL_STATIC_DRAW);
            // 设置顶点属性
            gl.EnableVertexAttribArray(0); // 位置属性
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);

            // 绑定 EBO
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, ebo);
            gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indices.ToArray(), OpenGL.GL_STATIC_DRAW);

            // 3. 创建实例化缓冲区
            // 将 Point3Ds 转换为 float[]
            float[] instanceData = new float[Point3Ds.Length * 3];
            for (int i = 0; i < Point3Ds.Length; i++)
            {
                instanceData[i * 3] = Point3Ds[i].X;
                instanceData[i * 3 + 1] = Point3Ds[i].Y;
                instanceData[i * 3 + 2] = Point3Ds[i].Z;
            }
            // 创建实例化缓冲区
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, instanceVBO);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, instanceData, OpenGL.GL_STATIC_DRAW);
            // 设置实例化属性
            gl.EnableVertexAttribArray(1); // 实例位置属性
            gl.VertexAttribPointer(1, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
            gl.VertexAttribDivisor(1, 1); // 每个实例使用一个位置
            // 解绑 VAO
            gl.BindVertexArray(0);
          
            indicesCount = indices.Count;
            return vao;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 释放资源
                Point3Ds = null;
            }
            base.Dispose(disposing);
        }
    }
}

