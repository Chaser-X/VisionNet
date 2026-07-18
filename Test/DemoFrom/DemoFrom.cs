using Lmi3d.GoSdk;
using Lmi3d.GoSdk.Messages;
using SharpGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
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
            Build2DTab(tabPage2D);
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
            VisionOperator.InitialLib();
            // cxDisplay1.SetViewUpDirection(new CxVector3D(0, 1, 0));
            cxDisplay1.SetCoordinateSystemLeftHanded(true);
            cxDisplay1.Camera.Enable2DView = false;
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
                cxDisplay1.SetPointCloudAdvancedItem(_pointCloud);
            }
            else
            {
                if (surface != null)
                    cxDisplay1.SetSurfaceAdvancedItem(surface);
                if (surface2 != null)
                    cxDisplay1.AddSurfaceAdvancedItem(surface2, CxMatrix4X4.RotationZ((float)Math.PI / 4) * CxMatrix4X4.Translation(5, 3, 2));
            }

            ////添加平面 Plane3D
            //var plane = new CxPlane3D(new CxPoint3D(0, 0, 0), new CxVector3D(1, 1, 1));
            //cxDisplay1.SetPlane(plane, Color.FromArgb(100, Color.Blue));

            //添加Box3D
            var box = new CxBox3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 10));
            //cxDisplay1.SetBox(new CxBox3D[] { box }, Color.FromArgb(100, Color.Yellow), 1);

            //cxDisplay1.SetPoint(new CxPoint3D[] { new CxPoint3D(2, 3, 1), new CxPoint3D(5, 1, 1) }, Color.Green, 10f, PointShape.Sphere);

            //添加Box3D
            cxDisplay2.ResetView();
            cxDisplay2.SetBox(new CxBox3D[] { box }, Color.Yellow, 10);
        }

        private void DemoFrom_FormClosing(object sender, FormClosingEventArgs e)
        {
            GocatorHandle.GocatorHandle.Instance.DisConnect();
            cxDisplay1.Dispose();
            cxDisplay2.Dispose();
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

            cxDisplay1.SetSurface(surfacemap);
        }

        private void btn_addSeg3D_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            //添加Segment3D线段
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
            //添加多边形
            List<CxPoint3D> pts = new List<CxPoint3D>();
            pts.Add(new CxPoint3D(0, 0, 0));
            pts.Add(new CxPoint3D(1, 0, 0));
            pts.Add(new CxPoint3D(1, 1, 0));
            pts.Add(new CxPoint3D(1, 1, 1));
            var polygon = new CxPolygon3D(pts.ToArray(), false);
            cxDisplay2.SetPolygon(new CxPolygon3D[] { polygon }, Color.Blue);

            //添加平面 Plane3D
            var plane = new CxPlane3D(new CxPoint3D(0, 0, 0), new CxVector3D(1, 1, 1));
            cxDisplay2.SetPlane(new CxPlane3D[] { plane }, Color.FromArgb(100, Color.Red));

            //添加Box3D
            var box = new CxBox3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 10));
            cxDisplay2.SetBox(new CxBox3D[] { box }, Color.FromArgb(100, Color.Yellow));

            //添加TextInfo
            var text = new CxTextInfo(new CxPoint3D(0, 0, 0), "this is a test中文!", 50);
            cxDisplay2.SetTextInfo(new CxTextInfo[] { text }, Color.Yellow);

            //添加Text2D
            var text2d = new CxText2D(new CxPoint2D(10, 50), "2D Labels", 10);
            cxDisplay2.SetText2D(new CxText2D[] { text2d }, Color.Green);
            cxDisplay2.ActivateAllItems();
        }

        private void btn_dragMark_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // 1. 程序生成平坦参考面（为深度缓冲提供有效值，并触发 FitView）
            const int W = 50, L = 50;
            var flatData = new short[W * L]; // 全零 = Z=0
            var flatSurf = new CxSurface(W, L, flatData, null,
                xOffset: 0, yOffset: 0, zOffset: 0,
                xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf); // 10×10 平面，FitView 自动对齐相机

            // 2. 放置可拖动 MARK 点（位于平面中心 5,5,0）
            var mark = cxDisplay2.SetPoint(
                new[] { new CxPoint3D(5.0f, 5.0f, 0f) }, Color.Red, 1f, PointShape.Sphere);
            mark.IsActiveObj = true;
            mark.HitThreshold = 2f;

            // 3. 订阅 OnChanged，实时更新坐标标签
            mark.OnChanged += item =>
            {
                var pos = ((CxPoint3DItem)item).Point3Ds[0];
                lbl_markPos.Text = $"X:{pos.X:F3}  Y:{pos.Y:F3}  Z:{pos.Z:F3}";
            };
        }

        private void btn_boxDemo_Click(object sender, EventArgs e)
        {
            cxDisplay2.ResetView();

            // 1. 平坦参考面（为深度缓冲提供有效值，触发 FitView）
            const int W = 60, L = 60;
            var flatData = new short[W * L];
            var flatSurf = new CxSurface(W, L, flatData, null,
                xOffset: 0, yOffset: 0, zOffset: 0,
                xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf);  // 12×12 平面

            // 2. 放置可拖拽 Box（位于平面中央）
            var initBox = new CxBox3D(new CxPoint3D(6.0f, 6.0f, 0.5f), new CxSize3D(4.0f, 3.0f, 1.0f));
            var box = cxDisplay2.SetBox(new[] { initBox }, Color.FromArgb(80, Color.Cyan));
            box.IsActiveObj = true;
            box.HitThreshold = 1f;

            // 3. 实时显示 Box 的 Center 和 Size
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

            // 参考平面（深度缓冲）
            const int W = 60, L = 60;
            var flatSurf = new CxSurface(W, L, new short[W * L], null,
                xOffset: 0, yOffset: 0, zOffset: 0, xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf);

            // 放置三条可拖拽线段
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

            // 参考平面（深度缓冲）
            const int W = 60, L = 60;
            var flatSurf = new CxSurface(W, L, new short[W * L], null,
                xOffset: 0, yOffset: 0, zOffset: 0, xScale: 0.2f, yScale: 0.2f, zScale: 0.001f);
            cxDisplay2.SetSurface(flatSurf);

            // 放置一个矩形多边形（闭合）和一条折线（开放）
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
            var matrix = CxMatrix4X4.RotationY((float)Math.PI / 4);
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

            var matrix = CxMatrix4X4.RotationY((float)Math.PI / 4);
            var result = VisionOperator.MeshToSurface(_currentMesh, matrix, new CxBox3D(new CxPoint3D(2, 4, 1.2f), new CxSize3D(15, 25, 2)), 0.01f, 0.01f);
            if (result == null) return;

            cxDisplay1.ResetView();
            cxDisplay1.SetSurfaceAdvancedItem(result);
        }

        private bool _poseApplied = false;

        private void btn_testPose_Click(object sender, EventArgs e)
        {
            _poseApplied = !_poseApplied;
            var pose = _poseApplied
                ? CxMatrix4X4.RotationZ((float)Math.PI / 4) * CxMatrix4X4.Translation(5, 3, 2)
                : CxMatrix4X4.Identity();

            cxDisplay1.SetSurfaceAdvancedItemPose(pose);
            cxDisplay1.SetPointCloudAdvancedItemPose(pose);
            cxDisplay1.SetMeshAdvancedItemPose(pose);

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
                    cxDisplay2.SetSurfaceAdvancedItem(loaded);
                    MessageBox.Show($"Surface saved → loaded\n{path}", "Save R/L");
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
                    MessageBox.Show($"Mesh saved → loaded (OBJ)\n{path}", "Save R/L");
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

        // ── 2D Tab ──────────────────────────────────────────────────────────────────

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

        // ── 2D Button Handlers ───────────────────────────────────────────────────────

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
                _cxDisplay2D.SetImage(img);
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
                var text = $"C:({b.Center.X:F0},{b.Center.Y:F0})\nS:({b.Size.Width:F0}×{b.Size.Height:F0})";
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

        // ── Image Loading Helper ─────────────────────────────────────────────────────

        private CxImage LoadColorImage(string path)
        {
            try
            {
                _cxDisplay2D.ResetView();
                 _cxDisplay2D.SetScaleAndOffset(new CxPoint3D(0.02f, 0.02f, 1f), new CxPoint3D(-40f,-80f,0));
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
                MessageBox.Show($"图片加载失败: {ex.Message}", "Load Image");
                return null;
            }
        }
    }
}