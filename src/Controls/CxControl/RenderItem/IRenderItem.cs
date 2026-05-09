using SharpGL;
using System.Drawing;
using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public enum SurfaceMode
    {
        PointCloud = 1,
        Mesh = 2,
    }

    public enum SurfaceColorMode
    {
        Color,
        Intensity,
        ColorWithIntensity,
    }

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

    /// <summary>
    /// 渲染数据容器（纯 CPU 数据）。
    /// CxDisplay 根据字段是否有值自动创建对应 GL 资源。
    /// </summary>
    public class RenderData
    {
        public float[] Vertices { get; set; }
        public float[] Colors { get; set; }
        public float[] UVCoords { get; set; }
        public uint[] Indices { get; set; }
        public int VertexCount { get; set; }
        public int IndexCount { get; set; }
        public TextureData TextureData { get; set; }
        public ShaderSource ShaderSource { get; set; }
        // 是否使用 VAO（高级模式）；简单模式用固定管线，不需要 VAO
        public bool UseVAO { get; set; }
        public Dictionary<string, object> Uniforms { get; set; } = new Dictionary<string, object>();
    }

    public class TextureData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Data { get; set; }
    }

    public class ShaderSource
    {
        public string VertexSource { get; set; }
        public string FragmentSource { get; set; }
    }

    /// <summary>
    /// GL 资源句柄，由 CxDisplay 持有和管理，Item 只读取。
    /// </summary>
    public class GLResourceHandle
    {
        public uint[] VboIds { get; set; } = new uint[3];
        public uint VaoId { get; set; }
        public uint ElementBufferId { get; set; }
        public uint ShaderProgram { get; set; }
        public uint TextureId { get; set; }
        public bool IsValid { get; set; }
        public bool NeedsUpdate { get; set; }
        public bool HasVAO { get; set; }
        public bool HasShader { get; set; }
        public bool HasTexture { get; set; }
        public bool HasEBO { get; set; }
        public int VboCount { get; set; }
        public bool UseUVMode { get; set; }
    }

    /// <summary>
    /// 主渲染对象统一接口。
    /// 职责分离：Item 管 CPU 数据，CxDisplay 管 GL 资源。
    /// </summary>
    public interface ICxObjRenderItem : IDisposable
    {
        event Action OnDisposed;
        /// <summary>
        /// 缓存数据失效时触发（例如 ColorMode 变化导致颜色数组需重算）。
        /// CxDisplay 收到后将对应 handle.NeedsUpdate 置 true。
        /// </summary>
        event Action OnRenderDataChanged;

        bool IsDisposed { get; }
        float ZMin { get; set; }
        float ZMax { get; set; }
        Box3D? BoundingBox { get; }
        SurfaceColorMode SurfaceColorMode { get; set; }
        SurfaceMode SurfaceMode { get; set; }

        /// <summary>准备 CPU 侧渲染数据，不涉及 GL 调用。</summary>
        RenderData PrepareRenderData();

        /// <summary>执行渲染，使用 CxDisplay 传入的已创建好的 GL 资源句柄。</summary>
        void Draw(OpenGL gl, GLResourceHandle handle);

        /// <summary>
        /// 由 CxDisplay 在每帧渲染前调用，传入所有 Item 合并后的全局 Z 范围。
        /// 使 Item 的颜色渲染与颜色条保持一致。
        /// </summary>
        void SetGlobalZRange(float zMin, float zMax);
    }
}
