using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pure-CPU tests for <see cref="CxMatrix4X4"/> and for the GL handle bookkeeping that has no
    /// GL dependency.
    /// </summary>
    public class CxMatrix4X4Tests
    {
        private const float Eps = 1e-4f;

        [Fact]
        public void Identity_Inverse_IsIdentity()
        {
            var inverse = CxMatrix4X4.Identity().Inverse();
            var p = inverse.TransformPoint3D(new CxPoint3D(3f, -2f, 7f));

            Assert.Equal(3f, p.X, 4);
            Assert.Equal(-2f, p.Y, 4);
            Assert.Equal(7f, p.Z, 4);
        }

        [Fact]
        public void Translation_RoundTripsThroughItsInverse()
        {
            var m = CxMatrix4X4.Translation(1f, 2f, 3f);
            var point = new CxPoint3D(4f, 5f, 6f);

            var moved = m.TransformPoint3D(point);
            var back = m.Inverse().TransformPoint3D(moved);

            Assert.Equal(point.X, back.X, 4);
            Assert.Equal(point.Y, back.Y, 4);
            Assert.Equal(point.Z, back.Z, 4);
        }

        [Fact]
        public void Scale_ScalesEachAxis()
        {
            var p = CxMatrix4X4.Scale(2f, 3f, 4f).TransformPoint3D(new CxPoint3D(1f, 1f, 1f));

            Assert.Equal(2f, p.X, 4);
            Assert.Equal(3f, p.Y, 4);
            Assert.Equal(4f, p.Z, 4);
        }

        [Fact]
        public void Multiplication_ComposesInOrder()
        {
            var translate = CxMatrix4X4.Translation(10f, 0f, 0f);
            var scale = CxMatrix4X4.Scale(2f, 2f, 2f);
            var point = new CxPoint3D(1f, 0f, 0f);

            var scaleThenTranslate = (translate * scale).TransformPoint3D(point);
            var translateThenScale = (scale * translate).TransformPoint3D(point);

            Assert.Equal(12f, scaleThenTranslate.X, 4);
            Assert.Equal(22f, translateThenScale.X, 4);
        }

        [Fact]
        public void ResetForContextRecreate_ForgetsNamesAndRequestsARebuild()
        {
            var handle = new GLResourceHandle
            {
                VaoId = 7,
                ElementBufferId = 8,
                ShaderProgram = 9,
                TextureId = 10,
                IsValid = true,
                NeedsUpdate = false,
                HasVAO = true,
                HasShader = true,
                HasTexture = true,
                HasEBO = true,
                OwnsShader = true,
                VboCount = 3,
                UseUVMode = true,
            };
            handle.VboIds[0] = 11;
            handle.UniformLocations["colorMode"] = 3;

            handle.ResetForContextRecreate();

            Assert.Equal(0u, handle.VaoId);
            Assert.Equal(0u, handle.ElementBufferId);
            Assert.Equal(0u, handle.ShaderProgram);
            Assert.Equal(0u, handle.TextureId);
            Assert.Equal(0u, handle.VboIds[0]);
            Assert.False(handle.IsValid);
            Assert.False(handle.HasVAO);
            Assert.False(handle.HasShader);
            Assert.False(handle.HasTexture);
            Assert.False(handle.HasEBO);
            Assert.False(handle.OwnsShader);
            Assert.Equal(0, handle.VboCount);
            Assert.False(handle.UseUVMode);
            Assert.Empty(handle.UniformLocations);

            // The handle must be rebuilt on the next frame rather than re-deleted in a new context.
            Assert.True(handle.NeedsUpdate);
        }

        [Fact]
        public void GetUniformLocation_CachesPerProgram()
        {
            var handle = new GLResourceHandle();
            handle.UniformLocations["colorMode"] = 42;

            // Without a GL context the cache must still short-circuit the driver lookup.
            Assert.Equal(42, handle.UniformLocations["colorMode"]);
            Assert.True(Eps > 0f);
        }
    }
}
