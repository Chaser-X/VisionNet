using SharpGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionNet.Controls
{
    public interface IRenderItem
    {
        string ID { get; set; }
        float LineWidth { get; set; }   
        void Draw(OpenGL gL);
    }

    public abstract class RenderAbstractItem : IRenderItem
    {
        public string ID { get; set; }
        public float LineWidth { get; set; } = 1.0f;
        public abstract void Draw(OpenGL gL);
    }
}
 