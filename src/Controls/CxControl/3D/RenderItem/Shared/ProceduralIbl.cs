using System;
using System.Collections.Generic;

namespace VisionNet.Controls
{
    /// <summary>Built-in procedural environment presets used by <see cref="SurfaceLightingMode.Pbr"/>.</summary>
    public enum SurfaceEnvironmentPreset
    {
        /// <summary>Cool sky gradient with two warm soft-box highlights (default).</summary>
        Studio,

        /// <summary>Even neutral grey gradient with a single soft highlight.</summary>
        Neutral,
    }

    /// <summary>
    /// CPU-side image-based-lighting data for one environment preset: the cosine-convolved
    /// irradiance cubemap, the GGX-prefiltered specular cubemap chain and the split-sum BRDF LUT.
    /// Everything is generated procedurally from an analytic environment function, so no external
    /// asset file or image decoder is required.
    /// </summary>
    internal sealed class IblEnvironment
    {
        /// <summary>Edge length of one face of the irradiance cubemap.</summary>
        public const int IrradianceSize = 16;

        /// <summary>Edge length of mip 0 of the prefiltered specular cubemap.</summary>
        public const int PrefilterBaseSize = 32;

        /// <summary>Number of mip levels in the prefiltered chain (32, 16, 8, 4, 2).</summary>
        public const int PrefilterMipCount = 5;

        /// <summary>Edge length of the square BRDF LUT.</summary>
        public const int BrdfSize = 128;

        /// <summary>Six faces of the irradiance cubemap, each <c>IrradianceSize² × 3</c> (linear RGB).</summary>
        public float[] Irradiance;

        /// <summary>Prefiltered specular faces per mip; index <c>m</c> is <c>6 × size² × 3</c>.</summary>
        public float[][] Prefilter;

        /// <summary>BRDF LUT texels as <c>BrdfSize² × 2</c> (scale, bias).</summary>
        public float[] Brdf;
    }

    /// <summary>
    /// Generates and caches <see cref="IblEnvironment"/> instances. The generation is pure CPU
    /// maths (no GL), deterministic, and cached per preset for the process lifetime, so a context
    /// recreation only pays for the texture upload.
    /// </summary>
    internal static class ProceduralIbl
    {
        private const float Pi = 3.14159265358979f;
        private const float GoldenAngle = 2.39996323f;   // π·(3 − √5)

        private static readonly object Sync = new object();
        private static readonly Dictionary<SurfaceEnvironmentPreset, IblEnvironment> Cache
            = new Dictionary<SurfaceEnvironmentPreset, IblEnvironment>();

        /// <summary>Returns the (cached) IBL data for <paramref name="preset"/>, generating it on first use.</summary>
        public static IblEnvironment Get(SurfaceEnvironmentPreset preset)
        {
            lock (Sync)
            {
                IblEnvironment env;
                if (!Cache.TryGetValue(preset, out env))
                {
                    env = Generate(preset);
                    Cache[preset] = env;
                }
                return env;
            }
        }

        private static IblEnvironment Generate(SurfaceEnvironmentPreset preset)
        {
            var env = new IblEnvironment
            {
                Irradiance = BuildIrradiance(preset),
                Prefilter = BuildPrefilter(preset),
                Brdf = BuildBrdfLut(),
            };
            return env;
        }

        // ── Environment ──────────────────────────────────────────────────────────

