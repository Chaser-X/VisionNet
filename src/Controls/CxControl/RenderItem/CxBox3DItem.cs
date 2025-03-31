using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxBox3DItem : RenderAbstractItem
    {
        public Dictionary<Box3D, Color> BoxColors { get; private set; }
        public CxBox3DItem(Dictionary<Box3D, Color> boxColors)
        {
            this.BoxColors = boxColors ?? throw new ArgumentNullException(nameof(boxColors));
        }

        public override void Draw(OpenGL gl)
        {
            if (gl == null)
            {
                throw new ArgumentNullException(nameof(gl));
            }

            if (BoxColors == null || BoxColors.Count == 0)
            {
                return; // û�к�����Ҫ����
            }

            foreach (var boxColor in BoxColors)
            {
                var box = boxColor.Key;
                var color = boxColor.Value;

                float halfSizeX = box.Size.Width / 2;
                float halfSizeY = box.Size.Height / 2;
                float halfSizeZ = box.Size.Depth / 2;

                // ���ƺ��ӵ�������
           //     gl.Enable(OpenGL.GL_BLEND);
           //     gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
                gl.Color(color.R / 255.0, color.G / 255.0, color.B / 255.0, 0.2); // ��͸����ɫ

                gl.Begin(OpenGL.GL_QUADS);

                // ǰ��
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);

                // ����
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);

                gl.End();
            //    gl.Disable(OpenGL.GL_BLEND);

                // ���ƺ��ӵı�Ե
                gl.Color(color.R / 255.0, color.G / 255.0, color.B / 255.0, 1.0); // ��͸����ɫ
                gl.LineWidth(LineWidth);

                gl.Begin(OpenGL.GL_LINES);

                // ǰ��
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);

                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);

                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);

                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);

                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);

                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);

                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);

                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);

                // ����
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);

                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z - halfSizeZ);

                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y + halfSizeY, box.Center.Z + halfSizeZ);

                // ����
                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z - halfSizeZ);

                gl.Vertex(box.Center.X - halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);
                gl.Vertex(box.Center.X + halfSizeX, box.Center.Y - halfSizeY, box.Center.Z + halfSizeZ);

                gl.End();
            }
        }
    }
}

