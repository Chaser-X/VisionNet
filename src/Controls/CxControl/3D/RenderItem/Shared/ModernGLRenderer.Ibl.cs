using System;
using System.Diagnostics;
using SharpGL;

namespace VisionNet.Controls
{
    /// <summary>
    /// Image-based-lighting resources for the shared surface program: the procedural irradiance /
    /// prefilter cubemaps and the BRDF LUT, plus the per-frame uniform setup that selects the
    /// lighting model. Textures are context-owned and released with the other programs.
    /// </summary>
    internal sealed partial class ModernGLRenderer
    {
        private uint _irradianceMap;
        private uint _prefilterMap;
        private uint _brdfLut;
        private bool _iblFailed;
        private SurfaceEnvironmentPreset _iblPreset;
        private bool _iblLoaded;

        // Cached uniform locations on the shared surface program.
        private int _lightingModeLoc = -1;
        private int _impostorLoc = -1;
        private int _envFollowsCameraLoc = -1;
        private int _iblIntensityLoc = -1;
        private int _prefilterMaxMipLoc = -1;
        private int _irradianceMapLoc = -1;
        private int _prefilterMapLoc = -1;
        private int _brdfLutLoc = -1;

        // GL_POINT_SPRITE (0x8861). Present in a compatibility profile; in a core profile point
        // sprites are always on and enabling it is a harmless INVALID_ENUM. Not exposed by SharpGL.
        private const uint GlPointSprite = 0x8861;

        /// <summary>
        /// Selects the lighting model for the current frame and, in <see cref="SurfaceLightingMode.Pbr"/>,
        /// binds the IBL textures to units 1–3. Called once per frame before the surface items are
        /// drawn; the uniform values persist in the shared program for every item of the frame.
        /// </summary>
        internal void BeginSurfaceLighting(OpenGL gl, SurfaceLightingMode mode,
            SurfaceEnvironmentPreset preset, float iblIntensity, bool impostor,
            bool environmentFollowsCamera)
        {
            if (!EnsureSurfaceProgram(gl)) return;

            if (mode == SurfaceLightingMode.Pbr)
                EnsureIbl(gl, preset);

            // Point sprites must be enabled in a compatibility profile for gl_PointCoord to be
            // populated (core profiles always have them on).
            if (impostor) gl.Enable(GlPointSprite);

            gl.UseProgram(_surfaceProgram);

            if (_lightingModeLoc < 0)
                _lightingModeLoc = gl.GetUniformLocation(_surfaceProgram, "lightingMode");
            if (_impostorLoc < 0)
                _impostorLoc = gl.GetUniformLocation(_surfaceProgram, "impostor");
            if (_envFollowsCameraLoc < 0)
                _envFollowsCameraLoc = gl.GetUniformLocation(_surfaceProgram, "envFollowsCamera");
            if (_iblIntensityLoc < 0)
                _iblIntensityLoc = gl.GetUniformLocation(_surfaceProgram, "iblIntensity");
            if (_prefilterMaxMipLoc < 0)
                _prefilterMaxMipLoc = gl.GetUniformLocation(_surfaceProgram, "prefilterMaxMip");

            gl.Uniform1(_lightingModeLoc, (int)mode);
            gl.Uniform1(_impostorLoc, impostor ? 1 : 0);
            gl.Uniform1(_envFollowsCameraLoc, environmentFollowsCamera ? 1 : 0);
            gl.Uniform1(_iblIntensityLoc, iblIntensity);
            gl.Uniform1(_prefilterMaxMipLoc, (float)(IblEnvironment.PrefilterMipCount - 1));

            if (mode == SurfaceLightingMode.Pbr && _irradianceMap != 0)
            {
                if (_irradianceMapLoc < 0)
                    _irradianceMapLoc = gl.GetUniformLocation(_surfaceProgram, "irradianceMap");
                if (_prefilterMapLoc < 0)
                    _prefilterMapLoc = gl.GetUniformLocation(_surfaceProgram, "prefilterMap");
                if (_brdfLutLoc < 0)
                    _brdfLutLoc = gl.GetUniformLocation(_surfaceProgram, "brdfLUT");

                gl.ActiveTexture(OpenGL.GL_TEXTURE1);
                gl.BindTexture(OpenGL.GL_TEXTURE_CUBE_MAP, _irradianceMap);
                gl.ActiveTexture(OpenGL.GL_TEXTURE2);
                gl.BindTexture(OpenGL.GL_TEXTURE_CUBE_MAP, _prefilterMap);
                gl.ActiveTexture(OpenGL.GL_TEXTURE3);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, _brdfLut);

                gl.Uniform1(_irradianceMapLoc, 1);
                gl.Uniform1(_prefilterMapLoc, 2);
                gl.Uniform1(_brdfLutLoc, 3);
            }

