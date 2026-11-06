using System;
using System.Collections.Generic;
using System.Drawing;
using OpenTK.Graphics.OpenGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    internal sealed partial class ModernGLRenderer
    {
        /// <summary>Longitude subdivisions of the instanced unit sphere mesh.</summary>
        private const int SphereSlices = 10;

        /// <summary>Latitude subdivisions of the instanced unit sphere mesh.</summary>
        private const int SphereStacks = 10;

        /// <summary>Uploads camera matrices and one item model matrix to a shader program.</summary>
        internal void UploadSceneMatrices(uint program, CxMatrix4X4 model)
        {
            EnsureSceneMatrices();
            GL.UniformMatrix4(GL.GetUniformLocation(program, "projection"), 1, true, _projection);
            GL.UniformMatrix4(GL.GetUniformLocation(program, "view"), 1, true, _view);
            GL.UniformMatrix4(GL.GetUniformLocation(program, "model"), 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
        }

        /// <summary>Uploads scene matrices using locations cached with the resource handle.</summary>
        internal void UploadSceneMatrices(GLResourceHandle handle, CxMatrix4X4 model)
        {
            EnsureSceneMatrices();
            GL.UniformMatrix4(handle.GetUniformLocation("projection"), 1, true, _projection);
            GL.UniformMatrix4(handle.GetUniformLocation("view"), 1, true, _view);
            GL.UniformMatrix4(handle.GetUniformLocation("model"), 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
        }

        /// <summary>
        /// Uploads the normal matrix <c>transpose(inverse(model))</c> (upper-left 3×3) for the
        /// shared surface program. The caller passes the item's already-cached pose
        /// <b>inverse</b> so no extra inversion happens here; the array is transposed into the
        /// row-major layout the other matrices use.
        /// </summary>
        internal void UploadNormalMatrix(GLResourceHandle handle, CxMatrix4X4 modelInverse)
        {
            var inv = (modelInverse ?? CxMatrix4X4.Identity()).Data;
            _normalMatrix[0] = inv[0]; _normalMatrix[1] = inv[4]; _normalMatrix[2] = inv[8];
            _normalMatrix[3] = inv[1]; _normalMatrix[4] = inv[5]; _normalMatrix[5] = inv[9];
            _normalMatrix[6] = inv[2]; _normalMatrix[7] = inv[6]; _normalMatrix[8] = inv[10];
            GL.UniformMatrix3(handle.GetUniformLocation("normalMatrix"), 1, true, _normalMatrix);
        }

        /// <summary>Gets the context-owned shader shared by the built-in surface items.</summary>
        internal uint SurfaceProgram => _surfaceProgram;

        /// <summary>Compiles the built-in surface shader once per GL context.</summary>
        internal bool EnsureSurfaceProgram()
        {
            if (_surfaceProgram != 0) return true;
            if (_surfaceProgramFailed) return false;

            _surfaceProgram = ShaderProgramCompiler.CompileProgram(
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
        internal void DrawSpheres(float[] positions, Color color, float radius,
            CxMatrix4X4 model)
        {
            if (positions == null || positions.Length < 3) return;
            if (!EnsureSphereProgram()) return;
            EnsureSphereBuffers();

            EnsureSceneMatrices();
            GL.BindVertexArray(_sphereVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _sphereInstanceVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, positions.Length * sizeof(float),
                positions, BufferUsageHint.StreamDraw);

            GL.UseProgram(_sphereProgram);
            GL.UniformMatrix4(_sphereProjection, 1, true, _projection);
            GL.UniformMatrix4(_sphereView, 1, true, _view);
            GL.UniformMatrix4(_sphereModel, 1, true,
                (model ?? CxMatrix4X4.Identity()).Data);
            GL.Uniform1(_sphereRadius, radius);
            GL.Uniform4(_sphereColor,
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, _sphereIndexCount,
                DrawElementsType.UnsignedShort, IntPtr.Zero, positions.Length / 3);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.UseProgram(0);
        }

        private bool EnsureSphereProgram()
        {
            if (_sphereProgram != 0) return true;
            if (_sphereProgramFailed) return false;

            _sphereProgram = ShaderProgramCompiler.CompileProgram(SphereVertex, SphereFragment);
            if (_sphereProgram == 0)
            {
                _sphereProgramFailed = true;
                return false;
            }
            _sphereProjection = GL.GetUniformLocation(_sphereProgram, "projection");
            _sphereView = GL.GetUniformLocation(_sphereProgram, "view");
            _sphereModel = GL.GetUniformLocation(_sphereProgram, "model");
            _sphereColor = GL.GetUniformLocation(_sphereProgram, "aColor");
            _sphereRadius = GL.GetUniformLocation(_sphereProgram, "radius");
            return true;
        }

        private void EnsureSphereBuffers()
        {
            if (_sphereVao != 0) return;

            var ids = new uint[1];
            GL.GenVertexArrays(1, ids);
            _sphereVao = ids[0];
            GL.BindVertexArray(_sphereVao);

            GL.GenBuffers(1, ids);
            _sphereVbo = ids[0];
            GL.BindBuffer(BufferTarget.ArrayBuffer, _sphereVbo);
            var vertices = new List<float>();
            var indices = new List<ushort>();
            GenerateUnitSphereData(SphereSlices, SphereStacks, vertices, indices);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float),
                vertices.ToArray(), BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false,
                3 * sizeof(float), IntPtr.Zero);

            GL.GenBuffers(1, ids);
            _sphereEbo = ids[0];
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _sphereEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(ushort),
                indices.ToArray(), BufferUsageHint.StaticDraw);
            _sphereIndexCount = indices.Count;

            GL.GenBuffers(1, ids);
            _sphereInstanceVbo = ids[0];
            GL.BindBuffer(BufferTarget.ArrayBuffer, _sphereInstanceVbo);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false,
                3 * sizeof(float), IntPtr.Zero);
            GL.VertexAttribDivisor(1, 1);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
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
        private bool EnsureGeometryProgram()
        {
            if (_geometryProgram != 0) return true;
            if (_geometryProgramFailed) return false;
            _geometryProgram = ShaderProgramCompiler.CompileProgram(GeometryVertex, GeometryFragment);
            if (_geometryProgram == 0)
            {
                _geometryProgramFailed = true;
                return false;
            }
            _geometryProjection = GL.GetUniformLocation(_geometryProgram, "projection");
            _geometryView = GL.GetUniformLocation(_geometryProgram, "view");
            _geometryModel = GL.GetUniformLocation(_geometryProgram, "model");
            _geometryUseVertexColor = GL.GetUniformLocation(_geometryProgram, "useVertexColor");
            _geometryColor = GL.GetUniformLocation(_geometryProgram, "color");
            return true;
        }

        /// <summary>Allocates the reusable geometry VAO/VBO on first use.</summary>
        private void EnsureGeometryBuffers()
        {
            if (_geometryVao == 0)
            {
                var ids = new uint[1];
                GL.GenVertexArrays(1, ids);
                _geometryVao = ids[0];
            }
            if (_geometryVbo == 0)
            {
                var ids = new uint[1];
                GL.GenBuffers(1, ids);
                _geometryVbo = ids[0];
            }
        }

        /// <summary>Compiles the screen-text program once per GL context.</summary>
        private bool EnsureTextProgram()
        {
            if (_textProgram != 0) return true;
            if (_textProgramFailed) return false;
            _textProgram = ShaderProgramCompiler.CompileProgram(TextVertex, TextFragment);
            if (_textProgram == 0)
            {
                _textProgramFailed = true;
                return false;
            }
            _textProjection = GL.GetUniformLocation(_textProgram, "projection");
            _textColor = GL.GetUniformLocation(_textProgram, "color");
            _textGlyph = GL.GetUniformLocation(_textProgram, "glyph");
            return true;
        }

        /// <summary>Allocates the reusable text quad VAO/VBO on first use.</summary>
        private void EnsureTextBuffers()
        {
            if (_textVao == 0)
            {
                var ids = new uint[1];
                GL.GenVertexArrays(1, ids);
                _textVao = ids[0];
            }
            if (_textVbo == 0)
            {
                var ids = new uint[1];
                GL.GenBuffers(1, ids);
                _textVbo = ids[0];
            }
        }
    }
}
