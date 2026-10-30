using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    internal sealed partial class ModernGLRenderer
    {

        /// <summary>Uploads camera matrices and one item model matrix to a shader program.</summary>
        internal void UploadSceneMatrices(OpenGL gl, uint program, CxMatrix4X4 model)
        {
            EnsureSceneMatrices(gl);
            gl.UniformMatrix4(gl.GetUniformLocation(program, "projection"), 1, true, _projection);
            gl.UniformMatrix4(gl.GetUniformLocation(program, "view"), 1, true, _view);
            gl.UniformMatrix4(gl.GetUniformLocation(program, "model"), 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
        }

        /// <summary>Uploads scene matrices using locations cached with the resource handle.</summary>
        internal void UploadSceneMatrices(OpenGL gl, GLResourceHandle handle, CxMatrix4X4 model)
        {
            EnsureSceneMatrices(gl);
            gl.UniformMatrix4(handle.GetUniformLocation(gl, "projection"), 1, true, _projection);
            gl.UniformMatrix4(handle.GetUniformLocation(gl, "view"), 1, true, _view);
            gl.UniformMatrix4(handle.GetUniformLocation(gl, "model"), 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
        }

        /// <summary>Gets or creates the model-stack state for a GL wrapper.</summary>
        private State GetState(OpenGL gl)
        {
            State state;
            if (!_states.TryGetValue(gl, out state))
            {
                state = new State();
                _states.Add(gl, state);
            }
            return state;
        }

        /// <summary>Gets the context-owned shader shared by the built-in surface items.</summary>
        internal uint SurfaceProgram => _surfaceProgram;

        /// <summary>Compiles the built-in surface shader once per GL context.</summary>
        internal bool EnsureSurfaceProgram(OpenGL gl)
        {
            if (_surfaceProgram != 0) return true;
            if (_surfaceProgramFailed) return false;

            _surfaceProgram = ShaderProgramCompiler.CompileProgram(
                gl,
                AbstractCxObjRenderItem.SharedVertexShaderSource,
                AbstractCxObjRenderItem.SharedFragmentShaderSource);
            if (_surfaceProgram == 0)
            {
                _surfaceProgramFailed = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Draws instanced unit spheres for a point overlay. The sphere mesh and shader are
        /// context-owned; only the instance positions are streamed for each draw.
        /// </summary>
        internal void DrawSpheres(OpenGL gl, float[] positions, Color color, float radius,
            CxMatrix4X4 model)
        {
            if (positions == null || positions.Length < 3) return;
            if (!EnsureSphereProgram(gl)) return;
            EnsureSphereBuffers(gl);

            EnsureSceneMatrices(gl);
            gl.BindVertexArray(_sphereVao);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _sphereInstanceVbo);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, positions, OpenGL.GL_STREAM_DRAW);

            gl.UseProgram(_sphereProgram);
            gl.UniformMatrix4(_sphereProjection, 1, true, _projection);
            gl.UniformMatrix4(_sphereView, 1, true, _view);
            gl.UniformMatrix4(_sphereModel, 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
            gl.Uniform1(_sphereRadius, radius);
            gl.Uniform4(_sphereColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            gl.DrawElementsInstanced(OpenGL.GL_TRIANGLES, _sphereIndexCount,
                OpenGL.GL_UNSIGNED_SHORT, IntPtr.Zero, positions.Length / 3);

            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            gl.UseProgram(0);
        }

        private bool EnsureSphereProgram(OpenGL gl)
        {
            if (_sphereProgram != 0) return true;
            if (_sphereProgramFailed) return false;

            _sphereProgram = ShaderProgramCompiler.CompileProgram(gl, SphereVertex, SphereFragment);
            if (_sphereProgram == 0)
            {
                _sphereProgramFailed = true;
                return false;
            }
            _sphereProjection = gl.GetUniformLocation(_sphereProgram, "projection");
            _sphereView = gl.GetUniformLocation(_sphereProgram, "view");
            _sphereModel = gl.GetUniformLocation(_sphereProgram, "model");
            _sphereColor = gl.GetUniformLocation(_sphereProgram, "aColor");
            _sphereRadius = gl.GetUniformLocation(_sphereProgram, "radius");
            return true;
        }

        private void EnsureSphereBuffers(OpenGL gl)
        {
            if (_sphereVao != 0) return;

            var ids = new uint[1];
            gl.GenVertexArrays(1, ids);
            _sphereVao = ids[0];
            gl.BindVertexArray(_sphereVao);

            gl.GenBuffers(1, ids);
            _sphereVbo = ids[0];
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _sphereVbo);
            var vertices = new List<float>();
            var indices = new List<ushort>();
            GenerateUnitSphereData(10, 10, vertices, indices);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices.ToArray(), OpenGL.GL_STATIC_DRAW);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false,
                3 * sizeof(float), IntPtr.Zero);

            gl.GenBuffers(1, ids);
            _sphereEbo = ids[0];
            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, _sphereEbo);
            gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, indices.ToArray(), OpenGL.GL_STATIC_DRAW);
            _sphereIndexCount = indices.Count;

            gl.GenBuffers(1, ids);
            _sphereInstanceVbo = ids[0];
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, _sphereInstanceVbo);
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, OpenGL.GL_FLOAT, false,
                3 * sizeof(float), IntPtr.Zero);
            gl.VertexAttribDivisor(1, 1);

            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
        }

        private static void GenerateUnitSphereData(int slices, int stacks,
            List<float> vertices, List<ushort> indices)
        {
            for (int stack = 0; stack <= stacks; stack++)
            {
                float phi = (float)(Math.PI / 2 - stack * Math.PI / stacks);
                float y = (float)Math.Sin(phi);
                float scale = (float)Math.Cos(phi);

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

        /// <summary>Compiles the shared geometry program once per GL context.</summary>
        private bool EnsureGeometryProgram(OpenGL gl)
        {
            if (_geometryProgram != 0) return true;
            if (_geometryProgramFailed) return false;
            _geometryProgram = ShaderProgramCompiler.CompileProgram(gl, GeometryVertex, GeometryFragment);
            if (_geometryProgram == 0)
            {
                _geometryProgramFailed = true;
                return false;
            }
            _geometryProjection = gl.GetUniformLocation(_geometryProgram, "projection");
            _geometryView = gl.GetUniformLocation(_geometryProgram, "view");
            _geometryModel = gl.GetUniformLocation(_geometryProgram, "model");
            _geometryUseVertexColor = gl.GetUniformLocation(_geometryProgram, "useVertexColor");
            _geometryColor = gl.GetUniformLocation(_geometryProgram, "color");
            return true;
        }

        /// <summary>Allocates the reusable geometry VAO/VBO on first use.</summary>
        private void EnsureGeometryBuffers(OpenGL gl)
        {
            if (_geometryVao == 0)
            {
                var ids = new uint[1];
                gl.GenVertexArrays(1, ids);
                _geometryVao = ids[0];
            }
            if (_geometryVbo == 0)
            {
                var ids = new uint[1];
                gl.GenBuffers(1, ids);
                _geometryVbo = ids[0];
            }
        }

        /// <summary>Compiles the screen-text program once per GL context.</summary>
        private bool EnsureTextProgram(OpenGL gl)
        {
            if (_textProgram != 0) return true;
            if (_textProgramFailed) return false;
            _textProgram = ShaderProgramCompiler.CompileProgram(gl, TextVertex, TextFragment);
            if (_textProgram == 0)
            {
                _textProgramFailed = true;
                return false;
            }
            _textProjection = gl.GetUniformLocation(_textProgram, "projection");
            _textColor = gl.GetUniformLocation(_textProgram, "color");
            _textGlyph = gl.GetUniformLocation(_textProgram, "glyph");
            return true;
        }

        /// <summary>Allocates the reusable text quad VAO/VBO on first use.</summary>
        private void EnsureTextBuffers(OpenGL gl)
        {
            if (_textVao == 0)
            {
                var ids = new uint[1];
                gl.GenVertexArrays(1, ids);
                _textVao = ids[0];
            }
            if (_textVbo == 0)
            {
                var ids = new uint[1];
                gl.GenBuffers(1, ids);
                _textVbo = ids[0];
            }
        }
    }
}
