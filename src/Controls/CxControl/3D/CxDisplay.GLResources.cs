using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SharpGL;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
        private void CreateGLResources(OpenGL gl, ICxObjRenderItem item, GLResourceHandle handle)
        {
            var data = item.PrepareRenderData();
            if (data == null) return;

            var tempId = new uint[1];

            if (data.UseVAO)
            {
                gl.GenVertexArrays(1, tempId);
                handle.VaoId = tempId[0];
                handle.HasVAO = true;
                gl.BindVertexArray(handle.VaoId);
            }

            int vboIndex = 0;

            if (data.Vertices != null)
            {
                gl.GenBuffers(1, tempId);
                handle.VboIds[vboIndex] = tempId[0];
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[vboIndex]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, data.Vertices, OpenGL.GL_STATIC_DRAW);
                if (data.UseVAO)
                {
                    gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
                    gl.EnableVertexAttribArray(0);
                }
                vboIndex++;
            }

            if (data.Colors != null)
            {
                gl.GenBuffers(1, tempId);
                handle.VboIds[vboIndex] = tempId[0];
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[vboIndex]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, data.Colors, OpenGL.GL_STATIC_DRAW);
                if (data.UseVAO)
                {
                    gl.VertexAttribPointer(1, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
                    gl.EnableVertexAttribArray(1);
                }
                vboIndex++;
            }
            else if (data.UVCoords != null)
            {
                gl.GenBuffers(1, tempId);
                handle.VboIds[vboIndex] = tempId[0];
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[vboIndex]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, data.UVCoords, OpenGL.GL_STATIC_DRAW);
                if (data.UseVAO)
                {
                    gl.VertexAttribPointer(1, 2, OpenGL.GL_FLOAT, false, 2 * sizeof(float), IntPtr.Zero);
                    gl.EnableVertexAttribArray(1);
                }
                vboIndex++;
            }

            handle.VboCount = vboIndex;

            if (data.Indices != null && data.Indices.Length > 0)
            {
                gl.GenBuffers(1, tempId);
                handle.ElementBufferId = tempId[0];
                handle.HasEBO = true;

                gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, handle.ElementBufferId);
                int bytes = data.Indices.Length * sizeof(uint);
                IntPtr ptr = Marshal.AllocHGlobal(bytes);
                try
                {
                    var indexBytes = new byte[bytes];
                    Buffer.BlockCopy(data.Indices, 0, indexBytes, 0, bytes);
                    Marshal.Copy(indexBytes, 0, ptr, bytes);
                    gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, bytes, ptr, OpenGL.GL_STATIC_DRAW);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            if (data.ShaderSource != null)
            {
                handle.ShaderProgram = CompileShader(gl, data.ShaderSource);
                handle.HasShader = true;
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

            if (handle.HasShader && handle.ShaderProgram != 0)
            {
                gl.DeleteProgram(handle.ShaderProgram);
                handle.ShaderProgram = 0;
                handle.HasShader = false;
            }

            if (handle.HasTexture && handle.TextureId != 0)
            {
                gl.DeleteTextures(1, new[] { handle.TextureId });
                handle.TextureId = 0;
                handle.HasTexture = false;
            }

            handle.IsValid = false;
        }

        private uint CompileShader(OpenGL gl, ShaderSource source)
        {
            uint vs = gl.CreateShader(OpenGL.GL_VERTEX_SHADER);
            gl.ShaderSource(vs, source.VertexSource);
            gl.CompileShader(vs);
            LogShaderError(gl, vs, "vertex");

            uint fs = gl.CreateShader(OpenGL.GL_FRAGMENT_SHADER);
            gl.ShaderSource(fs, source.FragmentSource);
            gl.CompileShader(fs);
            LogShaderError(gl, fs, "fragment");

            uint prog = gl.CreateProgram();
            gl.AttachShader(prog, vs);
            gl.AttachShader(prog, fs);
            gl.LinkProgram(prog);

            int[] status = new int[1];
            gl.GetProgram(prog, OpenGL.GL_LINK_STATUS, status);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] len = new int[1];
                gl.GetProgram(prog, OpenGL.GL_INFO_LOG_LENGTH, len);
                var log = new StringBuilder(len[0]);
                gl.GetProgramInfoLog(prog, len[0], IntPtr.Zero, log);
                Debug.WriteLine("Shader link error: " + log);
            }

            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            return prog;
        }

        private void LogShaderError(OpenGL gl, uint shader, string stage)
        {
            int[] status = new int[1];
            gl.GetShader(shader, OpenGL.GL_COMPILE_STATUS, status);
            if (status[0] != OpenGL.GL_FALSE) return;
            int[] len = new int[1];
            gl.GetShader(shader, OpenGL.GL_INFO_LOG_LENGTH, len);
            var log = new StringBuilder(len[0]);
            gl.GetShaderInfoLog(shader, len[0], IntPtr.Zero, log);
            Debug.WriteLine($"{stage} shader error: {log}");
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

            IntPtr ptr = Marshal.AllocHGlobal(uploadData.Length);
            try
            {
                Marshal.Copy(uploadData, 0, ptr, uploadData.Length);
                gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA,
                    uploadW, uploadH, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
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
