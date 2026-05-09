using SharpGL;
using SharpGL.SceneGraph.Quadrics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Specifies the visual shape used when rendering a set of 3D points.</summary>
    public enum PointShape
    {
        /// <summary>Render each point as an OpenGL point primitive (single pixel or point-size square).</summary>
        Point,

        /// <summary>Render each point as an instanced sphere mesh.</summary>
        Sphere,
    }

    /// <summary>
    /// Renders an array of <see cref="CxPoint3D"/> values as either flat GL_POINTS or
    /// instanced sphere meshes, depending on <see cref="Shape"/>.
    /// </summary>
    public class CxPoint3DItem : AbstractRenderItem
    {
        /// <summary>Gets the point positions to be rendered.</summary>
        public CxPoint3D[] Point3Ds { get; private set; }

        /// <summary>Gets or sets the visual shape (point or sphere).</summary>
        public PointShape Shape { get; set; } = PointShape.Point;

        /// <summary>Initializes the item with the given points, colour, size, and shape.</summary>
        /// <param name="points">World-space point positions.</param>
        /// <param name="color">Drawing colour.</param>
        /// <param name="size">Point size in pixels, or sphere radius when <paramref name="shape"/> is <see cref="PointShape.Sphere"/>.</param>
        /// <param name="shape">Visual shape to use for each point.</param>
        public CxPoint3DItem(CxPoint3D[] points, Color color, float size = 1.0f,
            PointShape shape = PointShape.Point) : base(color, size)
        {
            Point3Ds = points;
            Shape    = shape;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Point3Ds == null || Point3Ds.Length == 0) return;

            if (Shape == PointShape.Point)
            {
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0);
                gl.PointSize(Size);
                gl.Begin(OpenGL.GL_POINTS);
                foreach (var p in Point3Ds)
                    gl.Vertex(p.X, p.Y, p.Z);
                gl.End();
            }
            else
            {
                uint[] buffers     = new uint[3];
                int    indexCount  = 0;
                uint[] vao         = GenerateSphereVao(gl, ref buffers, ref indexCount);

                uint shader = CreateShaderProgram(gl);
                gl.UseProgram(shader);

                float[] proj = new float[16];
                float[] view = new float[16];
                gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, proj);
                gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX,  view);

                gl.UniformMatrix4(gl.GetUniformLocation(shader, "view"),       1, false, view);
                gl.UniformMatrix4(gl.GetUniformLocation(shader, "projection"), 1, false, proj);
                gl.Uniform4(gl.GetUniformLocation(shader, "aColor"),
                    Color.R / 255.0f, Color.G / 255.0f, Color.B / 255.0f, 1f);

                gl.BindVertexArray(vao[0]);
                gl.DrawElementsInstanced(OpenGL.GL_TRIANGLES, indexCount,
                    OpenGL.GL_UNSIGNED_SHORT, IntPtr.Zero, Point3Ds.Length);
                gl.BindVertexArray(0);

                gl.UseProgram(0);
                gl.DeleteProgram(shader);
                gl.DeleteBuffers(3, buffers);
                gl.DeleteVertexArrays(1, vao);
            }
        }

        /// <summary>
        /// Generates sphere vertex and index data for the given tessellation parameters.
        /// </summary>
        /// <param name="radius">Sphere radius.</param>
        /// <param name="slices">Number of longitude slices.</param>
        /// <param name="stacks">Number of latitude stacks.</param>
        /// <param name="vertices">Output vertex position list (X, Y, Z triples).</param>
        /// <param name="indices">Output triangle index list.</param>
        private void GenerateSphereData(float radius, int slices, int stacks,
            List<float> vertices, List<ushort> indices)
        {
            for (int stack = 0; stack <= stacks; stack++)
            {
                float phi   = (float)(Math.PI / 2 - stack * Math.PI / stacks);
                float y     = radius * (float)Math.Sin(phi);
                float scale = radius * (float)Math.Cos(phi);

                for (int slice = 0; slice <= slices; slice++)
                {
                    float theta = (float)(slice * 2 * Math.PI / slices);
                    vertices.Add(scale * (float)Math.Cos(theta));
                    vertices.Add(y);
                    vertices.Add(scale * (float)Math.Sin(theta));
                }
            }

            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    ushort first  = (ushort)(stack * (slices + 1) + slice);
                    ushort second = (ushort)(first + (ushort)(slices + 1));

                    indices.Add(first);
                    indices.Add(second);
                    indices.Add((ushort)(first  + 1));
                    indices.Add(second);
                    indices.Add((ushort)(second + 1));
                    indices.Add((ushort)(first  + 1));
                }
            }
        }

        /// <summary>
        /// Compiles and links a GLSL shader program for instanced sphere rendering.
        /// Returns 0 on compilation or link failure.
        /// </summary>
        private uint CreateShaderProgram(OpenGL gl)
        {
            string vertSrc =
                "#version 330 core\n"
                + "layout (location = 0) in vec3 aPos;\n"
                + "layout (location = 1) in vec3 aInstancePos;\n"
                + "uniform mat4 view;\n"
                + "uniform mat4 projection;\n"
                + "uniform vec4 aColor;\n"
                + "out vec4 vertexColor;\n"
                + "void main()\n"
                + "{\n"
                + "    mat4 model = mat4(1.0);\n"
                + "    model[3] = vec4(aInstancePos, 1.0);\n"
                + "    gl_Position = projection * view * model * vec4(aPos, 1.0);\n"
                + "    vertexColor = aColor;\n"
                + "}\n";

            string fragSrc =
                "#version 330 core\n"
                + "in vec4 vertexColor;\n"
                + "out vec4 FragColor;\n"
                + "void main() { FragColor = vertexColor; }\n";

            uint vert = CompileShader(gl, OpenGL.GL_VERTEX_SHADER,   vertSrc);
            uint frag = CompileShader(gl, OpenGL.GL_FRAGMENT_SHADER, fragSrc);
            if (vert == 0 || frag == 0) return 0;

            uint prog = gl.CreateProgram();
            gl.AttachShader(prog, vert);
            gl.AttachShader(prog, frag);
            gl.LinkProgram(prog);

            int[] success = new int[1];
            gl.GetProgram(prog, OpenGL.GL_LINK_STATUS, success);
            if (success[0] == 0)
            {
                var log = new StringBuilder(512);
                gl.GetProgramInfoLog(prog, 512, IntPtr.Zero, log);
                Console.WriteLine("ERROR::SHADER::PROGRAM::LINKING_FAILED\n" + log);
                return 0;
            }

            gl.DeleteShader(vert);
            gl.DeleteShader(frag);
            return prog;
        }

        private uint CompileShader(OpenGL gl, uint type, string source)
        {
            uint shader = gl.CreateShader(type);
            gl.ShaderSource(shader, source);
            gl.CompileShader(shader);

            int[] success = new int[1];
            gl.GetShader(shader, OpenGL.GL_COMPILE_STATUS, success);
            if (success[0] == 0)
            {
                var log = new StringBuilder(512);
                gl.GetShaderInfoLog(shader, 512, IntPtr.Zero, log);
                string typeName = type == OpenGL.GL_VERTEX_SHADER ? "VERTEX" : "FRAGMENT";
                Console.WriteLine($"ERROR::SHADER::{typeName}::COMPILATION_FAILED\n{log}");
                return 0;
            }
            return shader;
        }

        /// <summary>
        /// Creates a VAO, VBO, EBO, and instance VBO for one sphere mesh shared across all instances.
        /// </summary>
        /// <param name="gl">Active OpenGL context.</param>
        /// <param name="buffers">Output array to receive the three buffer IDs.</param>
        /// <param name="indexCount">Receives the number of indices in the EBO.</param>
        /// <returns>A single-element VAO ID array.</returns>
        private uint[] GenerateSphereVao(OpenGL gl, ref uint[] buffers, ref int indexCount)
        {
            var vao = new uint[1];
            gl.GenVertexArrays(1, vao);
            gl.BindVertexArray(vao[0]);

            var vertices = new List<float>();
            var indices  = new List<ushort>();
            GenerateSphereData(Size / 2, 10, 10, vertices, indices);

            gl.GenBuffers(3, buffers);
            uint vbo         = buffers[0];
            uint ebo         = buffers[1];
            uint instanceVbo = buffers[2];

            // Vertex positions.
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices.ToArray(), OpenGL.GL_STATIC_DRAW);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);

            // Indices.
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, ebo);
            gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indices.ToArray(), OpenGL.GL_STATIC_DRAW);

            // Per-instance world positions.
            float[] instanceData = new float[Point3Ds.Length * 3];
            for (int i = 0; i < Point3Ds.Length; i++)
            {
                instanceData[i * 3]     = Point3Ds[i].X;
                instanceData[i * 3 + 1] = Point3Ds[i].Y;
                instanceData[i * 3 + 2] = Point3Ds[i].Z;
            }
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, instanceVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, instanceData, OpenGL.GL_STATIC_DRAW);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
            gl.VertexAttribDivisor(1, 1);   // Advance once per instance.

            gl.BindVertexArray(0);

            indexCount = indices.Count;
            return vao;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Point3Ds = null;
            base.Dispose(disposing);
        }
    }
}
