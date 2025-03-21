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
        private CxCamera camera;
        public DemoFrom()
        {
            InitializeComponent();
            GocatorHandle.GocatorHandle.Instance.Connect("127.0.0.1", false);
            GocatorHandle.GocatorHandle.Instance.SetDataHandler(onData);
            camera = new CxCamera(openGLControl1);
        }
        CxSuface surface = null;
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
                    surface = new CxSuface((int)width, (int)length, new short[bufferSize], new byte[0], xoffset, yoffset, zoffset, xscale, yscale, zscale, SurfaceType.Surface);
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
                    surface = new CxSuface((int)width, (int)length, new short[bufferSize * 3], new byte[0], xoffset, yoffset, zoffset, xscale, yscale, zscale, SurfaceType.PointCloud);
                    surface.SetData(surfacePtr);
                    if (surfaceIntensityMsg != null)
                    {
                        IntPtr intensityPtr = surfaceIntensityMsg.Data;
                        surface.SetInetnsity(intensityPtr);
                    }
                }
            }
            camera.SufaceMode = SufaceMode.Mesh;
            camera.SetPointCloud(surface);
        }

        private void DemoFrom_FormClosing(object sender, FormClosingEventArgs e)
        {
            GocatorHandle.GocatorHandle.Instance.DisConnect();
            openGLControl1.Dispose();
            Environment.Exit(0);
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
            var surfacemap = VisionOperator.UniformSuface(points, null,500,750,
                0.5f, 0.5f, surface.ZScale, -50, -75, surface.ZOffset);

            camera.SetPointCloud(surfacemap);
            //var points = new CxPoint3D[]
            //{
            //    new CxPoint3D { X = 1.0f, Y = 1.0f, Z = 2.0f },
            //    new CxPoint3D { X = 2.0f, Y = 2.0f, Z = 3.0f }
            //};

            //var intensitys = new byte[]
            //{
            //100, 150
            //};

            //int size = points.Length;
            //float xScale = 1.0f;
            //float yScale = 1.0f;
            //float xMin = 0.0f;
            //float xMax = 10.0f;
            //float yMin = 0.0f;
            //float yMax = 10.0f;

            //float[] heightMap = new float[size];
            //byte[] intensityMap = new byte[size];

            //VisionOperator.UniformGridSample(points, intensitys, size, xScale, yScale, xMin, xMax, yMin, yMax, heightMap, intensityMap);

            //for (int i = 0; i < size; i++)
            //{
            //    Console.WriteLine($"Height: {heightMap[i]}, Intensity: {intensityMap[i]}");
            //}
        }
    }
}