using System;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public interface ICxObjRenderItem : IDisposable
    {
        event Action OnDisposed;
        event Action OnRenderDataChanged;

        bool IsDisposed { get; }
        float ZMin { get; set; }
        float ZMax { get; set; }
        Box3D? BoundingBox { get; }
        SurfaceColorMode SurfaceColorMode { get; set; }
        SurfaceMode SurfaceMode { get; set; }

        RenderData PrepareRenderData();

        void Draw(OpenGL gl, GLResourceHandle handle);

        void SetGlobalZRange(float zMin, float zMax);
    }
}
