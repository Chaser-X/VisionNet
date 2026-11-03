using Lmi3d.GoSdk;
using Lmi3d.GoSdk.Messages;
using SharpGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
            Build2DTab(tabPage2D);
            GocatorHandle.GocatorHandle.Instance.Connect("127.0.0.1", false);
            GocatorHandle.GocatorHandle.Instance.SetDataHandler(onData);
            //camera = new CxCamera(openGLControl1);
            var state = CxExtension.IsOpenGLAvailable();
            var message = CxExtension.GetOpenGLVersion();
            // MessageBox.Show(message);
            if (!state)
            {
                //MessageBox.Show("OpenGL�����ã���������ϵͳ���á�");
                return;
            }
            VisionOperator.InitialLib();
            // cxDisplay1.SetViewUpDirection(new CxVector3D(0, 1, 0));
            cxDisplay1.Camera.IsLeftHanded = true;
            cxDisplay1.Camera.Enable2DView = false;
            BuildGroupSimButton();
            BuildLightingTab();
        }
        CxSurface surface = null;
        CxSurface surface2 = null;
        private CxPointCloud _pointCloud;
        private CxMesh _currentMesh;

        private void onData(GoDataSet obj)
        {
            surface = null;
            surface2 = null;
            _pointCloud = null;

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
            //surfaceMsg��surfaceIntensityMsg����ȡ���ˣ�ת������������
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
                    surface = new CxSurface((int)width, (int)length, new short[bufferSize], new byte[0], xoffset, yoffset, zoffset, xscale, yscale, zscale);
                    surface.SetData(surfacePtr);
                    if (surfaceIntensityMsg != null)
                    {
                        IntPtr intensityPtr = surfaceIntensityMsg.Data;
                        surface.SetIntensity(intensityPtr);
                    }

                    IntPtr surfacePtr2 = surfaceMsg.Data;
                    surface2 = new CxSurface((int)width, (int)length, new short[bufferSize], new byte[0], xoffset + 10, yoffset, zoffset + 10, xscale, yscale, zscale);
                    surface2.SetData(surfacePtr2);
                    if (surfaceIntensityMsg != null)
                    {
                        IntPtr intensityPtr2 = surfaceIntensityMsg.Data;
                        surface2.SetIntensity(intensityPtr2);
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

                    //int size = (int)(width * length * 3);
                    //var Data = new short[size];
                    //Marshal.Copy(surfacePtr, Data, 0, size);

                    //var points = new short[width / 2 * length * 3];
                    //for (int i = 0; i < length; i++)
                    //{
                    //    for (int j = 0; j < width / 2; j++)
                    //    {
                    //        var index = i * width + j + width / 2;

                    //        points[(i * width / 2 + j) * 3] = Data[index * 3];
                    //        points[(i * width / 2 + j) * 3 + 1] = Data[index * 3 + 1];
                    //        points[(i * width / 2 + j) * 3 + 2] = Data[index * 3 + 2];
                    //    }
                    //}
                    _pointCloud = new CxPointCloud((int)width, (int)length, new short[bufferSize * 3], new byte[0], xoffset, yoffset, zoffset, xscale, yscale, zscale);
                    _pointCloud.SetData(surfacePtr);

                    if (surfaceIntensityMsg != null)
                    {
                        IntPtr intensityPtr = surfaceIntensityMsg.Data;
                        _pointCloud.SetIntensity(intensityPtr);
                    }
                }
            }
            cxDisplay1.ResetView();

            if (_pointCloud != null)
            {
                cxDisplay1.SetPointCloud(_pointCloud);
            }
            else
            {
                if (surface != null)
                    cxDisplay1.SetSurface(surface);
                if (surface2 != null)
                    cxDisplay1.SetSurface(surface2, "Surface2", pose: CxMatrix4X4.RotationZ(45f) * CxMatrix4X4.Translation(5, 3, 2));
            }

            ////����ƽ�� Plane3D
            //var plane = new CxPlane3D(new CxPoint3D(0, 0, 0), new CxVector3D(1, 1, 1));
            //cxDisplay1.SetPlane(plane, Color.FromArgb(100, Color.Blue));

            //����Box3D
            var box = new CxBox3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 10));
            //cxDisplay1.SetBox(new CxBox3D[] { box }, Color.FromArgb(100, Color.Yellow), 1);

            //cxDisplay1.SetPoint(new CxPoint3D[] { new CxPoint3D(2, 3, 1), new CxPoint3D(5, 1, 1) }, Color.Green, 10f, PointShape.Sphere);

            //����Box3D
            cxDisplay2.ResetView();
            cxDisplay2.SetBox(new CxBox3D[] { box }, Color.Yellow, 10);
        }

        private void DemoFrom_FormClosing(object sender, FormClosingEventArgs e)
        {
            GocatorHandle.GocatorHandle.Instance.DisConnect();
            cxDisplay1.Dispose();
            cxDisplay2.Dispose();
            _cxDisplayPbr?.Dispose();
            _cxDisplay2D?.Dispose();
            _currentImage?.Dispose();
            VisionOperator.DestroyLib();
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
            var surfacemap = VisionOperator.UniformSurface(points, surface.Intensity, 200, 3500,
                0.1f, 0.1f, surface.ZScale, -10, -175, surface.ZOffset);

            cxDisplay1.ClearSurfaceItems();
            cxDisplay1.SetSurface(surfacemap);
        }

        private void btn_addSeg3D_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            //����Segment3D�߶�
            cxDisplay2.SetSegment(new CxSegment3D[] { new CxSegment3D(new CxPoint3D(0, 0, 0), new CxPoint3D(1, 1, 1)) }, Color.Red);
            cxDisplay2.SetSegment(new CxSegment3D[] { new CxSegment3D(new CxPoint3D(0, 0, 0), new CxPoint3D(0, 1, 1)) }, Color.Yellow);

            cxDisplay2.SetPoint(new CxPoint3D[] { new CxPoint3D(2, 3, 1), new CxPoint3D(5, 1, 1) }, Color.Green, 1f, PointShape.Sphere);
            cxDisplay2.SetCoordinate3DSystem(new CxCoordination3D()
            {
                Origin = new CxPoint3D(5, 5, 5),
                XAxis = new CxVector3D(1, 0, 0),
                YAxis = new CxVector3D(0, 1, 0),
                ZAxis = new CxVector3D(0, 0, -1)
            }, 50);
            //���Ӷ����
            List<CxPoint3D> pts = new List<CxPoint3D>();
            pts.Add(new CxPoint3D(0, 0, 0));
            pts.Add(new CxPoint3D(1, 0, 0));
            pts.Add(new CxPoint3D(1, 1, 0));
            pts.Add(new CxPoint3D(1, 1, 1));
            var polygon = new CxPolygon3D(pts.ToArray(), false);
            cxDisplay2.SetPolygon(new CxPolygon3D[] { polygon }, Color.Blue);

            //����ƽ�� Plane3D
            var plane = new CxPlane3D(new CxPoint3D(0, 0, 0), new CxVector3D(1, 1, 1));
            cxDisplay2.SetPlane(new CxPlane3D[] { plane }, Color.FromArgb(100, Color.Red));

            //����Box3D
            var box = new CxBox3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 10));
            cxDisplay2.SetBox(new CxBox3D[] { box }, Color.FromArgb(100, Color.Yellow));

            //����TextInfo
            var text = new CxTextInfo(new CxPoint3D(0, 0, 0), "this is a test����!", 50);
            cxDisplay2.SetTextInfo(new CxTextInfo[] { text }, Color.Yellow);

            //����Text2D
            var text2d = new CxText2D(new CxPoint2D(10, 50), "2D Labels", 10);
            cxDisplay2.SetText2D(new CxText2D[] { text2d }, Color.Green);
            cxDisplay2.ActivateAllItems();
        }

        private void btn_dragMark_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // 1. ��������ƽ̹�ο��棨Ϊ��Ȼ����ṩ��Чֵ�������� FitView��
            const int W = 50, L = 50;
            var flatData = new short[W * L]; // ȫ�� = Z=0
            var flatSurf = new CxSurface(W, L, flatData, null,
                xOffset: 0, yOffset: 0, zOffset: 0,
                xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf); // 10��10 ƽ�棬FitView �Զ��������

            // 2. ���ÿ��϶� MARK �㣨λ��ƽ������ 5,5,0��
            var mark = cxDisplay2.SetPoint(
                new[] { new CxPoint3D(5.0f, 5.0f, 0f) }, Color.Red, 1f, PointShape.Sphere);
            mark.IsActiveObj = true;
            mark.HitThreshold = 2f;

            // 3. ���� OnChanged��ʵʱ���������ǩ
            mark.OnChanged += item =>
            {
                var pos = ((CxPoint3DItem)item).Point3Ds[0];
                lbl_markPos.Text = $"X:{pos.X:F3}  Y:{pos.Y:F3}  Z:{pos.Z:F3}";
            };
        }

        private void btn_boxDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // 1. ƽ̹�ο��棨Ϊ��Ȼ����ṩ��Чֵ������ FitView��
            const int W = 60, L = 60;
            var flatData = new short[W * L];
            var flatSurf = new CxSurface(W, L, flatData, null,
                xOffset: 0, yOffset: 0, zOffset: 0,
                xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf);  // 12��12 ƽ��

            // 2. ���ÿ���ק Box��λ��ƽ�����룩
            var initBox = new CxBox3D(new CxPoint3D(6.0f, 6.0f, 0.5f), new CxSize3D(4.0f, 3.0f, 1.0f));
            var box = cxDisplay2.SetBox(new[] { initBox }, Color.FromArgb(80, Color.Cyan));
            box.IsActiveObj = true;
            box.HitThreshold = 1f;

            // 3. ʵʱ��ʾ Box �� Center �� Size
            box.OnChanged += item =>
            {
                var b = ((CxBox3DItem)item).Box3Ds[0];
                lbl_markPos.Text =
                    $"C({b.Center.X:F2},{b.Center.Y:F2},{b.Center.Z:F2})\n" +
                    $"W{b.Size.Width:F2} H{b.Size.Height:F2} D{b.Size.Depth:F2}";
            };
        }

        private void btn_segDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // �ο�ƽ�棨��Ȼ��壩
            const int W = 60, L = 60;
            var flatSurf = new CxSurface(W, L, new short[W * L], null,
                xOffset: 0, yOffset: 0, zOffset: 0, xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf);

            // ������������ק�߶�
            var seg = cxDisplay2.SetSegment(new[]
            {
                new CxSegment3D(new CxPoint3D(2f, 4f, 0f), new CxPoint3D(8f, 4f, 0f)),
                new CxSegment3D(new CxPoint3D(3f, 7f, 0f), new CxPoint3D(9f, 9f, 0f)),
            }, Color.Lime, 2f);
            seg.IsActiveObj = true;
            seg.HitThreshold = 0.3f;

            seg.OnChanged += item =>
            {
                var s = ((CxSegment3DItem)item).Segment3Ds[0];
                lbl_markPos.Text =
                    $"S({s.Start.X:F2},{s.Start.Y:F2})\n" +
                    $"E({s.End.X:F2},{s.End.Y:F2})";
            };
        }

        private void btn_polyDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // �ο�ƽ�棨��Ȼ��壩
            const int W = 60, L = 60;
            var flatSurf = new CxSurface(W, L, new short[W * L], null,
                xOffset: 0, yOffset: 0, zOffset: 0, xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf);

            // ����һ�����ζ���Σ��պϣ���һ�����ߣ����ţ�
            var rect = new CxPolygon3D(new[]
            {
                new CxPoint3D(2f, 3f, 0f), new CxPoint3D(8f, 3f, 0f),
                new CxPoint3D(8f, 7f, 0f), new CxPoint3D(2f, 7f, 0f),
            }, isClosed: true);
            var open = new CxPolygon3D(new[]
            {
                new CxPoint3D(3f, 9f, 0f), new CxPoint3D(6f, 10f, 0f), new CxPoint3D(9f, 9f, 0f),
            }, isClosed: false);

            var poly = cxDisplay2.SetPolygon(new[] { rect, open }, Color.Cyan, 2f);
            poly.IsActiveObj = true;
            poly.HitThreshold = 0.3f;

            poly.OnChanged += item =>
            {
                var pts = ((CxPolygon3DItem)item).Polygon3Ds[0].Points;
                lbl_markPos.Text = pts.Length > 0
                    ? $"V0({pts[0].X:F2},{pts[0].Y:F2})\nV2({pts[2].X:F2},{pts[2].Y:F2})"
                    : "--";
            };
        }

        private void button1_Click(object sender, EventArgs e)
        {
            cxDisplay1.ResetView();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //var ps = surface.ToPoints();
            //var matrix = new CxMatrix4X4(new float[16] {
            //    1, 0, 0, 0,
            //    0, -1, 0, 0,
            //    0, 0, 1, 0,
            //    0, 0, 0, 1
            //});
            var matrix = CxMatrix4X4.RotationY(45f);
            var points = VisionOperator.TransformSurface(surface, matrix, 0.01f, 0.01f, SampleMode.Average);
            cxDisplay2.ResetView();
            cxDisplay2.SetSurface(points);
        }

        private void DemoFrom_Load(object sender, EventArgs e)
        {

        }

        private void btn_surfaceToMesh_Click(object sender, EventArgs e)
        {
            if (surface == null) return;
            _currentMesh = VisionOperator.SurfaceToMesh(surface, generateUVs: true);
            if (_currentMesh == null || _currentMesh.Vertices.Length == 0) return;

            cxDisplay2.ResetView();
            cxDisplay2.SetMesh(_currentMesh);
        }

        private void btn_meshToSurface_Click(object sender, EventArgs e)
        {
            if (_currentMesh == null) return;

            var matrix = CxMatrix4X4.RotationY(45f);
            var result = VisionOperator.MeshToSurface(_currentMesh, matrix, new CxBox3D(new CxPoint3D(2, 4, 1.2f), new CxSize3D(15, 25, 2)), 0.01f, 0.01f);
            if (result == null) return;

            cxDisplay1.ResetView();
            cxDisplay1.SetSurface(result);
        }

        private bool _poseApplied = false;

        private void btn_testPose_Click(object sender, EventArgs e)
        {
            _poseApplied = !_poseApplied;
            var pose = _poseApplied
                ? CxMatrix4X4.RotationZ(45f) * CxMatrix4X4.Translation(5, 3, 2)
                : CxMatrix4X4.Identity();

            cxDisplay1.SetSurfacePose(pose);
            cxDisplay1.SetPointCloudPose(pose);
            cxDisplay1.SetMeshPose(pose);

            btn_testPose.Text = _poseApplied ? "Reset Pose" : "Test Pose";
        }

        private void btn_ioSave_Click(object sender, EventArgs e)
        {
            string dir = "";

            if (surface != null)
            {
                string path = Path.Combine(dir, "test_surface.cxsurface");
                VisionOperator.SaveSurface(surface, path);
                var loaded = VisionOperator.LoadSurface(path);
                if (loaded != null)
                {
                    cxDisplay2.ResetView();
                    cxDisplay2.SetSurface(loaded);
                    MessageBox.Show($"Surface saved �� loaded\n{path}", "Save R/L");
                }
            }
            else if (_currentMesh != null)
            {
                string path = Path.Combine(dir, "test_mesh.obj");
                VisionOperator.SaveMesh(_currentMesh, path);
                var loaded = VisionOperator.LoadMesh(path);
                if (loaded != null)
                {
                    cxDisplay2.ResetView();
                    cxDisplay2.SetMesh(loaded);
                    MessageBox.Show($"Mesh saved �� loaded (OBJ)\n{path}", "Save R/L");
                }
            }
            else
            {
                MessageBox.Show("No surface or mesh data available.", "Save R/L");
            }
        }

        private void btn_ioLoadMesh_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Mesh files|*.obj;*.cxmesh;*.stl;*.stla|OBJ files|*.obj|CxMesh binary|*.cxmesh|STL binary|*.stl|STL ASCII|*.stla"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                var mesh = VisionOperator.LoadMesh(dlg.FileName);
                if (mesh == null)
                {
                    MessageBox.Show("Failed to load mesh.", "Load Mesh");
                    return;
                }
                _currentMesh = mesh;
                cxDisplay2.ResetView();
                cxDisplay2.SetMesh(mesh);
            }
        }

        private void btn_ioLoadSurface_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Surface files|*.cxsurface|All files|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                var loaded = VisionOperator.LoadSurface(dlg.FileName);
                if (loaded == null)
                {
                    MessageBox.Show("Failed to load surface.", "Load Surface");
                    return;
                }
                surface = loaded;
                cxDisplay2.ResetView();
                cxDisplay2.SetSurface(loaded);
            }
        }

        // ���� 2D Tab ������������������������������������������������������������������������������������������������������������������������������������

        private CxDisplay2D _cxDisplay2D;
        private Label _lbl2DPos;
        private CxImage _currentImage;
        private SplitContainer _split2D;
        private bool _split2DInit;

        private void Build2DTab(TabPage page)
        {
            _split2D = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                Panel2MinSize = 146,
            };

            // Set splitter position once the container has a valid size
            _split2D.SizeChanged += (s, ev) =>
            {
                if (!_split2DInit && _split2D.Width > 200)
                {
                    _split2DInit = true;
                    _split2D.SplitterDistance = Math.Max(_split2D.Width - 150, 100);
                }
            };

            _cxDisplay2D = new CxDisplay2D { Dock = DockStyle.Fill };
            _cxDisplay2D.ShowAxes(true);
            _cxDisplay2D.SetBackgroundColor(Color.White);
            _cxDisplay2D.SetAspectLock(false);
            _cxDisplay2D.CoordinatesChanged += pos =>
            {
                var text = $"X:{pos.X:F1}  Y:{pos.Y:F1}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
            _split2D.Panel1.Controls.Add(_cxDisplay2D);
            // Button panel (built entirely in code)
            var btnPanel = new Panel { Dock = DockStyle.Fill };
            int y = 10;
            Button MakeBtn(string text, EventHandler click)
            {
                var b = new Button { Text = text, Location = new Point(7, y), Size = new Size(132, 26) };
                b.Click += click;
                btnPanel.Controls.Add(b);
                y += 33;
                return b;
            }

            MakeBtn("Load Image", btn2D_loadImage_Click);
            MakeBtn("Static Segs", btn2D_staticSeg_Click);
            MakeBtn("Drag Segs", btn2D_dragSeg_Click);
            MakeBtn("Static Circles", btn2D_staticCircle_Click);
            MakeBtn("Drag Circles", btn2D_dragCircle_Click);
            MakeBtn("Drag 2 Circs", btn2D_drag2Circles_Click);
            MakeBtn("Static Lines", btn2D_staticLine_Click);
            MakeBtn("Drag Lines",   btn2D_dragLine_Click);
            MakeBtn("Static Boxes", btn2D_staticBox_Click);
            MakeBtn("Drag Box",     btn2D_dragBox_Click);
            MakeBtn("Drag Polygon", btn2D_dragPolygon_Click);
            MakeBtn("Static Rect", btn2D_staticRect_Click);
            MakeBtn("Drag Rect",   btn2D_dragRect_Click);
            MakeBtn("Arc Demo",    btn2D_arcDemo_Click);
            MakeBtn("Drag Arc",    btn2D_dragArc_Click);
            MakeBtn("Drag Fitting", btn2D_dragFitting_Click);
            MakeBtn("Drag ArcFit", btn2D_dragArcFitting_Click);
            MakeBtn("Drag PolyFit", btn2D_dragPolyFitting_Click);
            MakeBtn("Region Demo", btn2D_regionDemo_Click);
            MakeBtn("Drag CircleFit", btn2D_dragCircleFitting_Click);
            MakeBtn("Show Coord", btn2D_showCoord_Click);
            MakeBtn("Clear Overlays", btn2D_clearOverlays_Click);
            MakeBtn("Clear All", btn2D_clearAll_Click);

            _lbl2DPos = new Label
            {
                Font = new Font("Consolas", 8F),
                Location = new Point(5, y + 8),
                Size = new Size(140, 48),
                Text = "X: ---  Y: ---",
            };
            btnPanel.Controls.Add(_lbl2DPos);
            _split2D.Panel2.Controls.Add(btnPanel);
            page.Controls.Add(_split2D);
        }


        // ���� 2D Button Handlers ��������������������������������������������������������������������������������������������������������������

        private void btn2D_loadImage_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                var img = LoadColorImage(dlg.FileName);
                if (img == null) return;
                _currentImage?.Dispose();
                _currentImage = img;
                _cxDisplay2D.ClearOverlays();
                _cxDisplay2D.SetScaleAndOffset(new CxPoint3D(1,1f,1), new CxPoint3D(0, 0, 0));
                _cxDisplay2D.SetImageAdvance(img);
            }
        }

        private void btn2D_staticSeg_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float w = box.Size.Width > 0 ? box.Size.Width : 400f;
            float h = box.Size.Height > 0 ? box.Size.Height : 400f;

            var segs = new[]
            {
                new CxSegment2D(new CxPoint2D(w * 0.2f, 0),    new CxPoint2D(w * 0.2f, h)),
                new CxSegment2D(new CxPoint2D(w * 0.8f, 0),    new CxPoint2D(w * 0.8f, h)),
                new CxSegment2D(new CxPoint2D(0,         h * 0.5f), new CxPoint2D(w, h * 0.5f)),
            };
            _cxDisplay2D.SetSegment(segs, Color.LimeGreen, 1.5f);
        }

        private void btn2D_dragSeg_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float len = Math.Max(box.Size.Width > 0 ? box.Size.Width : 400,
                                  box.Size.Height > 0 ? box.Size.Height : 400) * 0.15f;
            if (len < 20) len = 60f;

            var segs = new[] { new CxSegment2D(new CxPoint2D(cx - len, cy), new CxPoint2D(cx + len, cy)) };
            var item = _cxDisplay2D.SetSegment(segs, Color.Yellow, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var seg = ((CxSegment2DItem)i).Segments[0];
                var text = $"S:({seg.Start.X:F0},{seg.Start.Y:F0})\nE:({seg.End.X:F0},{seg.End.Y:F0})";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_staticCircle_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 100;
            float cy = box.Size.Height > 0 ? box.Center.Y : 100;
            float minDim = Math.Min(box.Size.Width > 0 ? box.Size.Width : 400,
                                     box.Size.Height > 0 ? box.Size.Height : 400);

            var circles = new[]
            {
                new CxCircle2D(new CxPoint2D(cx, cy), minDim * 0.08f),
                new CxCircle2D(new CxPoint2D(cx, cy), minDim * 0.18f),
            };
            _cxDisplay2D.SetCircle(circles, Color.LightBlue, 1.5f);
        }

        private void btn2D_dragCircle_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 100;
            float cy = box.Size.Height > 0 ? box.Center.Y : 100;
            float r = Math.Min(box.Size.Width > 0 ? box.Size.Width : 400,
                                     box.Size.Height > 0 ? box.Size.Height : 400) * 0.1f;
            if (r < 10) r = 40f;

            var circles = new[] { new CxCircle2D(new CxPoint2D(cx, cy), r) };
            var item = _cxDisplay2D.SetCircle(circles, Color.Cyan, 2f,true);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var c = ((CxCircle2DItem)i).Circles[0];
                var text = $"C:({c.Center.X:F0},{c.Center.Y:F0})\nR:{c.Radius:F1}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_drag2Circles_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float r = Math.Min(box.Size.Width > 0 ? box.Size.Width : 400,
                                box.Size.Height > 0 ? box.Size.Height : 400) * 0.08f;
           // if (r < 10) r = 40f;

            var circles = new[]
            {
                new CxCircle2D(new CxPoint2D(cx - r * 2, cy), r),
                new CxCircle2D(new CxPoint2D(cx + r * 2, cy), r),
            };
            var item = _cxDisplay2D.SetCircle(circles, Color.Orange, 2f, filled: true);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var cs = ((CxCircle2DItem)i).Circles;
                var text = $"C1:({cs[0].Center.X:F0},{cs[0].Center.Y:F0})R:{cs[0].Radius:F1}\nC2:({cs[1].Center.X:F0},{cs[1].Center.Y:F0})R:{cs[1].Radius:F1}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_staticLine_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;

            var lines = new[]
            {
                new CxLine2D(new CxPoint2D(cx, cy), new CxVector2D(1, 0)),
                new CxLine2D(new CxPoint2D(cx, cy), new CxVector2D(0, 1)),
                new CxLine2D(new CxPoint2D(cx, cy), new CxVector2D(1, 1)),
            };
            _cxDisplay2D.SetLine(lines, Color.OrangeRed, 1.5f);
        }

        private void btn2D_dragLine_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;

            var lines = new[] { new CxLine2D(new CxPoint2D(cx, cy), new CxVector2D(1, 0.3f)) };
            var item = _cxDisplay2D.SetLine(lines, Color.Magenta, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var line = ((CxLine2DItem)i).Lines[0];
                var text = $"P:({line.Point.X:F0},{line.Point.Y:F0})\nD:({line.Direction.X:F2},{line.Direction.Y:F2})";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_staticBox_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float minDim = Math.Min(box.Size.Width > 0 ? box.Size.Width : 400,
                                     box.Size.Height > 0 ? box.Size.Height : 400);

            var boxes = new[]
            {
                new CxBox2D(new CxPoint2D(cx, cy), new CxSize2D(minDim * 0.6f, minDim * 0.4f)),
                new CxBox2D(new CxPoint2D(cx, cy), new CxSize2D(minDim * 0.3f, minDim * 0.3f)),
            };
            _cxDisplay2D.SetBox(boxes, Color.YellowGreen, 1.5f);
        }

        private void btn2D_dragBox_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float s = Math.Min(box.Size.Width > 0 ? box.Size.Width : 400,
                                box.Size.Height > 0 ? box.Size.Height : 400) * 0.2f;
            if (s < 20) s = 80f;

            var boxes = new[] { new CxBox2D(new CxPoint2D(cx, cy), new CxSize2D(s, s)) };
            var item = _cxDisplay2D.SetBox(boxes, Color.Gold, 2f, filled: false);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var b = ((CxBox2DItem)i).Boxes[0];
                var text = $"C:({b.Center.X:F0},{b.Center.Y:F0})\nS:({b.Size.Width:F0}��{b.Size.Height:F0})";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_dragPolygon_Click(object sender, EventArgs e)
        {
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float s = Math.Min(box.Size.Width > 0 ? box.Size.Width : 400,
                                box.Size.Height > 0 ? box.Size.Height : 400) * 0.12f;
            if (s < 20) s = 80f;

            var tri = new CxPolygon2D(new[]
            {
                new CxPoint2D(cx, cy - s),
                new CxPoint2D(cx - s * 0.866f, cy + s * 0.5f),
                new CxPoint2D(cx + s * 0.866f, cy + s * 0.5f),
            }, isClosed: true);

            var open = new CxPolygon2D(new[]
            {
                new CxPoint2D(cx - s * 1.8f, cy),
                new CxPoint2D(cx, cy - s * 0.8f),
                new CxPoint2D(cx + s * 1.8f, cy),
            }, isClosed: false);

            var item = _cxDisplay2D.SetPolygon(new[] { tri, open }, Color.DeepSkyBlue, 2f, filled: true);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var pts = ((CxPolygon2DItem)i).Polygons[0].Points;
                var text = pts.Length > 0
                    ? $"V0:({pts[0].X:F0},{pts[0].Y:F0})\nV1:({pts[1].X:F0},{pts[1].Y:F0})"
                    : "--";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_staticRect_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var rects = new[]
            {
                new CxRectangle2D(new CxPoint2D(200, 200), new CxSize2D(120, 80), 0f),
                new CxRectangle2D(new CxPoint2D(400, 300), new CxSize2D(100, 60), 30f),
                new CxRectangle2D(new CxPoint2D(600, 200), new CxSize2D(140, 90), -45f),
            };
            _cxDisplay2D.SetRectangle(rects, Color.Coral, 2f, filled: false);
            _cxDisplay2D.DeactivateAllItems();
        }

        private void btn2D_dragRect_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var initRect = new CxRectangle2D(new CxPoint2D(400, 300), new CxSize2D(200, 120), 15f);
            var item = _cxDisplay2D.SetRectangle(new[] { initRect }, Color.Gold, 2f, filled: false);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var r = ((CxRectangle2DItem)i).Rectangles[0];
                var text = $"C:({r.Center.X:F0},{r.Center.Y:F0}) A:{r.Angle:F1}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_arcDemo_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var arcs = new[]
            {
                new CxArc2D(new CxPoint2D(200, 200), 80, 0f, 90f),
                new CxArc2D(new CxPoint2D(400, 200), 60, 45f, 180f),
                new CxArc2D(new CxPoint2D(600, 200), 100, -30f, 270f),
            };
            _cxDisplay2D.SetArc(arcs, Color.Cyan, 2f);
            _cxDisplay2D.DeactivateAllItems();
        }

        private void btn2D_dragArc_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float r = Math.Max(box.Size.Width > 0 ? box.Size.Width : 400,
                                box.Size.Height > 0 ? box.Size.Height : 400) * 0.15f;
            if (r < 20) r = 80f;
            var initArc = new CxArc2D(new CxPoint2D(cx, cy), r, 0f, 90f);
            var item = _cxDisplay2D.SetArc(new[] { initArc }, Color.Orange, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var a = ((CxArc2DItem)i).Arcs[0];
                var text = $"R:{a.Radius:F0} S:{a.StartAngle:F0} W:{a.SweepAngle:F0}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_dragFitting_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float len = Math.Max(box.Size.Width > 0 ? box.Size.Width : 400,
                                  box.Size.Height > 0 ? box.Size.Height : 400) * 0.15f;
            if (len < 20) len = 60f;

            var field = new CxSegment2DFittingField(
                new CxSegment2D(new CxPoint2D(cx - len, cy), new CxPoint2D(cx + len, cy)),
                40f);
            var item = _cxDisplay2D.SetSegmentFittingField(new[] { field }, Color.Yellow, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var f = ((CxSegment2DFittingFieldItem)i).Fields[0];
                var text = $"S:({f.Axis.Start.X:F0},{f.Axis.Start.Y:F0}) E:({f.Axis.End.X:F0},{f.Axis.End.Y:F0}) W:{f.Width:F0}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_dragArcFitting_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float r = Math.Max(box.Size.Width > 0 ? box.Size.Width : 400,
                               box.Size.Height > 0 ? box.Size.Height : 400) * 0.12f;
            if (r < 30) r = 80f;

            var field = new CxArc2DFittingField(
                new CxArc2D(new CxPoint2D(cx, cy), r, 0f, 90f), 40f);
            var item = _cxDisplay2D.SetArcFittingField(new[] { field }, Color.Yellow, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var f = ((CxArc2DFittingFieldItem)i).Fields[0];
                var a = f.Axis;
                var text = $"C:({a.Center.X:F0},{a.Center.Y:F0}) R:{a.Radius:F0} S:{a.StartAngle:F0} W:{a.SweepAngle:F0} BW:{f.Width:F0}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_dragPolyFitting_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float r = Math.Max(box.Size.Width > 0 ? box.Size.Width : 400,
                               box.Size.Height > 0 ? box.Size.Height : 400) * 0.15f;
            if (r < 30) r = 60f;

            var line = new CxPoint2D[]
            {
                new CxPoint2D(cx - r, cy - r * 0.3f),
                new CxPoint2D(cx,     cy + r * 0.4f),
                new CxPoint2D(cx + r, cy - r * 0.3f),
            };
            var field = new CxPolygon2DFittingField(new CxPolygon2D(line, false), 30f);
            var item = _cxDisplay2D.SetPolygonFittingField(new[] { field }, Color.Yellow, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var f = ((CxPolygon2DFittingFieldItem)i).Fields[0];
                var text = $"N:{f.Axis.Points?.Length ?? 0} W:{f.Width:F0}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_dragCircleFitting_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float r = Math.Max(box.Size.Width > 0 ? box.Size.Width : 400,
                               box.Size.Height > 0 ? box.Size.Height : 400) * 0.1f;
            if (r < 30) r = 50f;

            var field = new CxCircle2DFittingField(new CxCircle2D(new CxPoint2D(cx, cy), r), 30f);
            var item = _cxDisplay2D.SetCircleFittingField(new[] { field }, Color.Yellow, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var f = ((CxCircle2DFittingFieldItem)i).Fields[0];
                var c = f.Axis;
                var text = $"C:({c.Center.X:F0},{c.Center.Y:F0}) R:{c.Radius:F0} W:{f.Width:F0}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn_diffDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            const int w = 80, h = 80;
            float spacing = 0.15f;
            float freq = 0.35f;
            float amp = 1.5f;

            var verts = new CxPoint3D[w * h];
            var uvs = new CxPoint2D[w * h];
            var diff = new float[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    float fx = x * spacing;
                    float fy = y * spacing;
                    float z = amp * (float)(Math.Sin(fx * freq) * Math.Cos(fy * freq));
                    verts[idx] = new CxPoint3D(fx, fy, 0);
                    uvs[idx] = new CxPoint2D((float)x / (w - 1), (float)y / (h - 1));
                    diff[idx] = z;
                }
            }

            var indices = new uint[(w - 1) * (h - 1) * 6];
            int i = 0;
            for (int y = 0; y < h - 1; y++)
            {
                uint row = (uint)(y * w);
                uint next = (uint)((y + 1) * w);
                for (int x = 0; x < w - 1; x++)
                {
                    uint tl = row + (uint)x;
                    uint tr = tl + 1;
                    uint bl = next + (uint)x;
                    uint br = bl + 1;
                    indices[i++] = tl; indices[i++] = tr; indices[i++] = bl;
                    indices[i++] = tr; indices[i++] = br; indices[i++] = bl;
                }
            }

            var mesh = new CxMesh
            {
                Vertices = verts,
                Indices = indices,
                //UVs = new CxPoint2D[0],
                //TextureWidth = 0,
                //TextureHeight = 0,
                Diff = diff,
            };

            _currentMesh = mesh;

            cxDisplay2.SurfaceMode = SurfaceMode.Mesh;
            cxDisplay2.SetMesh(mesh);
            // �����Զ����� Diff ģʽ��ȡ������ע��
            // cxDisplay2.SurfaceColorMode = SurfaceColorMode.Diff;

            lbl_markPos.Text = "Diff Demo: �ϳ����Ҳ� mesh\n�Ҽ���SurfaceColorMode��Diff �л�";
        }

        private void btn_diffSurfaceDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // CxSurface: 高度图（Data = W*L shorts，每个 = Z 高度）。Diff 同为 W*L 网格点。
            const int W = 90, L = 90;
            const float xScale = 0.15f, yScale = 0.15f, zScale = 0.002f;
            float freq = 0.35f;
            float amp = 1500f;

            var data = new short[W * L];
            var diff = new float[W * L];

            for (int y = 0; y < L; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int idx = y * W + x;
                    float fx = x * xScale;
                    float fy = y * yScale;
                    float z = 0;
                    data[idx] = (short)z;
                    diff[idx] = amp * (float)(Math.Sin(fx * freq) * Math.Cos(fy * freq)) * zScale;
                }
            }

            var surface = new CxSurface(W, L, data, null,
                xOffset: 0, yOffset: 0, zOffset: 0,
                xScale: xScale, yScale: yScale, zScale: zScale)
            { Diff = diff };

            cxDisplay2.SurfaceMode = SurfaceMode.Mesh;
            cxDisplay2.SetSurface(surface);
            // cxDisplay2.SurfaceColorMode = SurfaceColorMode.Diff;

            lbl_markPos.Text = "Diff Surface Demo: right-click SurfaceColorMode->Diff";
        }

        private void btn_diffPointDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // CxPointCloud: Data = W*L*3 shorts（每 3 个 = X,Y,Z）。Diff 为 W*L 网格点。
            const int W = 90, L = 90;
            const float xScale = 0.15f, yScale = 0.15f, zScale = 0.002f;
            float freq = 0.35f;
            float amp = 1500f;

            var data = new short[W * L * 3];
            var diff = new float[W * L];

            for (int y = 0; y < L; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int idx = y * W + x;
                    float fx = x * xScale;
                    float fy = y * yScale;
                    float z = 0;
                    data[idx * 3]     = (short)(x);
                    data[idx * 3 + 1] = (short)(y);
                    data[idx * 3 + 2] = (short)z;
                    diff[idx] = amp * (float)(Math.Sin(fx * freq) * Math.Cos(fy * freq)) * zScale;
                }
            }

            var cloud = new CxPointCloud(W, L, data, null,
                xOffset: 0, yOffset: 0, zOffset: 0,
                xScale: xScale, yScale: yScale, zScale: zScale)
            { Diff = diff };

            cxDisplay2.SurfaceMode = SurfaceMode.PointCloud;
            cxDisplay2.SetPointCloud(cloud);
            // cxDisplay2.SurfaceColorMode = SurfaceColorMode.Diff;

            lbl_markPos.Text = "Diff PointCloud Demo: right-click SurfaceColorMode->Diff";
        }

        private void btn_colorRangeDemo_Click(object sender, EventArgs e)
        {
            // 切换式：首次点击设固定 Color(Z) 范围，再次点击复位为自动。
            cxDisplay2.SurfaceColorMode = SurfaceColorMode.Color;
            if (_colorRangeSet)
            {
                cxDisplay2.ClearColorRange();
                _colorRangeSet = false;
                lbl_markPos.Text = "Color Range cleared -> auto";
            }
            else
            {
                cxDisplay2.SetColorRange(-1.0f, 1.0f);
                _colorRangeSet = true;
                lbl_markPos.Text = "Color Range set to [-1, 1] (click again to clear)";
            }
        }
        private bool _colorRangeSet = false;

        private void btn_diffRangeDemo_Click(object sender, EventArgs e)
        {
            // 切换式：首次点击设固定 Diff 范围，再次点击复位为自动。
            cxDisplay2.SurfaceColorMode = SurfaceColorMode.Diff;
            if (_diffRangeSet)
            {
                cxDisplay2.ClearDiffRange();
                _diffRangeSet = false;
                lbl_markPos.Text = "Diff Range cleared -> auto";
            }
            else
            {
                cxDisplay2.SetDiffRange(-0.5f, 0.5f);
                _diffRangeSet = true;
                lbl_markPos.Text = "Diff Range set to [-0.5, 0.5] (click again to clear)";
            }
        }
        private bool _diffRangeSet = false;

        private void btn_mixedViewDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // 1. Mesh：网格形式 + Color(Z 高度) 模式
            const int mw = 60, mh = 60;
            float spacing = 0.2f, freq = 0.3f, amp = 1.2f;
            var mVerts = new CxPoint3D[mw * mh];
            var mIdx = new uint[(mw - 1) * (mh - 1) * 6];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    float fx = x * spacing, fy = y * spacing;
                    mVerts[y * mw + x] = new CxPoint3D(fx, fy,
                        amp * (float)(Math.Sin(fx * freq) * Math.Cos(fy * freq)));
                }
            int ii = 0;
            for (int y = 0; y < mh - 1; y++)
                for (int x = 0; x < mw - 1; x++)
                {
                    uint tl = (uint)(y * mw + x), tr = tl + 1, bl = tl + (uint)mw, br = bl + 1;
                    mIdx[ii++] = tl; mIdx[ii++] = bl; mIdx[ii++] = tr;
                    mIdx[ii++] = tr; mIdx[ii++] = bl; mIdx[ii++] = br;
                }
            var mesh = new CxMesh { Vertices = mVerts, Indices = mIdx };
            // Mesh 形式 + Color 模式（不取全局，显式指定）
            cxDisplay2.SetMesh(mesh, "MixedMesh", mode: SurfaceMode.Mesh, colorMode: SurfaceColorMode.Color);

            // 2. PointCloud：点云形式 + Intensity 模式（与上面 Color 量纲不同）
            const int pw = 60, pl = 60;
            var pcData = new short[pw * pl * 3];
            var pcIntensity = new byte[pw * pl];
            for (int y = 0; y < pl; y++)
                for (int x = 0; x < pw; x++)
                {
                    int idx = y * pw + x;
                    pcData[idx * 3]     = (short)(x * 100);
                    pcData[idx * 3 + 1] = (short)(y * 100);
                    pcData[idx * 3 + 2] = (short)(80 + 60 * Math.Sin(x * 0.2));
                    pcIntensity[idx] = (byte)((x + y) * 4 % 256);
                }
            var cloud = new CxPointCloud(pw, pl, pcData, pcIntensity,
                xOffset: 15f, yOffset: 0f, zOffset: 0f,
                xScale: 0.1f, yScale: 0.1f, zScale: 0.01f);
            // 点云形式 + Intensity 模式
            cxDisplay2.SetPointCloud(cloud, "MixedPointCloud", mode: SurfaceMode.PointCloud, colorMode: SurfaceColorMode.Intensity);

            // 两者量纲不同（Color=Z，Intensity=0-255）→ colorBar 自动隐藏
            lbl_markPos.Text = "Mesh(Mesh+Color) + PointCloud(PC+Intensity)\nColorBar hidden: mixed magnitude classes";
        }

        private void btn2D_regionDemo_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();

            var pts = new CxPoint2D[]
            {
                new CxPoint2D(100, 20),
                new CxPoint2D(180, 60),
                new CxPoint2D(180, 140),
                new CxPoint2D(100, 180),
                new CxPoint2D(20, 140),
                new CxPoint2D(20, 60),
            };
            var poly = new CxPolygon2D(pts, true);
            VisionOperator.PolygonToRegion2D(poly, 200, 200, out var region);

            // Also create a second region via union with a smaller shifted shape
            var pts2 = new CxPoint2D[]
            {
                new CxPoint2D(70, 70),
                new CxPoint2D(130, 70),
                new CxPoint2D(130, 130),
                new CxPoint2D(70, 130),
            };
            var poly2 = new CxPolygon2D(pts2, true);
            VisionOperator.PolygonToRegion2D(poly2, 200, 200, out var region2);
            VisionOperator.SubtractRegion2D(region, region2, out var regionDiff);

            var item = _cxDisplay2D.SetRegion(new[] { region, region2, regionDiff }, Color.Yellow, 2f);
            item.IsActiveObj = true;
            item.OnChanged += i =>
            {
                var r = ((CxRegion2DItem)i).Regions[0];
                var text = $"Area:{r.Area} Runs:{r.Runs?.Length ?? 0}";
                if (_lbl2DPos.InvokeRequired)
                    _lbl2DPos.Invoke(new Action(() => _lbl2DPos.Text = text));
                else
                    _lbl2DPos.Text = text;
            };
        }

        private void btn2D_showCoord_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            var box = _cxDisplay2D.GetImageWorldRect();
            float cx = box.Size.Width > 0 ? box.Center.X : 200f;
            float cy = box.Size.Height > 0 ? box.Center.Y : 200f;
            float r = Math.Max(box.Size.Width > 0 ? box.Size.Width : 400,
                               box.Size.Height > 0 ? box.Size.Height : 400) * 0.12f;
            if (r < 40) r = 60f;

            var frames = new[]
            {
                new CxCoordination2D(new CxPoint2D(cx, cy), new CxPoint2D(r, r * 0.65f), 0f),
                new CxCoordination2D(new CxPoint2D(cx + r * 0.9f, cy + r * 0.4f), new CxPoint2D(r * 0.65f, r * 0.4f), -30f),
            };
            _cxDisplay2D.SetCoordination(frames,10,10, 2f);
        }

        private void btn2D_clearOverlays_Click(object sender, EventArgs e) =>
            _cxDisplay2D.ClearOverlays();

        private void btn2D_clearAll_Click(object sender, EventArgs e)
        {
            _cxDisplay2D.ClearOverlays();
            _cxDisplay2D.ClearImage();
            _currentImage?.Dispose();
            _currentImage = null;
            _lbl2DPos.Text = "X: ---  Y: ---";
        }

        // ���� Image Loading Helper ����������������������������������������������������������������������������������������������������������

        private CxImage LoadColorImage(string path)
        {
            try
            {
                _cxDisplay2D.ResetView();
                 _cxDisplay2D.SetScaleAndOffset(new CxPoint3D(1f, 1f, 1f), new CxPoint3D(-40f,-80f,0));
                _cxDisplay2D.ShowAxes(true);
                _cxDisplay2D.SetBackgroundColor(Color.White);
                _cxDisplay2D.SetAspectLock(false);
                using (var bmp = new Bitmap(path))
                {
                    // 4-channel BGRA (matches Format32bppArgb memory layout)
                    var lockRect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    var bd = bmp.LockBits(lockRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    var img = new CxImage();
                    img.SetData(bmp.Width, bmp.Height, bd.Scan0, PlainType.UInt8, 4);
                    bmp.UnlockBits(bd);
                    return img;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ͼƬ����ʧ��: {ex.Message}", "Load Image");
                return null;
            }
        }

        // ���� Group / Simulation Demo ��������������������������������������������������������������������������������������������������

        private SimDriver _simDriver;
        private Stopwatch _simWatch;
        private CxGroup _platformGroup;
        private CxGroup _vehicleGroup;
        private Button _btnGroupSim;
        private Button _btnStage3Axis;
        private Button _btnReplaceVehicle;
        private CxMeshItem _vehicleItem;

        private void BuildGroupSimButton()
        {
            _btnGroupSim = new Button
            {
                Text = "Group Sim",
                Location = new Point(29, 430),
                Size = new Size(123, 28),
            };
            _btnGroupSim.Click += btn_groupSim_Click;
            splitContainer1.Panel2.Controls.Add(_btnGroupSim);

            _btnStage3Axis = new Button
            {
                Text = "3-Axis Stage",
                Location = new Point(29, 460),
                Size = new Size(123, 28),
            };
            _btnStage3Axis.Click += btn_stage3Axis_Click;
            splitContainer1.Panel2.Controls.Add(_btnStage3Axis);

            _btnReplaceVehicle = new Button
            {
                Text = "Replace Vehicle",
                Location = new Point(160, 430),
                Size = new Size(123, 28),
            };
            _btnReplaceVehicle.Click += btn_replaceVehicle_Click;
            splitContainer1.Panel2.Controls.Add(_btnReplaceVehicle);

            var btnOverlayKey = new Button
            {
                Text = "Overlay Key",
                Location = new Point(160, 460),
                Size = new Size(123, 28),
            };
            btnOverlayKey.Click += btn_overlayKeyDemo_Click;
            splitContainer1.Panel2.Controls.Add(btnOverlayKey);

            // 公开事件演示：选中变化直接写进状态标签
            cxDisplay2.SelectionChanged += item =>
                lbl_markPos.Text = item == null
                    ? "Selection: (none)"
                    : $"Selection: {item.GetType().Name}";
        }

        private void btn_groupSim_Click(object sender, EventArgs e)
        {
            if (_simDriver != null) { StopGroupSim(); return; }
            StopStageSim();
            StartGroupSim();
        }

        private void StartGroupSim()
        {
            cxDisplay1.ResetView();

            // 1. 运动平台 + 载具（数字模型）与实物点云 mesh
            var platformMesh = BuildBoxMesh(8f, 6f, 0.4f, new CxPoint3D(0f, 0f, 0f));
            var vehicleMesh  = BuildBoxMesh(2.5f, 1.5f, 1.2f, new CxPoint3D(-1.5f, 0f, 0.8f));
            var realMesh     = BuildBumpyMesh(24, 24, 0.4f, new CxPoint3D(-5f, -5f, 0.4f));

            var platformItem = cxDisplay1.SetMesh(platformMesh, "Platform", mode: SurfaceMode.Mesh, colorMode: SurfaceColorMode.Color, autoFit: false);
            var vehicleItem  = cxDisplay1.SetMesh(vehicleMesh,  "Vehicle",  mode: SurfaceMode.Mesh, colorMode: SurfaceColorMode.Color, autoFit: false);
            var realItem     = cxDisplay1.SetMesh(realMesh,     "Real",     mode: SurfaceMode.Mesh, colorMode: SurfaceColorMode.Color, autoFit: true);
            _vehicleItem = vehicleItem;

            // 层级：MotionPlatform（整体运动）→ Vehicle（相对平台运动）→ 实物随载具
            _platformGroup = cxDisplay1.Groups.Create("MotionPlatform");
            cxDisplay1.Groups.Add(_platformGroup, platformItem);

            _vehicleGroup = cxDisplay1.Groups.Create("Vehicle", parent: _platformGroup);
            cxDisplay1.Groups.Add(_vehicleGroup, vehicleItem);
            cxDisplay1.Groups.Add(_vehicleGroup, realItem);

            // 外部仿真循环：Demo 自己驱动，CxDisplay 只提供 Groups.SetTransform。
            // 用后台驱动而非 WinForms Timer —— 后者跑在 WM_TIMER 上，鼠标在另一个 CxDisplay
            // 上持续移动时会被 WM_PAINT 饿死，动画冻结。
            _simWatch = Stopwatch.StartNew();
            _simDriver = new SimDriver(this, 16, SimTick);
            _simDriver.Start();

            _btnGroupSim.Text = "Stop Sim";
        }

        /// <summary>
        /// Replaces the vehicle mesh inside the running group simulation. A single keyed Set is
        /// enough: the group member is bound by key, so the new resource takes over the stored
        /// local pose automatically. autoFit stays off so the camera does not jump.
        /// </summary>
        private void btn_replaceVehicle_Click(object sender, EventArgs e)
        {
            if (_vehicleGroup == null || _vehicleItem == null) return;

            var replacement = BuildBoxMesh(3.0f, 1.8f, 1.4f, new CxPoint3D(-1.5f, 0f, 0.9f));
            _vehicleItem = cxDisplay1.SetMesh(replacement, "Vehicle",
                mode: SurfaceMode.Mesh, colorMode: SurfaceColorMode.Color, autoFit: false);
        }

        /// <summary>
        /// Overlay key demo: a keyed Set appends on first use, replaces the item in place on the
        /// second call (the previous item is disposed) and RemoveOverlayItem releases the key.
        /// </summary>
        private void btn_overlayKeyDemo_Click(object sender, EventArgs e)
        {
            var first = cxDisplay2.SetBox(
                new[] { new CxBox3D(new CxPoint3D(0f, 0f, 0f), new CxSize3D(6f, 6f, 3f)) },
                Color.Cyan, 1f, key: "DemoBox");

            var second = cxDisplay2.SetBox(
                new[] { new CxBox3D(new CxPoint3D(1f, 1f, 1f), new CxSize3D(8f, 8f, 4f)) },
                Color.Orange, 1f, key: "DemoBox");

            bool replaced = ReferenceEquals(second, cxDisplay2.GetOverlayItem("DemoBox"));
            bool removed = cxDisplay2.RemoveOverlayItem("DemoBox");

            lbl_markPos.Text =
                $"Overlay key: first.Disposed={first.IsDisposed}, replaced={replaced}\n" +
                $"RemoveOverlayItem=\"{removed}\", GetOverlayItem={(cxDisplay2.GetOverlayItem("DemoBox") == null ? "null" : "item")}";
        }

        private void StopGroupSim()
        {
            _simDriver?.Dispose();
            _simDriver = null;
            _simWatch = null;
            _btnGroupSim.Text = "Group Sim";
        }

        /// <summary>Runs on the UI thread (the driver marshals it there).</summary>
        private void SimTick()
        {
            if (_simWatch == null) return;
            double t = _simWatch.Elapsed.TotalSeconds;

            // 平台整体：沿 X 平移 + 绕 Z 摆动
            cxDisplay1.Groups.SetTransform(_platformGroup,
                CxMatrix4X4.Translation((float)(3.0 * Math.Sin(t)), 0f, 0f) *
                CxMatrix4X4.RotationZ((float)(25.0 * Math.Sin(t * 0.7))));

            // 载具相对平台：上下浮动 + 绕 Y 摆动
            cxDisplay1.Groups.SetTransform(_vehicleGroup,
                CxMatrix4X4.Translation(0f, 0f, (float)(0.6 * Math.Sin(t * 3.0))) *
                CxMatrix4X4.RotationY((float)(20.0 * Math.Sin(t * 1.5))));
        }

        // ── Gantry 3-axis trace demo ─────────────────────────────────────────────

        private SimDriver _stageDriver;
        private Stopwatch _stageWatch;
        private CxGroup _stageGroupX;
        private CxGroup _stageGroupY;
        private CxGroup _stageGroupZ;

        private const float  StageLx          = 6f;    // 物料 X 尺寸
        private const float  StageLy          = 4f;    // 物料 Y 尺寸
        private const float  StageCarrierTop  = 0.9f;  // 载台顶面高度
        private const float  StageClearance   = 0.15f; // 刀尖离面间隙
        private const float  StageTipHomeZ    = 2.8f;  // 刀尖初始高度
        private const double StageSideSeconds = 1.6;   // 每条边描边时长

        /// <summary>物料起伏顶面在局部 (u,v) 处相对底面的高度（与 BuildBumpyMesh 的公式一致）。</summary>
        private static float StageHeight(float u, float v)
            => 0.35f * (float)(Math.Sin(u + 3.0) * Math.Cos(v + 2.0));

        /// <summary>刀尖在世界 Y=0 平面内的描边轨迹点（贴合物料顶面）。</summary>
        private static CxPoint3D StageTip(float u, float v)
            => new CxPoint3D(u, 0f, StageCarrierTop + StageHeight(u, v) + StageClearance);

        private void btn_stage3Axis_Click(object sender, EventArgs e)
        {
            if (_stageDriver != null) { StopStageSim(); return; }
            StopGroupSim();
            StartStageSim();
        }

        private void StartStageSim()
        {
            cxDisplay1.ResetView();

            // 台面（mesh，兼作取景范围）
            var table = BuildBoxMesh(26f, 18f, 0.2f, new CxPoint3D(0f, 0f, -0.2f));
            cxDisplay1.SetMesh(table, "Table", mode: SurfaceMode.Mesh, colorMode: SurfaceColorMode.Color, autoFit: true);

            // 固定件（几何 box）：龙门（立柱+顶梁，不移动）、梁上 X 导轨、台面 Y 导轨
            cxDisplay1.SetBox(new[]
            {
                new CxBox3D(new CxPoint3D(-4.5f, 0f, 4.5f), new CxSize3D(0.5f, 0.5f, 9f)),      // 立柱
                new CxBox3D(new CxPoint3D( 4.5f, 0f, 4.5f), new CxSize3D(0.5f, 0.5f, 9f)),      // 立柱
                new CxBox3D(new CxPoint3D( 0f,   0f, 8.8f), new CxSize3D(9.5f, 0.6f, 0.6f)),   // 顶梁
                new CxBox3D(new CxPoint3D( 0f, -0.35f, 8.35f), new CxSize3D(9.0f, 0.2f, 0.2f)), // 梁上 X 导轨
                new CxBox3D(new CxPoint3D( 0f,  0.35f, 8.35f), new CxSize3D(9.0f, 0.2f, 0.2f)),
                new CxBox3D(new CxPoint3D(-1.6f, 0f, 0.35f), new CxSize3D(0.4f, 9f, 0.4f)),   // 台面 Y 导轨
                new CxBox3D(new CxPoint3D( 1.6f, 0f, 0.35f), new CxSize3D(0.4f, 9f, 0.4f)),
            }, Color.DimGray, 1f);

            // 描边轨迹（世界 Y=0 平面内的 X-Z 轮廓，绕物料边框一圈）
            const int n = 30;
            var tipPts = new List<CxPoint3D>();
            for (int i = 0; i < n; i++) { float s = i / (float)n; tipPts.Add(StageTip(-StageLx / 2 + StageLx * s,  StageLy / 2)); }
            for (int i = 0; i < n; i++) { float s = i / (float)n; tipPts.Add(StageTip( StageLx / 2,                 StageLy / 2 - StageLy * s)); }
            for (int i = 0; i < n; i++) { float s = i / (float)n; tipPts.Add(StageTip( StageLx / 2 - StageLx * s, -StageLy / 2)); }
            for (int i = 0; i < n; i++) { float s = i / (float)n; tipPts.Add(StageTip(-StageLx / 2,                -StageLy / 2 + StageLy * s)); }
            tipPts.Add(tipPts[0]);
            var tipSegs = new CxSegment3D[tipPts.Count - 1];
            for (int i = 0; i < tipSegs.Length; i++)
                tipSegs[i] = new CxSegment3D(tipPts[i], tipPts[i + 1]);
            cxDisplay1.SetSegment(tipSegs, Color.Orange, 2f);

            // ── Y 轴（台面固定导轨）：Y 滑块 + 治具 + 物料 ──
            var ySlider = cxDisplay1.SetBox(new[]
            {
                new CxBox3D(new CxPoint3D(0f, 0f, 0.45f), new CxSize3D(3.6f, 2.6f, 0.5f)),  // Y 滑块
                new CxBox3D(new CxPoint3D(0f, 0f, 0.75f), new CxSize3D(7.5f, 5.5f, 0.3f)),  // 治具
            }, Color.DodgerBlue, 1f);

            var material = BuildBumpyMesh(30, 20, 0.2f, new CxPoint3D(-StageLx / 2, -StageLy / 2, 0f));
            var materialItem = cxDisplay1.SetMesh(material, "Material",
                pose: CxMatrix4X4.Translation(0f, 0f, StageCarrierTop),
                mode: SurfaceMode.Mesh, colorMode: SurfaceColorMode.Color, autoFit: false);

            _stageGroupY = cxDisplay1.Groups.Create("YSlider");
            cxDisplay1.Groups.Add(_stageGroupY, ySlider);
            cxDisplay1.Groups.Add(_stageGroupY, materialItem);

            // ── X 轴（梁上滑块）：X 滑块 + Z 立柱（随 X 移动）──
            var xSlider = cxDisplay1.SetBox(new[]
            {
                new CxBox3D(new CxPoint3D(0f, 0f, 8.1f), new CxSize3D(1.4f, 1.4f, 0.9f)),  // X 滑块
                new CxBox3D(new CxPoint3D(0f, 0f, 5.5f), new CxSize3D(0.6f, 0.6f, 6.0f)),  // Z 立柱
            }, Color.SeaGreen, 1f);

            _stageGroupX = cxDisplay1.Groups.Create("XSlider");
            cxDisplay1.Groups.Add(_stageGroupX, xSlider);

            // ── Z 轴（立柱上滑块）：Z 滑块 + 工具头 + 刀尖标记 ──
            var zSlider = cxDisplay1.SetBox(new[]
            {
                new CxBox3D(new CxPoint3D(0f, 0f, 5.5f),  new CxSize3D(1.2f, 1.2f, 0.8f)),  // Z 滑块
                new CxBox3D(new CxPoint3D(0f, 0f, 3.95f), new CxSize3D(0.5f, 0.5f, 2.3f)),  // 主轴（下端＝刀尖）
            }, Color.OrangeRed, 1f);

            _stageGroupZ = cxDisplay1.Groups.Create("ZSlider", parent: _stageGroupX);
            cxDisplay1.Groups.Add(_stageGroupZ, zSlider);

            var tipMarker = cxDisplay1.SetPoint(new[] { new CxPoint3D(0f, 0f, StageTipHomeZ) },
                Color.Yellow, 0.6f, PointShape.Sphere);
            cxDisplay1.Groups.Add(_stageGroupZ, tipMarker);

            // 外部仿真循环（同 Group Sim：后台驱动，不走 WM_TIMER）
            _stageWatch = Stopwatch.StartNew();
            _stageDriver = new SimDriver(this, 50, StageTick);
            _stageDriver.Start();

            _btnStage3Axis.Text = "Stop Stage";
        }

        private void StopStageSim()
        {
            if (_stageDriver == null) return;
            _stageDriver.Dispose();
            _stageDriver = null;
            _stageWatch = null;
            _btnStage3Axis.Text = "3-Axis Stage";
        }

        /// <summary>Runs on the UI thread (the driver marshals it there).</summary>
        private void StageTick()
        {
            if (_stageWatch == null) return;

            // 绕物料边框连续循环：0 前边 → 1 右边 → 2 后边 → 3 左边
            double p = _stageWatch.Elapsed.TotalSeconds / StageSideSeconds % 4.0;
            int side = (int)p;
            float s = (float)(p - side);
            float u, v;
            switch (side)
            {
                case 0:  u = -StageLx / 2 + StageLx * s; v =  StageLy / 2;                 break;
                case 1:  u =  StageLx / 2;               v =  StageLy / 2 - StageLy * s;  break;
                case 2:  u =  StageLx / 2 - StageLx * s; v = -StageLy / 2;                 break;
                default: u = -StageLx / 2;               v = -StageLy / 2 + StageLy * s;  break;
            }

            float tipZ = StageCarrierTop + StageHeight(u, v) + StageClearance;

            // X 滑块、Y 滑块（治具/物料）、Z 滑块（工具头）三轴联动
            cxDisplay1.Groups.SetTransform(_stageGroupX, CxMatrix4X4.Translation(u, 0f, 0f));
            cxDisplay1.Groups.SetTransform(_stageGroupY, CxMatrix4X4.Translation(0f, -v, 0f));
            cxDisplay1.Groups.SetTransform(_stageGroupZ, CxMatrix4X4.Translation(0f, 0f, tipZ - StageTipHomeZ));
        }

        /// <summary>
        /// Drives a simulation callback from a background timer instead of a
        /// <see cref="System.Windows.Forms.Timer"/>.
        /// </summary>
        /// <remarks>
        /// A WinForms Timer runs on <c>WM_TIMER</c>, the lowest-priority message in the queue.
        /// While any control repaints continuously — e.g. hovering a second CxDisplay, which
        /// invalidates once per mouse move — a <c>WM_PAINT</c> is essentially always pending,
        /// <c>WM_TIMER</c> never gets generated, and the animation freezes. A background timer is
        /// immune to that.
        /// <para>The callback runs on the UI thread, so it may touch controls directly.
        /// <c>BeginInvoke</c> work items have no backpressure, so at most one apply is queued at a
        /// time: a tick that arrives while the previous apply is still waiting is dropped, which
        /// coalesces the burst onto the latest state instead of building a backlog of stale poses.
        /// </para>
        /// </remarks>
        private sealed class SimDriver : IDisposable
        {
            private readonly Control _marshalTo;
            private readonly Action _apply;
            private readonly int _intervalMs;
            private readonly System.Threading.Timer _timer;

            // 0 = idle, 1 = an apply is queued or running on the UI thread.
            private int _queued;
            private bool _disposed;

            public SimDriver(Control marshalTo, int intervalMs, Action apply)
            {
                _marshalTo = marshalTo ?? throw new ArgumentNullException(nameof(marshalTo));
                _apply = apply ?? throw new ArgumentNullException(nameof(apply));
                _intervalMs = intervalMs;
                // Created one-shot: each tick re-arms itself, so a slow UI thread cannot build up
                // a backlog of timer callbacks on top of the coalescing below.
                _timer = new System.Threading.Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
            }

            /// <summary>Starts ticking every <c>intervalMs</c>.</summary>
            public void Start() => _timer.Change(0, Timeout.Infinite);

            private void OnTick(object state)
            {
                if (_disposed) return;

                // Drop this tick when the previous apply has not run yet.
                if (Interlocked.CompareExchange(ref _queued, 1, 0) != 0)
                {
                    Rearm();
                    return;
                }

                try
                {
                    _marshalTo.BeginInvoke(new Action(() =>
                    {
                        Interlocked.Exchange(ref _queued, 0);
                        if (_disposed || _marshalTo.IsDisposed) return;
                        _apply();
                    }));
                }
                catch (InvalidOperationException)
                {
                    // The handle can be destroyed between the check and the post.
                    Interlocked.Exchange(ref _queued, 0);
                    return;
                }

                Rearm();
            }

            private void Rearm()
            {
                if (_disposed) return;
                try { _timer.Change(_intervalMs, Timeout.Infinite); }
                catch (ObjectDisposedException) { /* disposed concurrently */ }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _timer.Dispose();
            }
        }

        private static CxMesh BuildBoxMesh(float w, float d, float h, CxPoint3D origin)
        {
            float x0 = origin.X - w / 2, x1 = origin.X + w / 2;
            float y0 = origin.Y - d / 2, y1 = origin.Y + d / 2;
            float z0 = origin.Z, z1 = origin.Z + h;
            var v = new[]
            {
                new CxPoint3D(x0, y0, z0), new CxPoint3D(x1, y0, z0),
                new CxPoint3D(x1, y1, z0), new CxPoint3D(x0, y1, z0),
                new CxPoint3D(x0, y0, z1), new CxPoint3D(x1, y0, z1),
                new CxPoint3D(x1, y1, z1), new CxPoint3D(x0, y1, z1),
            };
            var idx = new uint[]
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0,
            };
            return new CxMesh { Vertices = v, Indices = idx };
        }

        private static CxMesh BuildSphereMesh(float radius, int slices, int stacks)
        {
            var verts = new CxPoint3D[(stacks + 1) * (slices + 1)];
            for (int i = 0; i <= stacks; i++)
            {
                float phi = (float)(Math.PI * i / stacks);   // 0 at +Z pole
                float z = radius * (float)Math.Cos(phi);
                float r = radius * (float)Math.Sin(phi);
                for (int j = 0; j <= slices; j++)
                {
                    float theta = (float)(2 * Math.PI * j / slices);
                    verts[i * (slices + 1) + j] = new CxPoint3D(
                        r * (float)Math.Cos(theta),
                        r * (float)Math.Sin(theta),
                        z);
                }
            }

            var idx = new uint[stacks * slices * 6];
            int k = 0;
            for (int i = 0; i < stacks; i++)
                for (int j = 0; j < slices; j++)
                {
                    uint a = (uint)(i * (slices + 1) + j);
                    uint b = a + 1;
                    uint c = a + (uint)(slices + 1);
                    uint d = c + 1;
                    idx[k++] = a; idx[k++] = c; idx[k++] = b;   // CCW, outward
                    idx[k++] = b; idx[k++] = c; idx[k++] = d;
                }
            return new CxMesh { Vertices = verts, Indices = idx };
        }

        private static CxMesh BuildBumpyMesh(int nx, int ny, float spacing, CxPoint3D origin)
        {
            var verts = new CxPoint3D[nx * ny];
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    float fx = x * spacing, fy = y * spacing;
                    verts[y * nx + x] = new CxPoint3D(
                        origin.X + fx, origin.Y + fy,
                        origin.Z + 0.35f * (float)(Math.Sin(fx) * Math.Cos(fy)));
                }
            var idx = new uint[(nx - 1) * (ny - 1) * 6];
            int i = 0;
            for (int y = 0; y < ny - 1; y++)
                for (int x = 0; x < nx - 1; x++)
                {
                    uint tl = (uint)(y * nx + x), tr = tl + 1, bl = tl + (uint)nx, br = bl + 1;
                    // CCW seen from +Z, so face normals point up (matches ComputeGridNormals).
                    idx[i++] = tl; idx[i++] = tr; idx[i++] = bl;
                    idx[i++] = tr; idx[i++] = br; idx[i++] = bl;
                }
            return new CxMesh { Vertices = verts, Indices = idx };
        }

        // ── Lighting / PBR switch test ──────────────────────────────────────────

        private CxDisplay _cxDisplayPbr;

        private void BuildLightingTab()
        {
            var page = new TabPage("光照/PBR 测试") { Padding = new Padding(8) };
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 4,
            };

            // SplitterDistance can only be set once the container has a real width.
            bool splitInit = false;
            split.SizeChanged += (s, e) =>
            {
                if (!splitInit && split.Width > 400)
                {
                    splitInit = true;
                    split.SplitterDistance = 360;
                }
            };

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
            };

            // Controls that only take effect in PBR mode; greyed out while Legacy is selected.
            var pbrOnly = new List<Control>();

            Label Header(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(3, 10, 3, 2),
            };

            Control[] AddSlider(string name, int min, int max, int value, Action<float> onChanged)
            {
                var lbl = new Label { Text = $"{name}: {value / 100f:F2}", AutoSize = true };
                var track = new TrackBar
                {
                    Minimum = min,
                    Maximum = max,
                    Value = value,
                    TickFrequency = Math.Max(1, (max - min) / 6),
                    Width = 320,
                    Height = 34,
                };
                track.ValueChanged += (s, e) =>
                {
                    float v = track.Value / 100f;
                    lbl.Text = $"{name}: {v:F2}";
                    onChanged(v);
                };
                panel.Controls.Add(lbl);
                panel.Controls.Add(track);
                return new Control[] { lbl, track };
            }

            Button Btn(string text, Action click)
            {
                var b = new Button { Text = text, AutoSize = true };
                b.Click += (s, e) => click();
                return b;
            }

            // WinForms groups radio buttons by their parent container (there is no GroupName
            // property, unlike WPF), so each concept gets its own inner container to keep the two
            // groups independent instead of one mutually-exclusive set.
            FlowLayoutPanel RadioGroup(params Control[] children)
            {
                var g = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Margin = new Padding(0),
                };
                g.Controls.AddRange(children);
                return g;
            }

            panel.Controls.Add(Header("Mesh 光照模式（本页 + 两个主 CxDisplay）"));
            var rbLegacy = new RadioButton { Text = "Legacy（Blinn-Phong，默认）", AutoSize = true, Checked = true };
            var rbPbr = new RadioButton { Text = "PBR / IBL", AutoSize = true };
            panel.Controls.Add(RadioGroup(rbLegacy, rbPbr));

            panel.Controls.Add(Header("环境 / 材质 / 强度（仅 PBR 生效）"));
            var rbStudio = new RadioButton { Text = "Studio", AutoSize = true, Checked = true };
            var rbNeutral = new RadioButton { Text = "Neutral", AutoSize = true };
            rbStudio.CheckedChanged += (s, e) => { if (rbStudio.Checked) ApplyEnvironment(SurfaceEnvironmentPreset.Studio); };
            rbNeutral.CheckedChanged += (s, e) => { if (rbNeutral.Checked) ApplyEnvironment(SurfaceEnvironmentPreset.Neutral); };
            panel.Controls.Add(RadioGroup(rbStudio, rbNeutral));
            pbrOnly.Add(rbStudio);
            pbrOnly.Add(rbNeutral);

            // Initial slider positions mirror the library defaults (0.2 / 0.5 / 1.5).
            pbrOnly.AddRange(AddSlider("Metallic", 0, 100, 20, v => ForEachDisplay(d => d.PbrMetallic = v)));
            pbrOnly.AddRange(AddSlider("Roughness", 0, 100, 50, v => ForEachDisplay(d => d.PbrRoughness = v)));
            pbrOnly.AddRange(AddSlider("IBL 强度", 0, 300, 150, v => ForEachDisplay(d => d.IblIntensity = v)));

            var chkEnvFollows = new CheckBox { Text = "环境跟随相机（旋转视角时重新打光，仅 PBR）", AutoSize = true, Checked = false };
            chkEnvFollows.CheckedChanged += (s, e) => ForEachDisplay(d => d.EnvironmentFollowsCamera = chkEnvFollows.Checked);
            panel.Controls.Add(chkEnvFollows);
            pbrOnly.Add(chkEnvFollows);

            var chkImpostor = new CheckBox { Text = "点云伪 3D 高光球（Point Impostor，两种模式都生效）", AutoSize = true, Checked = true };
            chkImpostor.CheckedChanged += (s, e) => ForEachDisplay(d => d.PointImpostor = chkImpostor.Checked);
            panel.Controls.Add(chkImpostor);

            rbLegacy.CheckedChanged += (s, e) =>
            {
                if (!rbLegacy.Checked) return;
                ApplyLighting(SurfaceLightingMode.Legacy);
                foreach (var c in pbrOnly) c.Enabled = false;
            };
            rbPbr.CheckedChanged += (s, e) =>
            {
                if (!rbPbr.Checked) return;
                ApplyLighting(SurfaceLightingMode.Pbr);
                foreach (var c in pbrOnly) c.Enabled = true;
            };
            foreach (var c in pbrOnly) c.Enabled = false;   // starts in Legacy

            panel.Controls.Add(Header("本页预览（右侧 CxDisplay）"));
            panel.Controls.Add(Btn("Mesh 预览（斜置板）", LoadLightingMeshDemo));
            panel.Controls.Add(Btn("球体预览（看 IBL 最直观）", LoadLightingSphereDemo));
            panel.Controls.Add(Btn("点云预览", LoadLightingCloudDemo));
            panel.Controls.Add(Btn("加载文件…（Mesh / Surface）", LoadLightingFileDemo));

            panel.Controls.Add(Header("同步到主页面（3D 页两个 CxDisplay）"));
            panel.Controls.Add(Btn("两个都加载 Mesh", LoadLightingBothDemo));

            // Live preview control. A code-created OpenGLControl needs the ISupportInitialize
            // round-trip so its GL context is created (the designer normally emits this).
            _cxDisplayPbr = new CxDisplay { Dock = DockStyle.Fill, Size = new Size(600, 600) };
            var init = (System.ComponentModel.ISupportInitialize)_cxDisplayPbr;
            init.BeginInit();
            init.EndInit();
            _cxDisplayPbr.Camera.IsLeftHanded = true;
            _cxDisplayPbr.SurfaceMode = SurfaceMode.Mesh;
            _cxDisplayPbr.ShowCoordinateSystem = false;

            split.Panel1.Controls.Add(panel);
            split.Panel2.Controls.Add(_cxDisplayPbr);
            page.Controls.Add(split);
            tabControl1.TabPages.Add(page);

            LoadLightingMeshDemo();
        }

        private void ForEachDisplay(Action<CxDisplay> action)
        {
            action(cxDisplay1);
            action(cxDisplay2);
            if (_cxDisplayPbr != null) action(_cxDisplayPbr);
        }

        private void ApplyLighting(SurfaceLightingMode mode) => ForEachDisplay(d => d.SurfaceLightingMode = mode);

        private void ApplyEnvironment(SurfaceEnvironmentPreset preset) => ForEachDisplay(d => d.EnvironmentPreset = preset);

        private void LoadLightingMeshDemo()
        {
            if (_cxDisplayPbr == null) return;
            var mesh = BuildBumpyMesh(60, 60, 0.15f, new CxPoint3D(0, 0, 0));
            // Tilt the plate to a 3/4 orientation so its normals span a range of the environment
            // instead of all pointing straight up (which made the shading look flat).
            var tilt = CxMatrix4X4.RotationX(35f) * CxMatrix4X4.RotationY(25f);
            _cxDisplayPbr.SurfaceMode = SurfaceMode.Mesh;
            _cxDisplayPbr.ResetView();
            _cxDisplayPbr.SetMesh(mesh, pose: tilt);
            lbl_markPos.Text = "Mesh preview（斜置 3/4）-> 光照/PBR 页";
        }

        private void LoadLightingSphereDemo()
        {
            if (_cxDisplayPbr == null) return;
            _cxDisplayPbr.SurfaceMode = SurfaceMode.Mesh;
            _cxDisplayPbr.ResetView();
            _cxDisplayPbr.SetMesh(BuildSphereMesh(4f, 48, 32));
            lbl_markPos.Text = "Sphere preview -> 光照/PBR 页";
        }

        private void LoadLightingCloudDemo()
        {
            if (_cxDisplayPbr == null) return;
            const int W = 90, L = 90;
            const float xScale = 0.15f, yScale = 0.15f, zScale = 0.02f;
            var data = new short[W * L * 3];
            for (int y = 0; y < L; y++)
                for (int x = 0; x < W; x++)
                {
                    int idx = y * W + x;
                    data[idx * 3] = (short)x;
                    data[idx * 3 + 1] = (short)y;
                    data[idx * 3 + 2] = (short)(50 * Math.Sin(x * 0.15f) * Math.Cos(y * 0.15f));
                }
            var cloud = new CxPointCloud(W, L, data, null,
                xOffset: 0f, yOffset: 0f, zOffset: 0f,
                xScale: xScale, yScale: yScale, zScale: zScale);
            _cxDisplayPbr.SurfaceMode = SurfaceMode.PointCloud;
            _cxDisplayPbr.ResetView();
            _cxDisplayPbr.SetPointCloud(cloud);
            lbl_markPos.Text = "PointCloud preview -> 光照/PBR 页";
        }

        private void LoadLightingFileDemo()
        {
            if (_cxDisplayPbr == null) return;
            using (var dlg = new OpenFileDialog
            {
                Filter = "Mesh / Surface|*.obj;*.cxmesh;*.stl;*.stla;*.cxsurface"
                       + "|Mesh files|*.obj;*.cxmesh;*.stl;*.stla"
                       + "|Surface files|*.cxsurface"
                       + "|All files|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                if (ext == ".cxsurface")
                {
                    var loaded = VisionOperator.LoadSurface(dlg.FileName);
                    if (loaded == null) { MessageBox.Show("Failed to load surface.", "Load"); return; }
                    surface = loaded;
                    _cxDisplayPbr.SurfaceMode = SurfaceMode.Mesh;
                    _cxDisplayPbr.ResetView();
                    _cxDisplayPbr.SetSurface(loaded);
                    lbl_markPos.Text = "Surface loaded -> 预览";
                }
                else
                {
                    var mesh = VisionOperator.LoadMesh(dlg.FileName);
                    if (mesh == null) { MessageBox.Show("Failed to load mesh.", "Load"); return; }
                    _currentMesh = mesh;
                    _cxDisplayPbr.SurfaceMode = SurfaceMode.Mesh;
                    _cxDisplayPbr.ResetView();
                    _cxDisplayPbr.SetMesh(mesh);
                    lbl_markPos.Text = "Mesh loaded -> 预览";
                }
            }
        }

        private void LoadLightingBothDemo()
        {
            cxDisplay1.SurfaceMode = SurfaceMode.Mesh;
            cxDisplay1.ResetView();
            cxDisplay1.SetMesh(BuildBumpyMesh(50, 50, 0.2f, new CxPoint3D(0, 0, 0)));

            cxDisplay2.SurfaceMode = SurfaceMode.Mesh;
            cxDisplay2.ResetView();
            cxDisplay2.SetMesh(BuildBumpyMesh(50, 50, 0.2f, new CxPoint3D(0, 0, 0)));

            lbl_markPos.Text = "Mesh -> 两个主 CxDisplay";
        }
    }
}
