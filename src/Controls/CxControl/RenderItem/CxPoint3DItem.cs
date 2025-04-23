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
            gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0); // 设置颜色
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
                    //使用sharpgl绘制球体
                    GLSphere.Sphere sphere = new GLSphere.Sphere();
                    sphere.Radius = Size / 2; // 球体半径
                    sphere.Slices =10 + 10 * (int)Size; // 球体的切片数
                    sphere.Stacks =10 + 10 * (int)Size; // 球体的堆叠数
                    gl.PushMatrix();
                    gl.Translate(point.X, point.Y, point.Z); // 移动到点的位置
                    sphere.CreateInContext(gl); // 创建球体
                    sphere.Render(gl, SharpGL.SceneGraph.Core.RenderMode.Render); // 绘制球体
                    sphere.DestroyInContext(gl); // 销毁球体
                    gl.PopMatrix();
                }
            }

        }
    }
}

