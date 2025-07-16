using Lmi3d.GoSdk;
using Lmi3d.GoSdk.Messages;
using SharpGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using VisionNet;
using VisionNet.Controls;
using VisionNet.DataType;

namespace DemoFrom
{
    public partial class DemoFrom : Form
    {
        //private CxCamera camera;
        public DemoFrom()
        {
            InitializeComponent();
            GocatorHandle.GocatorHandle.Instance.Connect("127.0.0.1", false);
            GocatorHandle.GocatorHandle.Instance.SetDataHandler(onData);
            //camera = new CxCamera(openGLControl1);
            var state = CxExtension.IsOpenGLAvailable();
            var message = CxExtension.GetOpenGLVersion();
           // MessageBox.Show(message);
            if (!state)
            {
                //MessageBox.Show("OpenGL不可用，请检查您的系统配置。");
                return;
            }
        }
        CxSurface surface = null;
        private void onData(GoDataSet obj)
        {
            surface = null;
            GoDataMsg msg = null;
            GoSurfaceIntensityMsg surfaceIntensityMsg = null;
            for (UInt32 i = 0; i < obj.Count; i++)
            {
                GoDataMsg dataObj = (GoDataMsg)obj.Get(i);
                switch (dataObj.MessageType)
                {
                    case GoDataMessageType.UniformSurface:
                        {
                            msg = dataObj;
                        }
                        break;
                    case GoDataMessageType.SurfaceIntensity:
                        {
                            surfaceIntensityMsg = (GoSurfaceIntensityMsg)dataObj;
                        }
                        break;
                    case GoDataMessageType.SurfacePointCloud:
                        {
                            msg = (GoSurfacePointCloudMsg)dataObj;
                        }
                        break;
                }
            }
            //surfaceMsg和surfaceIntensityMsg都获取到了，转换到点云数据
            if (msg != null)
            {
                if (msg.MessageType == GoDataMessageType.UniformSurface)
                {
                    var surfaceMsg = msg as GoUniformSurfaceMsg;
                    long width = surfaceMsg.Width;
                    long length = surfaceMsg.Length;
                    long bufferSize = width * length;
                    float xoffset = surfaceMsg.XOffset / 1000.0f;
                    float yoffset = surfaceMsg.YOffset / 1000.0f;
                    float zoffset = surfaceMsg.ZOffset / 1000.0f;
                    float xscale = surfaceMsg.XResolution / 1000000.0f;
                    float yscale = surfaceMsg.YResolution / 1000000.0f;
                    float zscale = surfaceMsg.ZResolution / 1000000.0f;
                    IntPtr surfacePtr = surfaceMsg.Data;
                    surface = new CxSurface((int)width, (int)length, new short[bufferSize], new byte[0], xoffset, yoffset, zoffset, xscale, yscale, zscale, SurfaceType.Surface);
                    surface.SetData(surfacePtr);
                    if (surfaceIntensityMsg != null)
                    {
                        IntPtr intensityPtr = surfaceIntensityMsg.Data;
                        surface.SetInetnsity(intensityPtr);
                    }
                }
                else
                {
                    var surfaceMsg = msg as GoSurfacePointCloudMsg;
                    long width = surfaceMsg.Width;
                    long length = surfaceMsg.Length; 
                    long bufferSize = width * length;
                    float xoffset = surfaceMsg.XOffset / 1000.0f;
                    float yoffset = surfaceMsg.YOffset / 1000.0f;
                    float zoffset = surfaceMsg.ZOffset / 1000.0f;
                    float xscale = surfaceMsg.XResolution / 1000000.0f;
                    float yscale = surfaceMsg.YResolution / 1000000.0f;
                    float zscale = surfaceMsg.ZResolution / 1000000.0f;
                    IntPtr surfacePtr = surfaceMsg.Data;
                    surface = new CxSurface((int)width, (int)length, new short[bufferSize * 3], new byte[0], xoffset, yoffset, zoffset, xscale, yscale, zscale, SurfaceType.PointCloud);
                    surface.SetData(surfacePtr);
                    if (surfaceIntensityMsg != null)
                    {
                        IntPtr intensityPtr = surfaceIntensityMsg.Data;
                        surface.SetInetnsity(intensityPtr);
                    }
                }
            }
            cxDisplay1.ResetView();
            //cxDisplay1.SurfaceMode = SurfaceMode.Mesh;
            //cxDisplay1.SurfaceColorMode = SurfaceColorMode.Intensity;
            cxDisplay1.SetSurfaceAdvancedItem(surface);
            ////添加平面 Plane3D
            //var plane = new Plane3D(new CxPoint3D(0, 0, 0), new CxVector3D(1, 1, 1));
            //cxDisplay1.SetPlane(plane, Color.FromArgb(100, Color.Blue));

            //添加Box3D
            var box = new Box3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 10));
            //cxDisplay1.SetBox(new Box3D[] { box }, Color.FromArgb(100, Color.Yellow), 1);

