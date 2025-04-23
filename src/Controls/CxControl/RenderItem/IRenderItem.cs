using SharpGL;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public enum SurfaceMode
    {
        PointCloud = 1,
        Mesh = 2,
        //HeightMap = 4,
        //Intensity = 8,
    }
    public enum SurfaceColorMode
    {
        Color,
        Intensity,
        ColorWithIntensity,
    }

    public interface IRenderItem
    {
        Color Color { get; set; }
        float Size { get; set; }
        void Draw(OpenGL gL);
    }

    public abstract class AbstractRenderItem : IRenderItem
    {
        public AbstractRenderItem()
        {
        }
        public AbstractRenderItem(Color color, float size = 1.0f)
        {
            Color = color;
            Size = size;
        }
        public float Size { get; set; } = 1.0f;
        public Color Color { get; set; } = Color.White;
        public abstract void Draw(OpenGL gL);
    }

    //mesh surface pointcloud 等obj公用接口
    public interface ICxObjRenderItem
    {
        float ZMin { get; set; }
        float ZMax { get; set; }
        Box3D? BoundingBox { get;}
        SurfaceColorMode SurfaceColorMode { get; set; }
        SurfaceMode SurfaceMode { get; set; }
        void Draw(OpenGL gL);
    }

}
