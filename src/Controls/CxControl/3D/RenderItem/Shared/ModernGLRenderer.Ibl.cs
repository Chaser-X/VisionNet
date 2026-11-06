using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;

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

        /// <summary>
        /// Selects the lighting model for the current frame and, in <see cref="SurfaceLightingMode.Pbr"/>,
        /// binds the IBL textures to units 1–3. Called once per frame before the surface items are
        /// drawn; the uniform values persist in the shared program for every item of the frame.
        /// </summary>
        internal void BeginSurfaceLighting(CxGlContext gl, SurfaceLightingMode mode,
            SurfaceEnvironmentPreset preset, float iblIntensity, bool impostor,
            bool environmentFollowsCamera)
        {
            if (!EnsureSurfaceProgram(gl)) return;

            if (mode == SurfaceLightingMode.Pbr)
                EnsureIbl(gl, preset);

            // Point sprites must be enabled in a compatibility profile for gl_PointCoord to be
            // populated (core profiles always have them on).
            if (impostor) GL.Enable(EnableCap.PointSprite);

            GL.UseProgram(_surfaceProgram);

            if (_lightingModeLoc < 0)
                _lightingModeLoc = GL.GetUniformLocation(_surfaceProgram, "lightingMode");
            if (_impostorLoc < 0)
                _impostorLoc = GL.GetUniformLocation(_surfaceProgram, "impostor");
            if (_envFollowsCameraLoc < 0)
                _envFollowsCameraLoc = GL.GetUniformLocation(_surfaceProgram, "envFollowsCamera");
            if (_iblIntensityLoc < 0)
                _iblIntensityLoc = GL.GetUniformLocation(_surfaceProgram, "iblIntensity");
            if (_prefilterMaxMipLoc < 0)
                _prefilterMaxMipLoc = GL.GetUniformLocation(_surfaceProgram, "prefilterMaxMip");

            GL.Uniform1(_lightingModeLoc, (int)mode);
            GL.Uniform1(_impostorLoc, impostor ? 1 : 0);
            GL.Uniform1(_envFollowsCameraLoc, environmentFollowsCamera ? 1 : 0);
            GL.Uniform1(_iblIntensityLoc, iblIntensity);
            GL.Uniform1(_prefilterMaxMipLoc, (float)(IblEnvironment.PrefilterMipCount - 1));

            if (mode == SurfaceLightingMode.Pbr && _irradianceMap != 0)
            {
                if (_irradianceMapLoc < 0)
                    _irradianceMapLoc = GL.GetUniformLocation(_surfaceProgram, "irradianceMap");
                if (_prefilterMapLoc < 0)
                    _prefilterMapLoc = GL.GetUniformLocation(_surfaceProgram, "prefilterMap");
                if (_brdfLutLoc < 0)
                    _brdfLutLoc = GL.GetUniformLocation(_surfaceProgram, "brdfLUT");

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.TextureCubeMap, _irradianceMap);
                GL.ActiveTexture(TextureUnit.Texture2);
                GL.BindTexture(TextureTarget.TextureCubeMap, _prefilterMap);
                GL.ActiveTexture(TextureUnit.Texture3);
                GL.BindTexture(TextureTarget.Texture2D, _brdfLut);

                GL.Uniform1(_irradianceMapLoc, 1);
                GL.Uniform1(_prefilterMapLoc, 2);
                GL.Uniform1(_brdfLutLoc, 3);
            }

            GL.UseProgram(0);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>Generates and uploads the IBL textures for <paramref name="preset"/> if needed.</summary>
        private void EnsureIbl(CxGlContext gl, SurfaceEnvironmentPreset preset)
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

        private static uint CreateCubeTexture(int levels, bool mipmapped)
        {
            var ids = new uint[1];
            GL.GenTextures(1, ids);
            uint tex = ids[0];
            GL.BindTexture(TextureTarget.TextureCubeMap, tex);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter,
                mipmapped ? (int)All.LinearMipmapLinear : (int)All.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMaxLevel, levels - 1);
            return tex;
        }

        private static uint UploadCubeLevel(CxGlContext gl, float[] faces, int size, bool mipmapped)
        {
            uint tex = CreateCubeTexture(1, mipmapped);
            int faceLen = size * size * 3;
            for (int face = 0; face < 6; face++)
            {
                GL.TexImage2D((TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face), 0,
                    PixelInternalFormat.Rgb16f, size, size, 0, PixelFormat.Rgb, PixelType.Float,
                    Slice(faces, face * faceLen, faceLen));
            }
            GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            return tex;
        }

        private static uint UploadCubeChain(CxGlContext gl, float[][] mips)
        {
            uint tex = CreateCubeTexture(mips.Length, true);
            for (int mip = 0; mip < mips.Length; mip++)
            {
                int size = Math.Max(1, IblEnvironment.PrefilterBaseSize >> mip);
                int faceLen = size * size * 3;
                var faces = mips[mip];
                for (int face = 0; face < 6; face++)
                {
                    GL.TexImage2D((TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face), mip,
                        PixelInternalFormat.Rgb16f, size, size, 0, PixelFormat.Rgb, PixelType.Float,
                        Slice(faces, face * faceLen, faceLen));
                }
            }
            GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            return tex;
        }

        private static uint UploadBrdfLut(CxGlContext gl, float[] lut, int size)
        {
            var ids = new uint[1];
            GL.GenTextures(1, ids);
            uint tex = ids[0];
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16f, size, size, 0,
                PixelFormat.Rg, PixelType.Float, lut);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return tex;
        }

        /// <summary>Deletes the IBL textures (must run with the owning context current).</summary>
        private void ReleaseIbl(CxGlContext gl)
        {
            if (_irradianceMap != 0) { GL.DeleteTextures(1, new[] { _irradianceMap }); _irradianceMap = 0; }
            if (_prefilterMap != 0) { GL.DeleteTextures(1, new[] { _prefilterMap }); _prefilterMap = 0; }
            if (_brdfLut != 0) { GL.DeleteTextures(1, new[] { _brdfLut }); _brdfLut = 0; }
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
