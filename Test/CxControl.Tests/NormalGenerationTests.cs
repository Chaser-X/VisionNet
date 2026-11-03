using System;
using VisionNet.DataType;
using Xunit;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// Pure-CPU tests for the P1 normal channel: the grid / triangle normal helpers and the
    /// per-item wiring that turns them into render data and the <c>useVertexNormal</c> uniform.
    /// No GL context is needed — everything asserted here is CPU-side state.
    /// </summary>
    public class NormalGenerationTests
    {
        private static void AssertUnit(float[] normals, int vertexIndex)
        {
            float x = normals[vertexIndex * 3];
            float y = normals[vertexIndex * 3 + 1];
            float z = normals[vertexIndex * 3 + 2];
            Assert.False(float.IsNaN(x) || float.IsInfinity(x));
            Assert.False(float.IsNaN(y) || float.IsInfinity(y));
            Assert.False(float.IsNaN(z) || float.IsInfinity(z));
            Assert.Equal(1f, (float)Math.Sqrt(x * x + y * y + z * z), 4);
        }

        [Fact]
        public void GridNormals_FlatSurface_PointStraightUp()
        {
            // 3x3 grid, every Z = 0 -> a plane in z = 0.
            var vertices = new float[9 * 3];
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int i = (y * 3 + x) * 3;
                    vertices[i] = x;
                    vertices[i + 1] = y;
                    vertices[i + 2] = 0f;
                }

            var normals = CxExtension.ComputeGridNormals(vertices, 3, 3);

            Assert.NotNull(normals);
            Assert.Equal(9 * 3, normals.Length);
            for (int v = 0; v < 9; v++)
            {
                AssertUnit(normals, v);
                Assert.Equal(0f, normals[v * 3], 4);
                Assert.Equal(0f, normals[v * 3 + 1], 4);
                Assert.Equal(1f, normals[v * 3 + 2], 4);
            }
        }

        [Fact]
        public void GridNormals_SlopedSurface_AreUnitLengthAndTiltBack()
        {
            // z = x: the plane rises along +X, so the normal tilts toward -X and +Z.
            var vertices = new float[3 * 3 * 3];
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int i = (y * 3 + x) * 3;
                    vertices[i] = x;
                    vertices[i + 1] = y;
                    vertices[i + 2] = x;
                }

            var normals = CxExtension.ComputeGridNormals(vertices, 3, 3);

            Assert.NotNull(normals);
            for (int v = 0; v < 9; v++)
            {
                AssertUnit(normals, v);
                Assert.True(normals[v * 3] < 0f, "normal should tilt toward -X");
                Assert.True(normals[v * 3 + 2] > 0f, "normal should point upward");
            }
        }

        [Fact]
        public void GridNormals_InvalidNeighbour_FallsBackWithoutNaN()
        {
            // 3x3 grid, only the centre point is no-data.
            var vertices = new float[9 * 3];
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int i = (y * 3 + x) * 3;
                    bool invalid = x == 1 && y == 1;
                    vertices[i] = x;
                    vertices[i + 1] = y;
                    vertices[i + 2] = invalid ? float.NegativeInfinity : 0f;
                }

            var normals = CxExtension.ComputeGridNormals(vertices, 3, 3);

            Assert.NotNull(normals);
            for (int v = 0; v < 9; v++)
                AssertUnit(normals, v);
        }

        [Fact]
        public void GridNormals_UnusableInput_ReturnsNull()
        {
            Assert.Null(CxExtension.ComputeGridNormals(null, 3, 3));
            Assert.Null(CxExtension.ComputeGridNormals(new float[3], 2, 2)); // too short
            Assert.Null(CxExtension.ComputeGridNormals(new float[27], 0, 3));
        }

        [Fact]
        public void TriangleNormals_Quad_PointsAlongZ()
        {
            // Unit quad in the z = 0 plane, wound counter-clockwise seen from +Z.
            var vertices = new float[]
            {
                0, 0, 0,
                1, 0, 0,
                1, 1, 0,
                0, 1, 0,
            };
            var indices = new uint[] { 0, 1, 2, 0, 2, 3 };

            var normals = CxExtension.ComputeTriangleNormals(vertices, indices);

            Assert.NotNull(normals);
            Assert.Equal(12, normals.Length);
            for (int v = 0; v < 4; v++)
            {
                AssertUnit(normals, v);
                Assert.Equal(0f, normals[v * 3], 4);
                Assert.Equal(0f, normals[v * 3 + 1], 4);
                Assert.Equal(1f, normals[v * 3 + 2], 4);
            }
        }

        [Fact]
        public void TriangleNormals_NoIndices_ReturnsNull()
        {
            Assert.Null(CxExtension.ComputeTriangleNormals(new float[9], null));
            Assert.Null(CxExtension.ComputeTriangleNormals(new float[9], new uint[0]));
        }

        private static CxSurface FlatSurface() =>
            new CxSurface(2, 2, new short[] { 0, 1, 2, 3 }, null, 0f, 0f, 0f, 1f, 1f, 1f);

        [Fact]
        public void SurfaceItem_MeshMode_BuildsNormalsAndEnablesTheUniform()
        {
            var item = new CxSurfaceItem(FlatSurface(), SurfaceMode.Mesh, SurfaceColorMode.Color);

            var data = item.PrepareRenderData();

            Assert.NotNull(data.Normals);
            Assert.Equal(data.VertexCount * 3, data.Normals.Length);
            Assert.Equal(1, (int)data.Uniforms["useVertexNormal"]);
        }

        [Fact]
        public void SurfaceItem_PointCloudMode_HasNoNormals()
        {
            var item = new CxSurfaceItem(FlatSurface(), SurfaceMode.PointCloud, SurfaceColorMode.Color);

            var data = item.PrepareRenderData();

            Assert.Null(data.Normals);
            Assert.Equal(0, (int)data.Uniforms["useVertexNormal"]);
        }

        private static CxMesh QuadMesh() => new CxMesh
        {
            Vertices = new[]
            {
                new CxPoint3D(0, 0, 0),
                new CxPoint3D(1, 0, 0),
                new CxPoint3D(1, 1, 0),
                new CxPoint3D(0, 1, 0),
            },
            Indices = new uint[] { 0, 1, 2, 0, 2, 3 },
        };

        [Fact]
        public void MeshItem_MeshMode_BuildsNormalsAndEnablesTheUniform()
        {
            var item = new CxMeshItem(QuadMesh(), SurfaceMode.Mesh, SurfaceColorMode.Color);

            var data = item.PrepareRenderData();

            Assert.NotNull(data.Normals);
            Assert.Equal(data.VertexCount * 3, data.Normals.Length);
            Assert.Equal(1, (int)data.Uniforms["useVertexNormal"]);
        }

        [Fact]
        public void MeshItem_PointCloudMode_HasNoNormals()
        {
            var item = new CxMeshItem(QuadMesh(), SurfaceMode.PointCloud, SurfaceColorMode.Color);

            var data = item.PrepareRenderData();

            Assert.Null(data.Normals);
            Assert.Equal(0, (int)data.Uniforms["useVertexNormal"]);
        }
    }
}
