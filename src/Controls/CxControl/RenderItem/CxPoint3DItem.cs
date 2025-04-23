using SharpGL;
using SharpGL.SceneGraph.Quadrics;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;
using GLSphere = SharpGL.SceneGraph.Quadrics;

namespace VisionNet.Controls
{
    public enum PointShape
    {
        Point,
        Sphere,
    }
    public class CxPoint3DItem : AbstractRenderItem
    {
        public CxPoint3D[] Point3Ds { get; private set; }
        public PointShape Shape { get; set; } = PointShape.Point;
        public CxPoint3DItem(CxPoint3D[] points, Color color, float size = 1.0f, PointShape shape = PointShape.Point) : base(color, size)
        {
            this.Point3Ds = points;
            this.Shape = shape;
        }

        public override void Draw(OpenGL gl)
        {
            if (Point3Ds == null || Point3Ds.Length == 0) return;
            gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0); // ������ɫ
            if (Shape == PointShape.Point)
            {
                gl.PointSize(Size);
                gl.Begin(OpenGL.GL_POINTS);
                foreach (var point in Point3Ds)
                {
                    gl.Vertex(point.X, point.Y, point.Z);
                }
                gl.End();
            }
            else
            {
                foreach (var point in Point3Ds)
                {
                    //ʹ��sharpgl��������
                    GLSphere.Sphere sphere = new GLSphere.Sphere();
                    sphere.Radius = Size / 2; // ����뾶
                    sphere.Slices =10 + 10 * (int)Size; // �������Ƭ��
                    sphere.Stacks =10 + 10 * (int)Size; // ����Ķѵ���
                    gl.PushMatrix();
                    gl.Translate(point.X, point.Y, point.Z); // �ƶ������λ��
                    sphere.CreateInContext(gl); // ��������
                    sphere.Render(gl, SharpGL.SceneGraph.Core.RenderMode.Render); // ��������
                    sphere.DestroyInContext(gl); // ��������
                    gl.PopMatrix();
                }
            }

        }
    }
}