            gl.UseProgram(0);
            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
        }

        /// <summary>Generates and uploads the IBL textures for <paramref name="preset"/> if needed.</summary>
        private void EnsureIbl(OpenGL gl, SurfaceEnvironmentPreset preset)
        {
            if (_iblLoaded && _iblPreset == preset) return;
            if (_iblFailed && _iblPreset == preset) return;

            ReleaseIbl(gl);
            try
            {
                var data = ProceduralIbl.Get(preset);
                _irradianceMap = UploadCubeLevel(gl, data.Irradiance,
                    IblEnvironment.IrradianceSize, mipmapped: false);
                _prefilterMap = UploadCubeChain(gl, data.Prefilter);
                _brdfLut = UploadBrdfLut(gl, data.Brdf, IblEnvironment.BrdfSize);
                _iblPreset = preset;
                _iblLoaded = true;
                _iblFailed = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CxControl] ModernGLRenderer.EnsureIbl failed: " + ex.Message);
                ReleaseIbl(gl);
                _iblPreset = preset;
                _iblFailed = true;
            }
        }

        private static uint CreateCubeTexture(OpenGL gl, int levels, bool mipmapped)
        {
            var ids = new uint[1];
            gl.GenTextures(1, ids);
            uint tex = ids[0];
            gl.BindTexture(OpenGL.GL_TEXTURE_CUBE_MAP, tex);
            gl.TexParameter(OpenGL.GL_TEXTURE_CUBE_MAP, OpenGL.GL_TEXTURE_MIN_FILTER,
                mipmapped ? OpenGL.GL_LINEAR_MIPMAP_LINEAR : OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_CUBE_MAP, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_CUBE_MAP, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_CUBE_MAP, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_CUBE_MAP, OpenGL.GL_TEXTURE_WRAP_R, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_CUBE_MAP, OpenGL.GL_TEXTURE_MAX_LEVEL, levels - 1);
            return tex;
        }

        private static uint UploadCubeLevel(OpenGL gl, float[] faces, int size, bool mipmapped)
        {
            uint tex = CreateCubeTexture(gl, 1, mipmapped);
            int faceLen = size * size * 3;
            for (int face = 0; face < 6; face++)
            {
                gl.TexImage2D(OpenGL.GL_TEXTURE_CUBE_MAP_POSITIVE_X + (uint)face, 0,
                    OpenGL.GL_RGB16F, size, size, 0, OpenGL.GL_RGB, OpenGL.GL_FLOAT,
                    Slice(faces, face * faceLen, faceLen));
            }
            gl.BindTexture(OpenGL.GL_TEXTURE_CUBE_MAP, 0);
            return tex;
        }

        private static uint UploadCubeChain(OpenGL gl, float[][] mips)
        {
            uint tex = CreateCubeTexture(gl, mips.Length, true);
            for (int mip = 0; mip < mips.Length; mip++)
            {
                int size = Math.Max(1, IblEnvironment.PrefilterBaseSize >> mip);
                int faceLen = size * size * 3;
                var faces = mips[mip];
                for (int face = 0; face < 6; face++)
                {
                    gl.TexImage2D(OpenGL.GL_TEXTURE_CUBE_MAP_POSITIVE_X + (uint)face, mip,
                        OpenGL.GL_RGB16F, size, size, 0, OpenGL.GL_RGB, OpenGL.GL_FLOAT,
                        Slice(faces, face * faceLen, faceLen));
                }
            }
            gl.BindTexture(OpenGL.GL_TEXTURE_CUBE_MAP, 0);
            return tex;
        }

        private static uint UploadBrdfLut(OpenGL gl, float[] lut, int size)
        {
            var ids = new uint[1];
            gl.GenTextures(1, ids);
            uint tex = ids[0];
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, tex);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RG16F, size, size, 0,
                OpenGL.GL_RG, OpenGL.GL_FLOAT, lut);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            return tex;
        }

        /// <summary>Deletes the IBL textures (must run with the owning context current).</summary>
        private void ReleaseIbl(OpenGL gl)
        {
            if (_irradianceMap != 0) { gl.DeleteTextures(1, new[] { _irradianceMap }); _irradianceMap = 0; }
            if (_prefilterMap != 0) { gl.DeleteTextures(1, new[] { _prefilterMap }); _prefilterMap = 0; }
            if (_brdfLut != 0) { gl.DeleteTextures(1, new[] { _brdfLut }); _brdfLut = 0; }
            _iblLoaded = false;
        }

        private static float[] Slice(float[] src, int offset, int count)
        {
            var dst = new float[count];
            Array.Copy(src, offset, dst, 0, count);
            return dst;
        }
    }
}
