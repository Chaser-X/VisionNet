using SharpGL;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxMeshItem : ICxObjRenderItem
    {
        public CxMesh Mesh { get; set; }
        private uint[] vboIds = new uint[2]; // ���ڴ洢�������ɫ�� VBO
        private bool vboInitialized = false;
        private bool meshUpdated = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        // ������BoundingBox ����
        public Box3D? BoundingBox { get; private set; }
        private SurfaceMode surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get
            {
                return surfaceMode;
            }
            set
            {
                meshUpdated = value != surfaceMode;
                surfaceMode = value;
            }
        }
        private SurfaceColorMode surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get
            {
                return surfaceColorMode;
            }
            set
            {
                meshUpdated = value != surfaceColorMode;
                surfaceColorMode = value;
            }
        }
        public CxMeshItem(CxMesh mesh, SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            this.Mesh = mesh;
            this.SurfaceMode = surfaceMode;
            this.SurfaceColorMode = surfaceColorMode;
            meshUpdated = true; // ���γ�ʼ��ʱ���Ϊ�Ѹ���
            BoundingBox = CalculateBoundingBox(); // ��ʼ��ʱ�����Χ��
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
            meshUpdated = false;
        }
        public void Draw(OpenGL gl)
        {
            if (Mesh == null || Mesh.Vertexs == null || Mesh.Vertexs.Length == 0 || Mesh.Indices == null || Mesh.Indices.Length == 0)
                return;

            if (vboInitialized && meshUpdated)
            {
                gl.DeleteBuffers(2, vboIds);
                vboInitialized = false;
            }

            if (!vboInitialized)
            {
                gl.GenBuffers(2, vboIds);
                vboInitialized = true;
                meshUpdated = true; // ���γ�ʼ��ʱ���Ϊ�Ѹ���
            }

            if (meshUpdated)
            {
                // ׼����������
                float[] vertices = new float[Mesh.Vertexs.Length * 3];
                float[] colors = new float[Mesh.Vertexs.Length * 3];
                for (int i = 0; i < Mesh.Vertexs.Length; i++)
                {
                    vertices[i * 3] = Mesh.Vertexs[i].X;
                    vertices[i * 3 + 1] = Mesh.Vertexs[i].Y;
                    vertices[i * 3 + 2] = Mesh.Vertexs[i].Z;

                    float intensity = 1;
                    if (Mesh.Intensity.Length == 0)
                    {
                        intensity = 1;
                    }
                    else
                    {
                        intensity = (float)Mesh.Intensity[i] / 255.0f; // ��������
                    }

                    if (SurfaceColorMode == SurfaceColorMode.Intensity)
                    {
                        colors[i * 3] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 1] = Math.Min(intensity, 1.0f);
                        colors[i * 3 + 2] = Math.Min(intensity, 1.0f);
                    }
                    else
                    {
                        var color = CxExtension.GetColorByHeight(Mesh.Vertexs[i].Z, ZMin, ZMax);
                        if (surfaceColorMode == SurfaceColorMode.Color)
                        {
                            intensity = 1;
                        }
                        colors[i * 3] = Math.Min(color.r * intensity, 1.0f);
                        colors[i * 3 + 1] = Math.Min(color.g * intensity, 1.0f);
                        colors[i * 3 + 2] = Math.Min(color.b * intensity, 1.0f);
                    }
                }

                // �ϴ��������ݵ� VBO
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices, OpenGL.GL_STATIC_DRAW);

                // �ϴ���ɫ���ݵ� VBO
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, colors, OpenGL.GL_STATIC_DRAW);

                meshUpdated = false; // ���ø��±��
            }

            // ���ö������ɫ����
            gl.EnableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.EnableClientState(OpenGL.GL_COLOR_ARRAY);

            // �󶨶�������
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
            gl.VertexPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);

            // ����ɫ����
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
            gl.ColorPointer(3, OpenGL.GL_FLOAT, 0, IntPtr.Zero);


            if (SurfaceMode == SurfaceMode.PointCloud)
                gl.DrawArrays(OpenGL.GL_POINTS, 0, Mesh.Vertexs.Length);
            else if (SurfaceMode == SurfaceMode.Mesh)
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, Mesh.Indices.Length, Mesh.Indices);
            }

            // ���ö������ɫ����
            gl.DisableClientState(OpenGL.GL_VERTEX_ARRAY);
            gl.DisableClientState(OpenGL.GL_COLOR_ARRAY);
        }
        // �����������Χ��
        private Box3D? CalculateBoundingBox()
        {
            if (Mesh == null || Mesh.Vertexs == null || Mesh.Vertexs.Length == 0)
                return null;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var point in Mesh.Vertexs)
            {
                if (float.IsInfinity(point.X) || float.IsInfinity(point.Y) || float.IsInfinity(point.Z))
                    continue;
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Z < minZ) minZ = point.Z;

                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
                if (point.Z > maxZ) maxZ = point.Z;
            }

            // �������ĵ�ͳߴ�
            var center = new CxPoint3D(
                (minX + maxX) / 2,
                (minY + maxY) / 2,
                (minZ + maxZ) / 2
            );

            var size = new CxSize3D(
                maxX - minX,
                maxY - minY,
                maxZ - minZ
            );

            return new Box3D(center, size);
        }
    }
}
