using System;
using System.Diagnostics;
using System.Text;
using SharpGL;

namespace VisionNet.Controls
{
    /// <summary>Compiles and links GLSL programs for the modern 3D renderers.</summary>
    internal static class ShaderProgramCompiler
    {
        internal static uint CompileProgram(OpenGL gl, string vertexSource, string fragmentSource)
        {
            if (gl == null || string.IsNullOrEmpty(vertexSource) || string.IsNullOrEmpty(fragmentSource))
                return 0;

            uint vertex = CompileShader(gl, OpenGL.GL_VERTEX_SHADER, vertexSource);
            uint fragment = CompileShader(gl, OpenGL.GL_FRAGMENT_SHADER, fragmentSource);
            if (vertex == 0 || fragment == 0)
            {
                if (vertex != 0) gl.DeleteShader(vertex);
                if (fragment != 0) gl.DeleteShader(fragment);
                return 0;
            }

            uint program = gl.CreateProgram();
            gl.AttachShader(program, vertex);
            gl.AttachShader(program, fragment);
            gl.LinkProgram(program);

            int[] status = new int[1];
            gl.GetProgram(program, OpenGL.GL_LINK_STATUS, status);
            gl.DeleteShader(vertex);
            gl.DeleteShader(fragment);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] length = new int[1];
                gl.GetProgram(program, OpenGL.GL_INFO_LOG_LENGTH, length);
                var log = new StringBuilder(Math.Max(1, length[0]));
                gl.GetProgramInfoLog(program, length[0], System.IntPtr.Zero, log);
                Debug.WriteLine("Shader link error: " + log);
                gl.DeleteProgram(program);
                return 0;
            }

            return program;
        }

        private static uint CompileShader(OpenGL gl, uint type, string source)
        {
            uint shader = gl.CreateShader(type);
            gl.ShaderSource(shader, source);
            gl.CompileShader(shader);

            int[] status = new int[1];
            gl.GetShader(shader, OpenGL.GL_COMPILE_STATUS, status);
            if (status[0] != OpenGL.GL_FALSE) return shader;

            int[] length = new int[1];
            gl.GetShader(shader, OpenGL.GL_INFO_LOG_LENGTH, length);
            var log = new StringBuilder(Math.Max(1, length[0]));
            gl.GetShaderInfoLog(shader, length[0], System.IntPtr.Zero, log);
            Debug.WriteLine("Shader compile error: " + log);
            gl.DeleteShader(shader);
            return 0;
        }
    }
}
