using System;
using System.Runtime.InteropServices;
using SharpGL;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
        private static void UploadPinned(OpenGL gl, uint target, Array data,
            int bytes, uint usage)
        {
            GCHandle pinned = default(GCHandle);
            try
            {
                pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
                gl.BufferData(target, bytes, pinned.AddrOfPinnedObject(), usage);
            }
            finally
            {
                if (pinned.IsAllocated) pinned.Free();
            }
        }

        /// <summary>
        /// Creates one vertex buffer for <paramref name="handle"/>, uploads <paramref name="data"/>
        /// into it, and wires it to <paramref name="attributeIndex"/>. Buffers are always float
        /// data, so the byte count is derived from the array length.
        /// </summary>
        /// <param name="vboIndex">Slot in <see cref="GLResourceHandle.VboIds"/> to fill.</param>
        /// <param name="componentCount">Components per vertex (3 for positions/colours, 2 for UVs, 1 for scalars).</param>
        /// <param name="stride">Byte stride between vertices; 0 means tightly packed.</param>
        /// <returns>The next free VBO slot index.</returns>
        private static int UploadVertexBuffer(OpenGL gl, GLResourceHandle handle, int vboIndex,
            Array data, uint attributeIndex, int componentCount, int stride)
        {
            var tempId = new uint[1];
            gl.GenBuffers(1, tempId);
            handle.VboIds[vboIndex] = tempId[0];
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[vboIndex]);
            UploadPinned(gl, OpenGL.GL_ARRAY_BUFFER, data,
                data.Length * sizeof(float), OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(attributeIndex, componentCount, OpenGL.GL_FLOAT, false,
                stride, IntPtr.Zero);
            gl.EnableVertexAttribArray(attributeIndex);
            return vboIndex + 1;
        }

        private void CreateGLResources(OpenGL gl, ICxObjRenderItem item, GLResourceHandle handle)
        {
            var data = item.PrepareRenderData();
            if (data == null) return;

            handle.ShaderFailed = false;
            handle.OwnsShader = false;

            // Compile or acquire the shader first: a failing shader aborts before any buffer or
            // texture upload. The three built-in ComplexGeometry items share one program per
            // OpenGL context; custom shader sources retain the old per-handle ownership path.
            if (data.ShaderSource != null)
            {
                if (IsSharedSurfaceShader(data.ShaderSource))
                {
                    var renderer = ModernGLRenderer.For(gl);
                    if (renderer.EnsureSurfaceProgram(gl))
                    {
                        handle.ShaderProgram = renderer.SurfaceProgram;
                    }
                }
                else
                {
                    handle.ShaderProgram = ShaderProgramCompiler.CompileProgram(gl,
                        data.ShaderSource.VertexSource, data.ShaderSource.FragmentSource);
                    handle.OwnsShader = true;
                }
                handle.HasShader = handle.ShaderProgram != 0;
                handle.UniformLocations.Clear();
                if (!handle.HasShader)
                {
                    // Remember the failure so ProcessResourcePool does not retry every frame.
                    handle.ShaderFailed = true;
                    handle.IsValid = false;
                    handle.NeedsUpdate = false;
                    // Reported by ProcessResourcePool after the resource lock is released.
                    _failedItems.Add(item);
                    return;
                }
            }

            if (data.VertexCount > 0)
            {
                var tempId = new uint[1];
                gl.GenVertexArrays(1, tempId);
                handle.VaoId = tempId[0];
                handle.HasVAO = true;
                gl.BindVertexArray(handle.VaoId);

                int vboIndex = 0;

                if (data.Vertices != null && data.Vertices.Length >= data.VertexCount * 3)
                    vboIndex = UploadVertexBuffer(gl, handle, vboIndex, data.Vertices,
                        attributeIndex: 0, componentCount: 3, stride: 3 * sizeof(float));

                if (data.Colors != null && data.Colors.Length >= data.VertexCount * 3)
                    vboIndex = UploadVertexBuffer(gl, handle, vboIndex, data.Colors,
                        attributeIndex: 1, componentCount: 3, stride: 3 * sizeof(float));
                else if (data.UVCoords != null && data.UVCoords.Length >= data.VertexCount * 2)
                    vboIndex = UploadVertexBuffer(gl, handle, vboIndex, data.UVCoords,
                        attributeIndex: 1, componentCount: 2, stride: 2 * sizeof(float));

                if (data.DiffValues != null && data.DiffValues.Length >= data.VertexCount)
                    vboIndex = UploadVertexBuffer(gl, handle, vboIndex, data.DiffValues,
                        attributeIndex: 2, componentCount: 1, stride: 0);

                if (data.IntensityValues != null && data.IntensityValues.Length >= data.VertexCount)
                    vboIndex = UploadVertexBuffer(gl, handle, vboIndex, data.IntensityValues,
                        attributeIndex: 3, componentCount: 1, stride: sizeof(float));

                handle.VboCount = vboIndex;

                if (data.Indices != null && data.Indices.Length > 0)
                {
                    gl.GenBuffers(1, tempId);
                    handle.ElementBufferId = tempId[0];
                    handle.HasEBO = true;

                    gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, handle.ElementBufferId);
                    int bytes = data.Indices.Length * sizeof(uint);
                    UploadPinned(gl, OpenGL.GL_ELEMENT_ARRAY_BUFFER, data.Indices,
                        bytes, OpenGL.GL_STATIC_DRAW);
                }
            }

            if (data.TextureData?.Data != null)
            {
                handle.TextureId = CreateTexture(gl, data.TextureData);
                handle.HasTexture = true;
            }

            if (handle.HasVAO)
                gl.BindVertexArray(0);

            handle.IsValid = true;
            handle.NeedsUpdate = false;
        }

        private void ReleaseGLResources(OpenGL gl, GLResourceHandle handle)
        {
            if (handle.VboCount > 0)
            {
                gl.DeleteBuffers(handle.VboCount, handle.VboIds);
                for (int i = 0; i < handle.VboIds.Length; i++) handle.VboIds[i] = 0;
                handle.VboCount = 0;
            }

            if (handle.HasVAO && handle.VaoId != 0)
            {
                gl.DeleteVertexArrays(1, new[] { handle.VaoId });
                handle.VaoId = 0;
                handle.HasVAO = false;
            }

            if (handle.HasEBO && handle.ElementBufferId != 0)
            {
                gl.DeleteBuffers(1, new[] { handle.ElementBufferId });
                handle.ElementBufferId = 0;
                handle.HasEBO = false;
            }

            if (handle.HasShader && handle.OwnsShader && handle.ShaderProgram != 0)
            {
                gl.DeleteProgram(handle.ShaderProgram);
            }
            handle.ShaderProgram = 0;
            handle.HasShader = false;
            handle.OwnsShader = false;
            handle.UniformLocations.Clear();

            if (handle.HasTexture && handle.TextureId != 0)
            {
                gl.DeleteTextures(1, new[] { handle.TextureId });
                handle.TextureId = 0;
                handle.HasTexture = false;
            }

            handle.IsValid = false;
        }

        private static bool IsSharedSurfaceShader(ShaderSource source)
        {
            return source != null
                && string.Equals(source.VertexSource,
                    AbstractCxObjRenderItem.SharedVertexShaderSource, StringComparison.Ordinal)
                && string.Equals(source.FragmentSource,
                    AbstractCxObjRenderItem.SharedFragmentShaderSource, StringComparison.Ordinal);
        }

        private uint CreateTexture(OpenGL gl, TextureData tex)
        {
            int[] maxSizeArr = new int[1];
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, maxSizeArr);
            int maxSize = maxSizeArr[0];

            int uploadW = Math.Min(tex.Width,  maxSize);
            int uploadH = Math.Min(tex.Height, maxSize);
            byte[] uploadData = (uploadW != tex.Width || uploadH != tex.Height)
                ? DownsampleTextureRGBA(tex.Data, tex.Width, tex.Height, uploadW, uploadH)
                : tex.Data;

            var ids = new uint[1];
            gl.GenTextures(1, ids);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, ids[0]);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);

            GCHandle pinned = default(GCHandle);
            try
            {
                pinned = GCHandle.Alloc(uploadData, GCHandleType.Pinned);
                gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA,
                    uploadW, uploadH, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE,
                    pinned.AddrOfPinnedObject());
            }
            finally
            {
                if (pinned.IsAllocated) pinned.Free();
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            }

            return ids[0];
        }

        private static byte[] DownsampleTextureRGBA(
            byte[] src, int srcW, int srcH, int dstW, int dstH)
        {
            float scaleX = (float)srcW / dstW;
            float scaleY = (float)srcH / dstH;
            var dst = new byte[dstW * dstH * 4];

            for (int y = 0; y < dstH; y++)
            {
                int srcY = Math.Min((int)(y * scaleY), srcH - 1);
                for (int x = 0; x < dstW; x++)
                {
                    int srcX = Math.Min((int)(x * scaleX), srcW - 1);
                    int si = (srcY * srcW + srcX) * 4;
                    int di = (y    * dstW + x)    * 4;
                    dst[di]     = src[si];
                    dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2];
                    dst[di + 3] = src[si + 3];
                }
            }

            return dst;
        }
    }
}
