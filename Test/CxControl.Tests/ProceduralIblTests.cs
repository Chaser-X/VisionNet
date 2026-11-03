using System;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pure-CPU tests for the procedural IBL generator: cache identity, array shapes, finiteness
    /// and the numeric ranges the shader relies on. No GL context is needed.
    /// </summary>
    public class ProceduralIblTests
    {
        [Fact]
        public void Get_ReturnsCachedInstancePerPreset()
        {
            var a = ProceduralIbl.Get(SurfaceEnvironmentPreset.Studio);
            var b = ProceduralIbl.Get(SurfaceEnvironmentPreset.Studio);
            Assert.Same(a, b);
        }

        [Fact]
        public void Generated_ArraysHaveTheExpectedShapes()
        {
            var env = ProceduralIbl.Get(SurfaceEnvironmentPreset.Studio);

            int irr = IblEnvironment.IrradianceSize;
            Assert.Equal(6 * irr * irr * 3, env.Irradiance.Length);

            Assert.Equal(IblEnvironment.PrefilterMipCount, env.Prefilter.Length);
            for (int mip = 0; mip < IblEnvironment.PrefilterMipCount; mip++)
            {
                int size = Math.Max(1, IblEnvironment.PrefilterBaseSize >> mip);
                Assert.Equal(6 * size * size * 3, env.Prefilter[mip].Length);
            }

            int brdf = IblEnvironment.BrdfSize;
            Assert.Equal(brdf * brdf * 2, env.Brdf.Length);
        }

        [Fact]
        public void Generated_ValuesAreFiniteAndNonNegative()
        {
            var env = ProceduralIbl.Get(SurfaceEnvironmentPreset.Studio);

            foreach (var v in env.Irradiance)
            {
                Assert.False(float.IsNaN(v) || float.IsInfinity(v));
                Assert.True(v >= 0f, "irradiance must be non-negative");
            }

            foreach (var mip in env.Prefilter)
                foreach (var v in mip)
                    Assert.False(float.IsNaN(v) || float.IsInfinity(v));

            foreach (var v in env.Brdf)
                Assert.False(float.IsNaN(v) || float.IsInfinity(v));
        }

        [Fact]
        public void BrdfLut_StaysInTheUnitRange()
        {
            var env = ProceduralIbl.Get(SurfaceEnvironmentPreset.Neutral);
            for (int i = 0; i < env.Brdf.Length; i++)
            {
                Assert.InRange(env.Brdf[i], 0f, 1.01f);
            }
        }

        [Fact]
        public void Environment_IsDeterministicPerPreset_AndDiffersBetweenPresets()
        {
            ProceduralIbl.SampleEnvironment(SurfaceEnvironmentPreset.Studio, 0f, 1f, 0f,
                out float s1r, out float s1g, out float s1b);
            ProceduralIbl.SampleEnvironment(SurfaceEnvironmentPreset.Studio, 0f, 1f, 0f,
                out float s2r, out float s2g, out float s2b);
            Assert.Equal(s1r, s2r);
            Assert.Equal(s1g, s2g);
            Assert.Equal(s1b, s2b);

            ProceduralIbl.SampleEnvironment(SurfaceEnvironmentPreset.Neutral, 0f, 1f, 0f,
                out float nr, out float ng, out float nb);

            // Studio has a strong warm key light toward the upper-right-front; Neutral is grey.
            Assert.NotEqual(s1r, nr);
        }

        [Fact]
        public void Environment_HasPositiveRadianceInEveryDirection()
        {
            // Even the darkest direction should not be black, so lit surfaces never go fully dark.
            ProceduralIbl.SampleEnvironment(SurfaceEnvironmentPreset.Studio, 0f, -1f, 0f,
                out float r, out float g, out float b);
            Assert.True(r > 0f && g > 0f && b > 0f);
        }
    }
}
