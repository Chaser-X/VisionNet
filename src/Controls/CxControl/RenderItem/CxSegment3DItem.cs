using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxSegment3DItem : RenderAbstractItem
    {
        public Segment3D[] Segment3Ds { get; private set; }
        public CxSegment3DItem(Segment3D[] segments, Color color, float size = 1.0f) : base(color, size)
        {
            this.Segment3Ds = segments;
        }
        public override void Draw(OpenGL gl)
        {
            if (Segment3Ds == null || Segment3Ds.Length == 0) return;

            gl.LineWidth(Size);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var segment in Segment3Ds)
            {
                gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0); // …Ë÷√—’…´
                gl.Vertex(segment.Start.X, segment.Start.Y, segment.Start.Z);
                gl.Vertex(segment.End.X, segment.End.Y, segment.End.Z);
            }
            gl.End();
        }
    }
}
