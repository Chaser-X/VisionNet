using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxSegment3DItem : RenderAbstractItem
    {
        public Dictionary<Segment3D, Color> SegmentColors { get; set; } = new Dictionary<Segment3D, Color>();
        public CxSegment3DItem(Dictionary<Segment3D, Color> segmentColors)
        {
            this.SegmentColors = segmentColors;
        }

        public override void Draw(OpenGL gl)
        {
            if (SegmentColors.Count == 0) return;

            gl.LineWidth(LineWidth);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var kvp in SegmentColors)
            {
                var segment = kvp.Key;
                var color = kvp.Value;

                gl.Color(color.R / 255.0, color.G /255.0, color.B / 255.0); // …Ë÷√—’…´
                gl.Vertex(segment.Start.X, segment.Start.Y, segment.Start.Z);
                gl.Vertex(segment.End.X, segment.End.Y, segment.End.Z);
            }
            gl.End();
        }
    }
}