            //cxDisplay1.SetPoint(new CxPoint3D[] { new CxPoint3D(2, 3, 1), new CxPoint3D(5, 1, 1) }, Color.Green, 10f, PointShape.Sphere);

            //添加Box3D
            cxDisplay2.ResetView();
            cxDisplay2.SetBox(new Box3D[] { box }, Color.Yellow, 10);
        }

        private void DemoFrom_FormClosing(object sender, FormClosingEventArgs e)
        {
            GocatorHandle.GocatorHandle.Instance.DisConnect();
            cxDisplay1.Dispose();
            cxDisplay2.Dispose();
            //  Environment.Exit(0);
        }

        private void btn_test_Click(object sender, EventArgs e)
        {
            List<CxPoint3D> pts = new List<CxPoint3D>();
            pts.Add(new CxPoint3D());
            pts.Add(new CxPoint3D(1, 1, 2));
            var p = VisionOperator.GetPoint3DArrayCenter(pts);

            if (surface == null) return;
            var points = surface.ToPoints();
            var heights = new float[points.Length];
            var intensitys = new byte[points.Length];
            var surfacemap = VisionOperator.UniformSuface(points, surface.Intensity, 200, 3500,
                0.1f, 0.1f, surface.ZScale, -10, -175, surface.ZOffset);

            cxDisplay1.SetPointCloud(surfacemap);
        }

        private void btn_addSeg3D_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            //添加Segment3D线段
            cxDisplay2.SetSegment(new Segment3D[] { new Segment3D(new CxPoint3D(0, 0, 0), new CxPoint3D(1, 1, 1)) }, Color.Red);
            cxDisplay2.SetSegment(new Segment3D[] { new Segment3D(new CxPoint3D(0, 0, 0), new CxPoint3D(0, 1, 1)) }, Color.Yellow);

            cxDisplay2.SetPoint(new CxPoint3D[] { new CxPoint3D(2, 3, 1), new CxPoint3D(5, 1, 1) }, Color.Green, 1f, PointShape.Sphere);
            cxDisplay2.SetCoordinate3DSystem(new CxCoordination3D() {
                Origin = new CxPoint3D(5, 5 ,5),
                XAxis = new CxVector3D(1, 0, 0),
                YAxis = new CxVector3D(0, 1, 0),
                ZAxis = new CxVector3D(0, 0, -1)
            },50);
            //添加多边形
            List<CxPoint3D> pts = new List<CxPoint3D>();
            pts.Add(new CxPoint3D(0, 0, 0));
            pts.Add(new CxPoint3D(1, 0, 0));
            pts.Add(new CxPoint3D(1, 1, 0));
            pts.Add(new CxPoint3D(1, 1, 1));
            var polygon = new Polygon3D(pts.ToArray(), false);
            cxDisplay2.SetPolygon(new Polygon3D[] { polygon }, Color.Blue);

            //添加平面 Plane3D
            var plane = new Plane3D(new CxPoint3D(0, 0, 0), new CxVector3D(1, 1, 1));
            cxDisplay2.SetPlane(new Plane3D[] { plane }, Color.FromArgb(100, Color.Red));

            //添加Box3D
            var box = new Box3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 10));
            cxDisplay2.SetBox(new Box3D[] { box }, Color.FromArgb(100, Color.Yellow));

            //添加TextInfo
            var text = new TextInfo(new CxPoint3D(0, 0, 0), "this is a test中文!", 50);
            cxDisplay2.SetTextInfo(new TextInfo[] { text }, Color.Yellow);

            //添加Text2D
            var text2d = new Text2D(new CxPoint2D(10, 50), "2D Labels", 10);
            cxDisplay2.SetText2D(new Text2D[] { text2d }, Color.Green);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            cxDisplay1.ResetView();
        }
    }
}