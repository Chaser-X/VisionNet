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
        void Draw(OpenGL gL);
    }
}
 