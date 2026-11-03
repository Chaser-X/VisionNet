using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pure-CPU tests for the PBR material parameters on a ComplexGeometry item: defaults,
    /// clamping and the fact that changing them only updates the cached uniforms.
    /// </summary>
    public class PbrMaterialTests
    {
        private static CxSurfaceItem NewItem() =>
            new CxSurfaceItem(
                new CxSurface(2, 2, new short[] { 0, 1, 2, 3 }, null, 0f, 0f, 0f, 1f, 1f, 1f),
                SurfaceMode.Mesh, SurfaceColorMode.Color);

        [Fact]
        public void Defaults_AreLowMetallicAndMidRough()
        {
            var item = NewItem();
            Assert.Equal(0.2f, item.Metallic);
            Assert.Equal(0.5f, item.Roughness);

            var data = item.PrepareRenderData();
            Assert.Equal(0.2f, (float)data.Uniforms["metallic"]);
            Assert.Equal(0.5f, (float)data.Uniforms["roughness"]);
        }

        [Fact]
        public void Values_AreClampedToUnitRange()
        {
            var item = NewItem();

            item.Metallic = 2f;
            item.Roughness = -1f;
            Assert.Equal(1f, item.Metallic);
            Assert.Equal(0f, item.Roughness);

            item.Metallic = float.NaN;
            item.Roughness = float.NaN;
            Assert.Equal(0f, item.Metallic);
            Assert.Equal(0.5f, item.Roughness);
        }

        [Fact]
        public void Change_UpdatesTheUniformInPlace_WithoutRebuilding()
        {
            var item = NewItem();
            var data = item.PrepareRenderData();

            item.Metallic = 0.8f;
            item.Roughness = 0.2f;

            Assert.Same(data, item.PrepareRenderData());
            Assert.Equal(0.8f, (float)data.Uniforms["metallic"]);
            Assert.Equal(0.2f, (float)data.Uniforms["roughness"]);
        }

        [Fact]
        public void SurfaceLightingMode_HasStableValues()
        {
            Assert.Equal(0, (int)SurfaceLightingMode.Legacy);
            Assert.Equal(1, (int)SurfaceLightingMode.Pbr);
        }
    }
}
