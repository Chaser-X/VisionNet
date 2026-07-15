using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="CxSegment3D"/> values as GL_LINES with a configurable line width.
    /// </summary>
    public class CxSegment3DItem : AbstractRenderItem
    {
        /// <summary>Gets the line segments to be rendered.</summary>
        public CxSegment3D[] Segment3Ds { get; private set; }

        /// <summary>Initializes the item with the given segments, colour, and line width.</summary>
        /// <param name="segments">World-space line segments.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels.</param>
        public CxSegment3DItem(CxSegment3D[] segments, Color color, float size = 1.0f) : base(color, size)
        {
            Segment3Ds = segments;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Segment3Ds == null || Segment3Ds.Length == 0) return;

            gl.Color(Color.R / 255.0, Color.G / 255.0, Color.B / 255.0);
            gl.LineWidth(Size);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var seg in Segment3Ds)
            {
                gl.Vertex(seg.Start.X, seg.Start.Y, seg.Start.Z);
                gl.Vertex(seg.End.X,   seg.End.Y,   seg.End.Z);
            }
            gl.End();
        }
    }
}
