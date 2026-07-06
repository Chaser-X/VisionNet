using SharpGL;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders an array of <see cref="Segment3D"/> values as GL_LINES with a configurable line width.
    /// </summary>
    public class CxSegment3DItem : AbstractRenderItem
    {
        /// <summary>Gets the line segments to be rendered.</summary>
        public Segment3D[] Segment3Ds { get; private set; }

        /// <summary>Initializes the item with the given segments, colour, and line width.</summary>
        /// <param name="segments">World-space line segments.</param>
        /// <param name="color">Line colour.</param>
        /// <param name="size">Line width in pixels.</param>
        public CxSegment3DItem(Segment3D[] segments, Color color, float size = 1.0f) : base(color, size)
        {
            Segment3Ds = segments;
        }

        /// <inheritdoc/>
        public override void Draw(OpenGL gl)
        {
            if (Segment3Ds == null || Segment3Ds.Length == 0) return;

            gl.Color(DrawColor.R / 255.0, DrawColor.G / 255.0, DrawColor.B / 255.0);
            gl.LineWidth(Size);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var seg in Segment3Ds)
            {
                gl.Vertex(seg.Start.X, seg.Start.Y, seg.Start.Z);
                gl.Vertex(seg.End.X,   seg.End.Y,   seg.End.Z);
            }
            gl.End();
        }

        /// <inheritdoc/>
        public override bool HitTest(CxPoint3D worldPos)
        {
            if (Segment3Ds == null) return false;
            float t2 = HitThreshold * HitThreshold;
            foreach (var seg in Segment3Ds)
            {
                float dxS = seg.Start.X - worldPos.X, dyS = seg.Start.Y - worldPos.Y, dzS = seg.Start.Z - worldPos.Z;
                float dxE = seg.End.X   - worldPos.X, dyE = seg.End.Y   - worldPos.Y, dzE = seg.End.Z   - worldPos.Z;
                if (dxS * dxS + dyS * dyS + dzS * dzS <= t2) return true;
                if (dxE * dxE + dyE * dyE + dzE * dzE <= t2) return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override void Translate(double dx, double dy, double dz)
        {
            if (Segment3Ds == null) return;
            for (int i = 0; i < Segment3Ds.Length; i++)
                Segment3Ds[i] = new Segment3D(
                    new CxPoint3D((float)(Segment3Ds[i].Start.X + dx), (float)(Segment3Ds[i].Start.Y + dy), (float)(Segment3Ds[i].Start.Z + dz)),
                    new CxPoint3D((float)(Segment3Ds[i].End.X   + dx), (float)(Segment3Ds[i].End.Y   + dy), (float)(Segment3Ds[i].End.Z   + dz)));
        }
    }
}
