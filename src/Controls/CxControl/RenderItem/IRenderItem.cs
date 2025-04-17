using SharpGL;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionNet.Controls
{
    public interface IRenderItem
    {
        Color Color { get; set; }
        float Size { get; set; }
        void Draw(OpenGL gL);
    }

    public abstract class RenderAbstractItem : IRenderItem
    {
        public RenderAbstractItem()
        {
        }
        public RenderAbstractItem(Color color, float size = 1.0f)
        {
            Color = color;
            Size = size;
        }
        public float Size { get; set; } = 1.0f;
        public Color Color { get; set; } = Color.White;

        public abstract void Draw(OpenGL gL);
    }
}
