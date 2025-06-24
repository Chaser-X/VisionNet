using SharpGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public enum ViewMode
    {
        None,
        Top,
        Front,
        Left,
        Right,
    }
    public interface ICamera : IDisposable
    {
        ViewMode ViewMode { get; set; }
        bool Enable2DView { get; set; }
        CxPoint3D? RotationPoint { get; set; }

        void FitView(Box3D? viewBox);
        void LookAtMatrix(OpenGL gl);
    }
}
