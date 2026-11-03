using System.Drawing;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pure-CPU tests for the state a ComplexGeometry item feeds to its shader: opacity,
    /// solid colour, point size, the colour-mode mapping and the PointCloud/Mesh render-data
    /// switch. No GL context is needed — everything asserted here is CPU-side state.
    /// </summary>
    public class ComplexGeometryItemTests
    {
        private static CxSurfaceItem NewItem(
            SurfaceMode mode = SurfaceMode.PointCloud,
            SurfaceColorMode colorMode = SurfaceColorMode.Color) =>
            new CxSurfaceItem(
                new CxSurface(2, 2, new short[] { 0, 1, 2, 3 }, null, 0f, 0f, 0f, 1f, 1f, 1f),
                mode, colorMode);

        [Fact]
        public void Opacity_IsClampedToUnitRange()
        {
            var item = NewItem();

            item.Opacity = 1.5f;
            Assert.Equal(1f, item.Opacity);

            item.Opacity = -0.5f;
            Assert.Equal(0f, item.Opacity);

            item.Opacity = 0.25f;
            Assert.Equal(0.25f, item.Opacity);
        }

        [Fact]
        public void Opacity_DefaultsToOpaqueAndFlowsIntoTheUniforms()
        {
            var item = NewItem();
            Assert.Equal(1f, item.Opacity);

            var data = item.PrepareRenderData();
            Assert.NotNull(data);
            Assert.Equal(1f, (float)data.Uniforms["opacity"]);

            // Changing it must update the cached uniforms without dropping the render data.
            item.Opacity = 0.4f;
            Assert.Same(data, item.PrepareRenderData());
            Assert.Equal(0.4f, (float)data.Uniforms["opacity"]);
        }

        [Fact]
        public void Opacity_NaNIsTreatedAsOpaque()
        {
            var item = NewItem();
            item.Opacity = 0.5f;

            // NaN must not reach the shader as an alpha value.
            item.Opacity = float.NaN;

            Assert.Equal(1f, item.Opacity);
            Assert.Equal(1f, (float)item.PrepareRenderData().Uniforms["opacity"]);
        }

        [Fact]
        public void SolidColor_DefaultsToTheLegacyLitGrey()
        {
            var item = NewItem();

            Assert.Equal(Color.FromArgb(217, 217, 217), item.SolidColor);

            var data = item.PrepareRenderData();
            Assert.Equal(Color.FromArgb(217, 217, 217), (Color)data.Uniforms["solidColor"]);
        }

        [Fact]
        public void SolidColor_ChangeUpdatesTheUniformInPlace()
        {
            var item = NewItem();
            var data = item.PrepareRenderData();

            item.SolidColor = Color.Red;

            Assert.Same(data, item.PrepareRenderData());
            Assert.Equal(Color.Red, (Color)data.Uniforms["solidColor"]);
        }

        [Fact]
        public void PointSize_DefaultsToThree()
        {
            Assert.Equal(3f, NewItem().PointSize);
        }

        [Fact]
        public void LitMode_IsSentAsMode4_EvenInPointCloudMode()
        {
            // Lit used to fall back to Color when the item was not a Mesh; it is now the flat
            // colour mode and applies to point clouds too.
            var item = NewItem(SurfaceMode.PointCloud, SurfaceColorMode.Lit);

            var data = item.PrepareRenderData();

            Assert.Equal((int)SurfaceColorMode.Lit, (int)data.Uniforms["colorMode"]);
            Assert.Equal(0, (int)data.Uniforms["surfaceMode"]);
        }

        [Fact]
        public void ColorModeChange_UpdatesTheUniformWithoutRebuilding()
        {
            var item = NewItem();
            var data = item.PrepareRenderData();
            Assert.Equal((int)SurfaceColorMode.Color, (int)data.Uniforms["colorMode"]);

            item.SurfaceColorMode = SurfaceColorMode.Intensity;

            Assert.Same(data, item.PrepareRenderData());
            Assert.Equal((int)SurfaceColorMode.Intensity, (int)data.Uniforms["colorMode"]);
        }

        [Fact]
        public void SwitchingToMesh_RebuildsIndexedRenderData()
        {
            var item = NewItem(SurfaceMode.PointCloud);
            var pointsOnly = item.PrepareRenderData();
            Assert.Equal(0, pointsOnly.IndexCount);

            item.SurfaceMode = SurfaceMode.Mesh;

            var mesh = item.PrepareRenderData();
            Assert.NotSame(pointsOnly, mesh);
            Assert.True(mesh.IndexCount > 0);
            Assert.NotNull(mesh.Indices);
            Assert.Equal(1, (int)mesh.Uniforms["surfaceMode"]);
        }

        [Fact]
        public void SwitchingBackToPointCloud_KeepsTheGeometryButDrawsPoints()
        {
            var item = NewItem(SurfaceMode.Mesh);
            var mesh = item.PrepareRenderData();
            Assert.True(mesh.IndexCount > 0);

            item.SurfaceMode = SurfaceMode.PointCloud;

            var back = item.PrepareRenderData();
            Assert.Same(mesh, back);
            Assert.Equal(0, (int)back.Uniforms["surfaceMode"]);
        }

        [Fact]
        public void BoundingBox_IsDerivedFromTheSourceData()
        {
            var item = NewItem();
            Assert.NotNull(item.BoundingBox);
            Assert.Equal(1f, item.BoundingBox.Value.Size.Width, 4);
            Assert.Equal(1f, item.BoundingBox.Value.Size.Height, 4);
        }
    }
}
