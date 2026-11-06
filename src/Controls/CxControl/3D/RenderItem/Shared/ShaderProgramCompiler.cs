using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;

namespace VisionNet.Controls
{
    /// <summary>Compiles and links GLSL programs for the modern 3D renderers.</summary>
    internal static class ShaderProgramCompiler
    {
        internal static uint CompileProgram(CxGlContext gl, string vertexSource, string fragmentSource)
        {
            if (string.IsNullOrEmpty(vertexSource) || string.IsNullOrEmpty(fragmentSource))
                return 0;

            int vertex = CompileShader(ShaderType.VertexShader, vertexSource);
            int fragment = CompileShader(ShaderType.FragmentShader, fragmentSource);
            if (vertex == 0 || fragment == 0)
            {
                if (vertex != 0) GL.DeleteShader(vertex);
                if (fragment != 0) GL.DeleteShader(fragment);
                return 0;
            }

            int program = GL.CreateProgram();
            GL.AttachShader(program, vertex);
            GL.AttachShader(program, fragment);
            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
            GL.DeleteShader(vertex);
            GL.DeleteShader(fragment);
            if (status == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                Debug.WriteLine("Shader link error: " + log);
                GL.DeleteProgram(program);
                return 0;
            }

            return (uint)program;
        }

        private static int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
            if (status != 0) return shader;

            string log = GL.GetShaderInfoLog(shader);
            Debug.WriteLine("Shader compile error: " + log);
            GL.DeleteShader(shader);
            return 0;
        }
    }
}
