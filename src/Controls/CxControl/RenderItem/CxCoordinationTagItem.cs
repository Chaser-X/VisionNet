using SharpGL;
using SharpGL.SceneGraph;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxCoordinationTagItem : AbstractRenderItem
    {
        public CxPoint3D Point { get; set; } = new CxPoint3D();
        public byte? Intensity { get; set; } = null; // ���ǿ��ֵ
        public bool Visible { get; set; } = false; // ���Ʊ�ǩ�Ƿ�ɼ�

        public Color TextColor { get; set; } = Color.White;

        public void SetCoordinates(CxPoint3D point, byte? intesity = null)
        {
            Point = point;
            Intensity = intesity;
        }

        public override void Draw(OpenGL gl)
        {
            if (!Visible) return; // �����ǩ���ɼ����򲻻���

            // ��3D����ת��Ϊ��Ļ���꣨���������Ϣ��
            var objCoord = new Vertex(Point.X, Point.Y, Point.Z);
            var screenCoord = gl.Project(objCoord);
            // �ж��Ƿ�����Ļ��Χ��
            if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
            {
                return;
            }
            // �����Ҫ����͸�ӷ�Χ�����Լ�� screenCoord.Z��������Ϊ��һ����ȣ�0��1��
            if (screenCoord.Z < 0 || screenCoord.Z > 1)
            {
                return;
            }

            int rectWidth = 80; // ���ο��
            int rectHeight = Intensity.HasValue ? 90 : 80; // ���θ߶�
            int startX = (int)screenCoord.X; // �������Ͻ�X����
            int startY = (int)screenCoord.Y - 10; // �������Ͻ�Y����

            //�ر���Ȳ���
            gl.Disable(OpenGL.GL_DEPTH_TEST);

            // ���浱ǰ����ģʽ�;���
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();

            // ��������ͶӰ
            int width = gl.RenderContextProvider.Width;
            int height = gl.RenderContextProvider.Height;
            gl.Ortho(0, width, 0, height, -1, 1);

            // �л���ģ����ͼ����
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            //���ư�ɫ�߿�İ�͸����ɫ���ο���
            // ���ð�͸����ɫ���
            gl.Color(0.0f, 0.0f, 0.0f, 0.5f); // RGBA����ɫ��50%͸��
            // ����������
            gl.Begin(OpenGL.GL_QUADS);
            gl.Vertex(startX, startY); // ���Ͻ�
            gl.Vertex(startX + rectWidth, startY); // ���Ͻ�
            gl.Vertex(startX + rectWidth, startY - rectHeight); // ���½�
            gl.Vertex(startX, startY - rectHeight); // ���½�
            gl.End();
            // ���ð�ɫ�߿�
            gl.LineWidth(1.0f); // �����߿�
            gl.Color(1.0f, 1.0f, 1.0f, 1.0f); // RGBA����ɫ����͸��
            // ���ƾ��α߿�
            gl.Begin(OpenGL.GL_LINE_LOOP);
            gl.Vertex(startX - 1, startY + 1); // ���Ͻ�
            gl.Vertex(startX + rectWidth + 1, startY + 1); // ���Ͻ�
            gl.Vertex(startX + rectWidth + 1, startY - rectHeight - 1); // ���½�
            gl.Vertex(startX - 1, startY - rectHeight - 1); // ���½�
            gl.End();

            int textOffsetX = 10; // �ı���Xƫ��
            int textOffsetY = 20; // �ı���Yƫ��
            var (R, G, B) = (TextColor.R / 255.0f, TextColor.G / 255.0f, TextColor.B / 255.0f);
            gl.DrawText(startX + textOffsetX, startY - textOffsetY, R, G, B, "Helvetica", 12, $"X: {Point.X:F3}");
            gl.DrawText(startX + textOffsetX, startY - textOffsetY * 2, R, B, B, "Helvetica", 12, $"Y: {Point.Y:F3}");
            gl.DrawText(startX + textOffsetX, startY - textOffsetY * 3, R, G, B, "Helvetica", 12, $"Z: {Point.Z:F3}");
            if (Intensity.HasValue)
                gl.DrawText(startX + textOffsetX, startY - textOffsetY * 4, R, G, B, "Helvetica", 12, $"I: {Intensity.Value}");

            // �ָ�ģ����ͼ����
            gl.PopMatrix();

            // �ָ�ͶӰ����
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();

            // �л���Ĭ�Ͼ���ģʽ
            gl.MatrixMode(OpenGL.GL_MODELVIEW);

            // �ָ���Ȳ���
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // �ͷ��й���Դ
            }
            // �ͷŷ��й���Դ
            base.Dispose(disposing);
        }
    }
}