        /// <summary>
        /// Analytic environment radiance for a unit direction, in linear RGB. Shared by the
        /// irradiance convolution and the specular prefilter, and exposed for tests.
        /// </summary>
        /// <remarks>
        /// The scene and camera are <b>Z-up</b>: surface heights are Z and the top view looks down
        /// -Z. The environment follows the same convention, so <c>z</c> is the sky axis — a
        /// Y-up environment would light the scene from the side and look "backlit" from the top.
        /// </remarks>
        internal static void SampleEnvironment(SurfaceEnvironmentPreset preset,
            float x, float y, float z, out float r, out float g, out float b)
        {
            float len = (float)Math.Sqrt(x * x + y * y + z * z);
            if (len > 1e-6f) { x /= len; y /= len; z /= len; }

            if (preset == SurfaceEnvironmentPreset.Neutral)
            {
                // Even grey dome: a touch brighter overhead, darker underfoot.
                float up = Math.Max(0f, z);
                float down = Math.Max(0f, -z);
                float grey = 0.45f + 0.25f * up - 0.25f * down;
                r = g = b = grey;
                AddSpot(x, y, z, 0.20f, 0.30f, 0.90f, 1.0f, 1.0f, 1.0f, 1.2f, 12f,
                    ref r, ref g, ref b);
                return;
            }

            // Studio: cool sky above the horizon (z = 0), dark floor below, two soft-box lights.
            const float hzR = 0.35f, hzG = 0.38f, hzB = 0.44f;   // horizon
            const float znR = 0.58f, znG = 0.65f, znB = 0.76f;   // zenith
            const float flR = 0.10f, flG = 0.10f, flB = 0.12f;   // floor

            if (z >= 0f)
            {
                r = hzR + (znR - hzR) * z;
                g = hzG + (znG - hzG) * z;
                b = hzB + (znB - hzB) * z;
            }
            else
            {
                float t = -z;
                r = hzR + (flR - hzR) * t;
                g = hzG + (flG - hzG) * t;
                b = hzB + (flB - hzB) * t;
            }

            // Warm key light, up and to the right-front.
            AddSpot(x, y, z, 0.35f, 0.50f, 0.80f, 1.00f, 0.95f, 0.85f, 3.2f, 48f,
                ref r, ref g, ref b);
            // Cool fill light, up and to the left-back.
            AddSpot(x, y, z, -0.60f, -0.35f, 0.45f, 0.70f, 0.80f, 1.00f, 1.4f, 24f,
                ref r, ref g, ref b);
        }

        private static void AddSpot(float x, float y, float z,
            float lx, float ly, float lz, float cr, float cg, float cb, float intensity, float sharpness,
            ref float r, ref float g, ref float b)
        {
            float len = (float)Math.Sqrt(lx * lx + ly * ly + lz * lz);
            if (len > 1e-6f) { lx /= len; ly /= len; lz /= len; }

            float d = x * lx + y * ly + z * lz;
            if (d <= 0f) return;

            float w = (float)Math.Pow(d, sharpness) * intensity;
            r += cr * w;
            g += cg * w;
            b += cb * w;
        }

        // ── Cube face directions ─────────────────────────────────────────────────

        /// <summary>
        /// Direction of texel (<paramref name="i"/>, <paramref name="j"/>) on cube face
        /// <paramref name="face"/> (GL_TEXTURE_CUBE_MAP_POSITIVE_X .. NEGATIVE_Z order).
        /// </summary>
        private static void FaceDirection(int face, int i, int j, int size,
            out float x, out float y, out float z)
        {
            float u = 2f * (i + 0.5f) / size - 1f;
            float v = 2f * (j + 0.5f) / size - 1f;

            switch (face)
            {
                case 0: x =  1f; y = -v;  z = -u;  break;   // +X
                case 1: x = -1f; y = -v;  z =  u;  break;   // -X
                case 2: x =  u;  y =  1f; z =  v;  break;   // +Y
                case 3: x =  u;  y = -1f; z = -v;  break;   // -Y
                case 4: x =  u;  y = -v;  z =  1f; break;   // +Z
                default: x = -u; y = -v;  z = -1f; break;   // -Z
            }

            float len = (float)Math.Sqrt(x * x + y * y + z * z);
            if (len > 1e-6f) { x /= len; y /= len; z /= len; }
        }

        // ── Irradiance (cosine convolution) ──────────────────────────────────────

