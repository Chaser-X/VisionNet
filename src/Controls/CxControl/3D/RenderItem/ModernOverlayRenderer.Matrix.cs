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
    internal sealed partial class ModernOverlayRenderer
    {

        /// <summary>
        /// Deletes programs, buffers, textures, and model-stack state owned by this
        /// renderer. The caller must have made the owning context current.
        /// </summary>
        private void ReleasePrograms(OpenGL gl)
        {
            gl.BindVertexArray(0);
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0);
            if (_geometryProgram != 0)
            {
                gl.DeleteProgram(_geometryProgram);
                _geometryProgram = 0;
            }
            if (_surfaceProgram != 0)
            {
                gl.DeleteProgram(_surfaceProgram);
                _surfaceProgram = 0;
            }
            _surfaceProgramFailed = false;
            _geometryProgramFailed = false;
            _textProgramFailed = false;
            _geometryProjection = _geometryView = _geometryModel = -1;
            _geometryUseVertexColor = _geometryColor = -1;
            if (_textProgram != 0)
            {
                gl.DeleteProgram(_textProgram);
                _textProgram = 0;
            }
            _textProjection = _textColor = _textGlyph = -1;
            if (_sphereProgram != 0)
            {
                gl.DeleteProgram(_sphereProgram);
                _sphereProgram = 0;
            }
            _sphereProgramFailed = false;
            _sphereProjection = _sphereView = _sphereModel = -1;
            _sphereColor = _sphereRadius = -1;
            if (_geometryVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _geometryVbo });
                _geometryVbo = 0;
            }
            if (_geometryVao != 0)
            {
                gl.DeleteVertexArrays(1, new[] { _geometryVao });
                _geometryVao = 0;
            }
            if (_textVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _textVbo });
                _textVbo = 0;
            }
            if (_textVao != 0)
            {
                gl.DeleteVertexArrays(1, new[] { _textVao });
                _textVao = 0;
            }
            if (_sphereInstanceVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _sphereInstanceVbo });
                _sphereInstanceVbo = 0;
            }
            if (_sphereEbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _sphereEbo });
                _sphereEbo = 0;
            }
            if (_sphereVbo != 0)
            {
                gl.DeleteBuffers(1, new[] { _sphereVbo });
                _sphereVbo = 0;
            }
            if (_sphereVao != 0)
            {
                gl.DeleteVertexArrays(1, new[] { _sphereVao });
                _sphereVao = 0;
            }
            _sphereIndexCount = 0;
            foreach (var cached in _textTextures.Values)
                if (cached.Texture != 0)
                    gl.DeleteTextures(1, new[] { cached.Texture });
            _textTextures.Clear();
            _textTextureOrder.Clear();
            _textTexturesUsedThisFrame.Clear();
            foreach (var atlas in _glyphAtlases.Values)
                if (atlas.Texture != 0)
                    gl.DeleteTextures(1, new[] { atlas.Texture });
            _glyphAtlases.Clear();
            _states.Clear();
            _frameStateCaptured = false;
            _hasSceneMatrices = false;
            _viewportWidth = _viewportHeight = -1;
        }

        /// <summary>
        /// Preserves the standalone public text helpers' historical behaviour by
        /// using the caller's active fixed-function matrices when no CxDisplay frame
        /// has supplied explicit scene matrices.
        /// </summary>
        private void ReadCurrentGLMatrices(OpenGL gl)
        {
            gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, _projectionQuery);
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, _viewQuery);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    _projection[row * 4 + col] = _projectionQuery[col * 4 + row];
                    _view[row * 4 + col] = _viewQuery[col * 4 + row];
                }
        }

        /// <summary>
        /// Supplies the caller's active fixed-function matrices for standalone draws.
        /// CxDisplay sets explicit matrices for a frame, so its shader path does not
        /// depend on whatever legacy matrix state happens to be active.
        /// </summary>
        internal void EnsureSceneMatrices(OpenGL gl)
        {
            if (!_hasSceneMatrices)
                ReadCurrentGLMatrices(gl);
        }

        /// <summary>Expands 2D screen vertices to z=0 positions for the geometry shader.</summary>
        private float[] To3D(float[] values)
        {
            int required = values.Length / 2 * 3;
            EnsureCapacity(ref _screenPositions3D, required);
            float[] result = _screenPositions3D;
            for (int i = 0; i < values.Length / 2; i++)
            {
                result[i * 3] = values[i * 2];
                result[i * 3 + 1] = values[i * 2 + 1];
                result[i * 3 + 2] = 0f;
            }
            return result;
        }

        private static void EnsureCapacity(ref float[] buffer, int required)
        {
            if (buffer.Length < required)
                buffer = new float[required];
        }

        private static void Multiply4x4(float[] left, float[] right, float[] result)
        {
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                    result[row * 4 + col] = left[row * 4] * right[col]
                        + left[row * 4 + 1] * right[4 + col]
                        + left[row * 4 + 2] * right[8 + col]
                        + left[row * 4 + 3] * right[12 + col];
        }

        private void RefreshViewport(OpenGL gl)
        {
            if (gl.RenderContextProvider == null) return;
            int width = gl.RenderContextProvider.Width;
            int height = gl.RenderContextProvider.Height;
            if (width == _viewportWidth && height == _viewportHeight) return;
            gl.GetInteger(OpenGL.GL_VIEWPORT, _viewport);
            _viewportWidth = width;
            _viewportHeight = height;
        }

        /// <summary>Returns an identity matrix in the renderer's row-major layout.</summary>
        private static float[] IdentityData() => new float[]
        {
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1
        };

        /// <summary>Builds the pixel-to-NDC orthographic matrix for a viewport.</summary>
        private float[] Ortho(int width, int height)
        {
            // Row-major matrix with the translation in the last column. The
            // renderer uploads row-major arrays with transpose=true, so this
            // maps pixel coordinates (0,0) to the lower-left NDC corner.
            _ortho[0] = 2f / Math.Max(1, width); _ortho[1] = 0; _ortho[2] = 0; _ortho[3] = -1;
            _ortho[4] = 0; _ortho[5] = 2f / Math.Max(1, height); _ortho[6] = 0; _ortho[7] = -1;
            _ortho[8] = 0; _ortho[9] = 0; _ortho[10] = -1; _ortho[11] = 0;
            _ortho[12] = 0; _ortho[13] = 0; _ortho[14] = 0; _ortho[15] = 1;
            return _ortho;
        }

    }
}
