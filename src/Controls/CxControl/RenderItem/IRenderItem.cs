using System;
using System.Drawing;
using SharpGL;

namespace VisionNet.Controls
{
    public interface IRenderItem : IDisposable
    {
        Color Color { get; set; }
        float Size { get; set; }
        void Draw(OpenGL gL);
    }

    public abstract class AbstractRenderItem : IRenderItem
    {
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }

        public AbstractRenderItem() { }

        public AbstractRenderItem(Color color, float size = 1.0f)
        {
            Color = color;
            Size = size;
        }

        public float Size { get; set; } = 1.0f;
        public Color Color { get; set; } = Color.White;
        public abstract void Draw(OpenGL gL);
    }
}