        private static float[] BuildIrradiance(SurfaceEnvironmentPreset preset)
        {
            const int size = IblEnvironment.IrradianceSize;
            const int samples = 2048;
            float weight = 4f / samples;   // (4π/N) / π, so a uniform environment maps to itself

            var directions = new float[samples * 3];
            for (int i = 0; i < samples; i++)
            {
                float y = 1f - (i + 0.5f) * 2f / samples;
                float rad = (float)Math.Sqrt(Math.Max(0f, 1f - y * y));
                float theta = i * GoldenAngle;
                directions[i * 3] = (float)Math.Cos(theta) * rad;
                directions[i * 3 + 1] = y;
                directions[i * 3 + 2] = (float)Math.Sin(theta) * rad;
            }

            var result = new float[6 * size * size * 3];
            for (int face = 0; face < 6; face++)
            {
                for (int j = 0; j < size; j++)
                {
                    for (int i = 0; i < size; i++)
                    {
                        FaceDirection(face, i, j, size, out float nx, out float ny, out float nz);
                        float r = 0f, g = 0f, b = 0f;
                        for (int s = 0; s < samples; s++)
                        {
                            float dx = directions[s * 3], dy = directions[s * 3 + 1], dz = directions[s * 3 + 2];
                            float cos = nx * dx + ny * dy + nz * dz;
                            if (cos <= 0f) continue;

                            SampleEnvironment(preset, dx, dy, dz, out float lr, out float lg, out float lb);
                            float w = cos * weight;
                            r += lr * w; g += lg * w; b += lb * w;
                        }

                        int o = ((face * size + j) * size + i) * 3;
                        result[o] = r; result[o + 1] = g; result[o + 2] = b;
                    }
                }
            }
            return result;
        }

        // ── Specular prefilter (GGX importance sampling) ─────────────────────────

        private static float[][] BuildPrefilter(SurfaceEnvironmentPreset preset)
        {
            const int baseSize = IblEnvironment.PrefilterBaseSize;
            const int mipCount = IblEnvironment.PrefilterMipCount;
            const int samples = 64;

            var mips = new float[mipCount][];
            for (int mip = 0; mip < mipCount; mip++)
            {
                int size = Math.Max(1, baseSize >> mip);
                float roughness = mipCount > 1 ? (float)mip / (mipCount - 1) : 0f;
                var faces = new float[6 * size * size * 3];

                for (int face = 0; face < 6; face++)
                {
                    for (int j = 0; j < size; j++)
                    {
                        for (int i = 0; i < size; i++)
                        {
                            // The prefilter assumes N = V = R.
                            FaceDirection(face, i, j, size, out float nx, out float ny, out float nz);
                            float r = 0f, g = 0f, b = 0f, totalWeight = 0f;

                            for (int s = 0; s < samples; s++)
                            {
                                Hammersley((uint)s, samples, out float xi0, out float xi1);
                                ImportanceSampleGGX(xi0, xi1, nx, ny, nz, roughness,
                                    out float hx, out float hy, out float hz);

                                float vDotH = nx * hx + ny * hy + nz * hz;
                                float lx = 2f * vDotH * hx - nx;
                                float ly = 2f * vDotH * hy - ny;
                                float lz = 2f * vDotH * hz - nz;
                                float nDotL = nx * lx + ny * ly + nz * lz;
                                if (nDotL <= 0f) continue;

                                SampleEnvironment(preset, lx, ly, lz, out float lr, out float lg, out float lb);
                                r += lr * nDotL; g += lg * nDotL; b += lb * nDotL;
                                totalWeight += nDotL;
                            }

                            if (totalWeight > 0f) { r /= totalWeight; g /= totalWeight; b /= totalWeight; }

                            int o = ((face * size + j) * size + i) * 3;
                            faces[o] = r; faces[o + 1] = g; faces[o + 2] = b;
                        }
                    }
                }
                mips[mip] = faces;
            }
            return mips;
        }

        // ── Split-sum BRDF LUT ───────────────────────────────────────────────────

        private static float[] BuildBrdfLut()
        {
            const int size = IblEnvironment.BrdfSize;
            const int samples = 64;
            var lut = new float[size * size * 2];

            for (int j = 0; j < size; j++)
            {
                float roughness = (j + 0.5f) / size;
                for (int i = 0; i < size; i++)
                {
                    float nDotV = (i + 0.5f) / size;
                    IntegrateBrdf(nDotV, roughness, samples, out float a, out float b);
                    int o = (j * size + i) * 2;
                    lut[o] = a; lut[o + 1] = b;
                }
            }
            return lut;
        }

        private static void IntegrateBrdf(float nDotV, float roughness, int samples,
            out float scale, out float bias)
        {
            // V in tangent space (N = +Z).
            float vx = (float)Math.Sqrt(Math.Max(0f, 1f - nDotV * nDotV));
            float vy = 0f, vz = nDotV;

            float a = 0f, b = 0f;
            for (int s = 0; s < samples; s++)
            {
                Hammersley((uint)s, samples, out float xi0, out float xi1);
                ImportanceSampleGGX(xi0, xi1, 0f, 0f, 1f, roughness,
                    out float hx, out float hy, out float hz);

                float vDotH = vx * hx + vy * hy + vz * hz;
                float lz = 2f * vDotH * hz - vz;     // L.z
                if (lz <= 0f) continue;

                float nDotL = lz;
                float nDotH = hz;
                float g = GeometrySmith(nDotV, nDotL, roughness);
                float gVis = (g * vDotH) / Math.Max(nDotH * nDotV, 1e-6f);
                float fc = (float)Math.Pow(1f - vDotH, 5f);
                a += (1f - fc) * gVis;
                b += fc * gVis;
            }

            scale = a / samples;
            bias = b / samples;
        }

        // ── Shared maths ─────────────────────────────────────────────────────────

        private static float GeometrySchlickGgx(float nDotV, float roughness)
        {
            float k = roughness * roughness * 0.5f;   // IBL remap
            return nDotV / (nDotV * (1f - k) + k);
        }

        private static float GeometrySmith(float nDotV, float nDotL, float roughness)
            => GeometrySchlickGgx(nDotV, roughness) * GeometrySchlickGgx(nDotL, roughness);

        private static void ImportanceSampleGGX(float xi0, float xi1,
            float nx, float ny, float nz, float roughness, out float hx, out float hy, out float hz)
        {
            float a = roughness * roughness;
            float phi = 2f * Pi * xi0;
            float cosTheta = (float)Math.Sqrt((1f - xi1) / (1f + (a * a - 1f) * xi1));
            float sinTheta = (float)Math.Sqrt(Math.Max(0f, 1f - cosTheta * cosTheta));

            float hxT = sinTheta * (float)Math.Cos(phi);
            float hyT = sinTheta * (float)Math.Sin(phi);
            float hzT = cosTheta;

            // Orthonormal basis around N (Gram-Schmidt).
            float upx = 0f, upy = 0f, upz = 0f;
            if (Math.Abs(nz) < 0.999f) upz = 1f; else upx = 1f;

            float tx = upy * nz - upz * ny;
            float ty = upz * nx - upx * nz;
            float tz = upx * ny - upy * nx;
            float tlen = (float)Math.Sqrt(tx * tx + ty * ty + tz * tz);
            if (tlen < 1e-6f) { tx = 1f; ty = 0f; tz = 0f; }
            else { tx /= tlen; ty /= tlen; tz /= tlen; }

            float bx = ny * tz - nz * ty;
            float by = nz * tx - nx * tz;
            float bz = nx * ty - ny * tx;

            hx = tx * hxT + bx * hyT + nx * hzT;
            hy = ty * hxT + by * hyT + ny * hzT;
            hz = tz * hxT + bz * hyT + nz * hzT;
        }

        private static void Hammersley(uint i, int n, out float x, out float y)
        {
            x = (float)i / n;
            y = RadicalInverseVdC(i);
        }

        private static float RadicalInverseVdC(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
            bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
            bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
            bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
            return bits * 2.3283064365386963e-10f;
        }
    }
}
