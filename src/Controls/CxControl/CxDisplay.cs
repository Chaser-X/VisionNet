using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public partial class CxDisplay : OpenGLControl, IDisposable
    {
        private CxAdvancedTrackBallCamera camera;
        private bool isMouseDown = false;

        // GL 资源池：CxDisplay 持有所有 GL 资源，Item 只持有 CPU 数据
        private readonly Dictionary<ICxObjRenderItem, GLResourceHandle> _resourcePool
            = new Dictionary<ICxObjRenderItem, GLResourceHandle>();
        private readonly object _resourceLock = new object();

        // 待释放的 GL 资源队列（线程安全，在下一帧 GL 上下文中释放）
        private readonly ConcurrentQueue<GLResourceHandle> _pendingRelease
            = new ConcurrentQueue<GLResourceHandle>();

        private readonly List<ICxObjRenderItem> _surfaceItems = new List<ICxObjRenderItem>();
        private CxCoordinateSystemItem coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem colorBarItem = new CxColorBarItem();
        private CxCoordinationTagItem coorTagItem = new CxCoordinationTagItem();
        private readonly List<IRenderItem> renderItem = new List<IRenderItem>();

        public CxAdvancedTrackBallCamera Camera => camera;

        public ViewMode SurfaceViewMode
        {
            get => camera.ViewMode;
            set
            {
                if (camera.ViewMode != value)
                {
                    camera.ViewMode = value;
                    updataMenuItem();
                }
            }
        }

        private SurfaceMode pSufaceMode = SurfaceMode.PointCloud;
        public SurfaceMode SurfaceMode
        {
            get => pSufaceMode;
            set
            {
                if (pSufaceMode != value) { pSufaceMode = value; updataMenuItem(); }
                List<ICxObjRenderItem> snapshot;
                lock (_resourceLock) snapshot = new List<ICxObjRenderItem>(_surfaceItems);
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceMode = value;
            }
        }

        private SurfaceColorMode pSurfaceColorMode = SurfaceColorMode.ColorWithIntensity;
        public SurfaceColorMode SurfaceColorMode
        {
            get => pSurfaceColorMode;
            set
            {
                if (pSurfaceColorMode != value) { pSurfaceColorMode = value; updataMenuItem(); }
                List<ICxObjRenderItem> snapshot;
                lock (_resourceLock) snapshot = new List<ICxObjRenderItem>(_surfaceItems);
                foreach (var item in snapshot)
                    if (!item.IsDisposed) item.SurfaceColorMode = value;
            }
        }

        public bool ShowCoordinateSystem { get; set; } = false;

        public CxDisplay() : this(ViewMode.Top, SurfaceMode.PointCloud, SurfaceColorMode.ColorWithIntensity) { }

        public CxDisplay(ViewMode viewMode = ViewMode.Top,
            SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.ColorWithIntensity)
        {
            if (!DesignMode)
            {
                InitializeComponent();
                camera = new CxAdvancedTrackBallCamera(this);
                camera.ViewMode = viewMode;
                SurfaceMode = surfaceMode;
                SurfaceColorMode = surfaceColorMode;
                updataMenuItem();
            }
        }

        private void updataMenuItem()
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => updataMenuItem())); return; }

            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == camera.ViewMode.ToString();
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == pSufaceMode.ToString();
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = ((ToolStripMenuItem)item).Text == pSurfaceColorMode.ToString();
        }

        #region 设置主渲染对象

        private Box3D? GetCombinedBoundingBox()
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var item in _surfaceItems)
            {
                var bb = item.BoundingBox;
                if (bb == null) continue;
                float x0 = bb.Value.Center.X - bb.Value.Size.Width  / 2f;
                float x1 = bb.Value.Center.X + bb.Value.Size.Width  / 2f;
                float y0 = bb.Value.Center.Y - bb.Value.Size.Height / 2f;
                float y1 = bb.Value.Center.Y + bb.Value.Size.Height / 2f;
                float z0 = bb.Value.Center.Z - bb.Value.Size.Depth  / 2f;
                float z1 = bb.Value.Center.Z + bb.Value.Size.Depth  / 2f;
                if (x0 < minX) minX = x0; if (x1 > maxX) maxX = x1;
                if (y0 < minY) minY = y0; if (y1 > maxY) maxY = y1;
                if (z0 < minZ) minZ = z0; if (z1 > maxZ) maxZ = z1;
            }

            if (minX == float.MaxValue) return null;
            return new Box3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        private void ReplaceSurfaceItem(ICxObjRenderItem newItem)
        {
            lock (_resourceLock)
            {
                foreach (var old in _surfaceItems)
                {
                    if (_resourcePool.TryGetValue(old, out var h))
                    {
                        _pendingRelease.Enqueue(h);
                        _resourcePool.Remove(old);
                    }
                    old.OnRenderDataChanged -= OnItemRenderDataChanged;
                    old.Dispose();
                }
                _surfaceItems.Clear();

                _surfaceItems.Add(newItem);
                _resourcePool[newItem] = new GLResourceHandle { IsValid = false, NeedsUpdate = true };
                newItem.OnRenderDataChanged += OnItemRenderDataChanged;
            }

            camera.FitView(newItem.BoundingBox);
            Invalidate();
        }

        private void AppendSurfaceItem(ICxObjRenderItem newItem)
        {
            Box3D? combined;
            lock (_resourceLock)
            {
                _surfaceItems.Add(newItem);
                _resourcePool[newItem] = new GLResourceHandle { IsValid = false, NeedsUpdate = true };
                newItem.OnRenderDataChanged += OnItemRenderDataChanged;
                combined = GetCombinedBoundingBox();
            }

            camera.FitView(combined);
            Invalidate();
        }

        private void OnItemRenderDataChanged()
        {
            lock (_resourceLock)
            {
                foreach (var item in _surfaceItems)
                {
                    if (_resourcePool.TryGetValue(item, out var handle))
                        handle.NeedsUpdate = true;
                }
            }
            Invalidate();
        }

        public void SetPointCloud(CxSurface inpointCloud)
        {
            var surface = inpointCloud;
            if (inpointCloud.Width * inpointCloud.Length > 100_000_000)
            {
                var points = inpointCloud.ToPoints();
                float ratio  = points.Length / 10_000_000f;
                surface = VisionOperator.UniformSuface(points, inpointCloud.Intensity,
                    (int)(inpointCloud.Width / ratio), (int)(inpointCloud.Length / ratio),
                    inpointCloud.XScale * ratio, inpointCloud.YScale * ratio,
                    inpointCloud.ZScale, inpointCloud.XOffset, inpointCloud.YOffset, inpointCloud.ZOffset);
            }
            ReplaceSurfaceItem(new CxSurfaceItem(surface, SurfaceMode, SurfaceColorMode));
        }

        public void SetMesh(CxMesh mesh)
        {
            ReplaceSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));
        }

        public void SetSurfaceAdvancedItem(CxSurface surface)
        {
            ReplaceSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_0000));
        }

        public void SetMeshAdvancedItem(CxMesh mesh)
        {
            ReplaceSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));
        }

        public void SetSegment(Segment3D[] segment, Color color, float size = 1.0f)
        {
            renderItem.Add(new CxSegment3DItem(segment, color, size));
            Invalidate();
        }

        public void SetPoint(CxPoint3D[] point, Color color, float size = 1.0f, PointShape shape = PointShape.Point)
        {
            renderItem.Add(new CxPoint3DItem(point, color, size, shape));
            Invalidate();
        }

        public void SetPolygon(Polygon3D[] polygon, Color color, float size = 1.0f)
        {
            renderItem.Add(new CxPolygon3DItem(polygon, color, size));
            Invalidate();
        }

        public void SetPlane(Plane3D[] plane, Color color, float size = 100.0f)
        {
            renderItem.Add(new CxPlane3DItem(plane, color, size));
            Invalidate();
        }

        public void SetBox(Box3D[] box, Color color, float size = 1.0f)
        {
            renderItem.Add(new CxBox3DItem(box, color, size));
            Invalidate();
        }

        public void SetTextInfo(TextInfo[] textInfo, Color color)
        {
            renderItem.Add(new CxTextInfoItem(textInfo, color, 1));
            Invalidate();
        }

        public void SetText2D(Text2D[] text2Ds, Color color)
        {
            renderItem.Add(new CxText2DItem(text2Ds, color, 1));
            Invalidate();
        }

        public void SetCoordinate3DSystem(CxCoordination3D? coordination = null, float axisLength = 5)
        {
            if (!coordination.HasValue)
                coordination = new CxCoordination3D
                {
                    Origin = new CxPoint3D(0, 0, 0),
                    XAxis = new CxVector3D(1, 0, 0),
                    YAxis = new CxVector3D(0, 1, 0),
                    ZAxis = new CxVector3D(0, 0, 1),
                };
            renderItem.Add(new CxCoordinateSystemItem(
                axisLength, axisLength / 50, axisLength / 10, axisLength / 25, coordination));
            Invalidate();
        }

        public void AddPointCloud(CxSurface inpointCloud)
        {
            var surface = inpointCloud;
            if (inpointCloud.Width * inpointCloud.Length > 100_000_000)
            {
                var points = inpointCloud.ToPoints();
                float ratio = points.Length / 10_000_000f;
                surface = VisionOperator.UniformSuface(points, inpointCloud.Intensity,
                    (int)(inpointCloud.Width / ratio), (int)(inpointCloud.Length / ratio),
                    inpointCloud.XScale * ratio, inpointCloud.YScale * ratio,
                    inpointCloud.ZScale, inpointCloud.XOffset, inpointCloud.YOffset, inpointCloud.ZOffset);
            }
            AppendSurfaceItem(new CxSurfaceItem(surface, SurfaceMode, SurfaceColorMode));
        }

        public void AddMesh(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));

        public void AddSurfaceAdvancedItem(CxSurface surface)
            => AppendSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_000));

        public void AddMeshAdvancedItem(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));

        public void AddSurfaceItem(ICxObjRenderItem item)
            => AppendSurfaceItem(item);

        public void ClearSurfaceItems()
        {
            lock (_resourceLock)
            {
                foreach (var old in _surfaceItems)
                {
                    if (_resourcePool.TryGetValue(old, out var h))
                    {
                        _pendingRelease.Enqueue(h);
                        _resourcePool.Remove(old);
                    }
                    old.OnRenderDataChanged -= OnItemRenderDataChanged;
                    old.Dispose();
                }
                _surfaceItems.Clear();
            }
            Invalidate();
        }

        #endregion

        #region 渲染

        protected override void DoOpenGLInitialized()
        {
            base.DoOpenGLInitialized();
            OpenGL.ClearColor(0, 0, 0, 0);
            OpenGL.PointSize(2.0f);
        }

        protected override void DoOpenGLDraw(RenderEventArgs e)
        {
            if (DesignMode) return;
            base.DoOpenGLDraw(e);

            var gl = OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            // 1. 在 GL 上下文中释放旧资源
            ProcessPendingRelease(gl);

            // 2. 为新/变更的 Item 创建 GL 资源（全程持锁，防止 Dispose 并发）
            ProcessResourcePool(gl);

            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LESS);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LoadIdentity();

            camera.LookAtMatrix(gl);
            Render(gl);

            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Disable(OpenGL.GL_BLEND);
        }

        private void ProcessPendingRelease(OpenGL gl)
        {
            while (_pendingRelease.TryDequeue(out var handle))
                ReleaseGLResources(gl, handle);
        }

        /// <summary>
        /// 全程持锁：防止遍历期间 ReplaceSurfaceItem 在另一线程 Dispose 掉正在创建资源的 Item。
        /// </summary>
        private void ProcessResourcePool(OpenGL gl)
        {
            lock (_resourceLock)
            {
                foreach (var kv in _resourcePool)
                {
                    var item   = kv.Key;
                    var handle = kv.Value;

                    if (item.IsDisposed) continue;

                    if (!handle.IsValid || handle.NeedsUpdate)
                    {
                        if (handle.IsValid)
                            ReleaseGLResources(gl, handle);

                        CreateGLResources(gl, item, handle);
                    }
                }
            }
        }

        private void Render(OpenGL gl)
        {
            if (!camera.Enable2DView && ShowCoordinateSystem)
                coordinationItem.Draw(gl);

            List<ICxObjRenderItem> snapshot;
            lock (_resourceLock)
                snapshot = new List<ICxObjRenderItem>(_surfaceItems);

            float globalZMin = float.MaxValue, globalZMax = float.MinValue;
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;
                if (cur.SurfaceColorMode != SurfaceColorMode.Intensity)
                {
                    if (cur.ZMin < globalZMin) globalZMin = cur.ZMin;
                    if (cur.ZMax > globalZMax) globalZMax = cur.ZMax;
                }
            }

            if (globalZMin < globalZMax)
            {
                foreach (var cur in snapshot)
                {
                    if (cur != null && !cur.IsDisposed)
                        cur.SetGlobalZRange(globalZMin, globalZMax);
                }
            }

            bool anyDrawn = false;
            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                GLResourceHandle handle;
                lock (_resourceLock)
                    _resourcePool.TryGetValue(cur, out handle);

                if (handle?.IsValid != true) continue;

                cur.Draw(gl, handle);
                anyDrawn = true;
            }

            if (anyDrawn && globalZMin < globalZMax)
            {
                colorBarItem.SetRange(globalZMin, globalZMax);
                colorBarItem.Draw(gl);
            }

            if (anyDrawn)
                coorTagItem.Draw(gl);

            var items = renderItem.ToArray();
            foreach (var item in items)
                item.Draw(gl);

            if (!camera.Enable2DView)
                coordinationItem.DrawScreenPositionedAxes(gl);
        }

        #endregion

        #region GL 资源管理

        private void CreateGLResources(OpenGL gl, ICxObjRenderItem item, GLResourceHandle handle)
        {
            var data = item.PrepareRenderData();
            if (data == null) return;

            var tempId = new uint[1];

            if (data.UseVAO)
            {
                gl.GenVertexArrays(1, tempId);
                handle.VaoId = tempId[0];
                handle.HasVAO = true;
                gl.BindVertexArray(handle.VaoId);
            }

            int vboIndex = 0;

            if (data.Vertices != null)
            {
                gl.GenBuffers(1, tempId);
                handle.VboIds[vboIndex] = tempId[0];
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[vboIndex]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, data.Vertices, OpenGL.GL_STATIC_DRAW);
                if (data.UseVAO)
                {
                    gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
                    gl.EnableVertexAttribArray(0);
                }
                vboIndex++;
            }

            if (data.Colors != null)
            {
                gl.GenBuffers(1, tempId);
                handle.VboIds[vboIndex] = tempId[0];
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[vboIndex]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, data.Colors, OpenGL.GL_STATIC_DRAW);
                if (data.UseVAO)
                {
                    gl.VertexAttribPointer(1, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
                    gl.EnableVertexAttribArray(1);
                }
                vboIndex++;
            }
            else if (data.UVCoords != null)
            {
                gl.GenBuffers(1, tempId);
                handle.VboIds[vboIndex] = tempId[0];
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, handle.VboIds[vboIndex]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, data.UVCoords, OpenGL.GL_STATIC_DRAW);
                if (data.UseVAO)
                {
                    gl.VertexAttribPointer(1, 2, OpenGL.GL_FLOAT, false, 2 * sizeof(float), IntPtr.Zero);
                    gl.EnableVertexAttribArray(1);
                }
                vboIndex++;
            }

            handle.VboCount = vboIndex;

            if (data.Indices != null && data.Indices.Length > 0)
            {
                gl.GenBuffers(1, tempId);
                handle.ElementBufferId = tempId[0];
                handle.HasEBO = true;

                gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, handle.ElementBufferId);
                int bytes = data.Indices.Length * sizeof(uint);
                IntPtr ptr = Marshal.AllocHGlobal(bytes);
                try
                {
                    var indexBytes = new byte[bytes];
                    Buffer.BlockCopy(data.Indices, 0, indexBytes, 0, bytes);
                    Marshal.Copy(indexBytes, 0, ptr, bytes);
                    gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, bytes, ptr, OpenGL.GL_STATIC_DRAW);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            if (data.ShaderSource != null)
            {
                handle.ShaderProgram = CompileShader(gl, data.ShaderSource);
                handle.HasShader = true;
            }

            if (data.TextureData?.Data != null)
            {
                handle.TextureId = CreateTexture(gl, data.TextureData);
                handle.HasTexture = true;
            }

            if (handle.HasVAO)
                gl.BindVertexArray(0);

            handle.IsValid = true;
            handle.NeedsUpdate = false;
        }

        private void ReleaseGLResources(OpenGL gl, GLResourceHandle handle)
        {
            if (handle.VboCount > 0)
            {
                gl.DeleteBuffers(handle.VboCount, handle.VboIds);
                for (int i = 0; i < handle.VboIds.Length; i++) handle.VboIds[i] = 0;
                handle.VboCount = 0;
            }

            if (handle.HasVAO && handle.VaoId != 0)
            {
                gl.DeleteVertexArrays(1, new[] { handle.VaoId });
                handle.VaoId = 0;
                handle.HasVAO = false;
            }

            if (handle.HasEBO && handle.ElementBufferId != 0)
            {
                gl.DeleteBuffers(1, new[] { handle.ElementBufferId });
                handle.ElementBufferId = 0;
                handle.HasEBO = false;
            }

            if (handle.HasShader && handle.ShaderProgram != 0)
            {
                gl.DeleteProgram(handle.ShaderProgram);
                handle.ShaderProgram = 0;
                handle.HasShader = false;
            }

            if (handle.HasTexture && handle.TextureId != 0)
            {
                gl.DeleteTextures(1, new[] { handle.TextureId });
                handle.TextureId = 0;
                handle.HasTexture = false;
            }

            handle.IsValid = false;
        }

        private uint CompileShader(OpenGL gl, ShaderSource source)
        {
            uint vs = gl.CreateShader(OpenGL.GL_VERTEX_SHADER);
            gl.ShaderSource(vs, source.VertexSource);
            gl.CompileShader(vs);
            LogShaderError(gl, vs, "vertex");

            uint fs = gl.CreateShader(OpenGL.GL_FRAGMENT_SHADER);
            gl.ShaderSource(fs, source.FragmentSource);
            gl.CompileShader(fs);
            LogShaderError(gl, fs, "fragment");

            uint prog = gl.CreateProgram();
            gl.AttachShader(prog, vs);
            gl.AttachShader(prog, fs);
            gl.LinkProgram(prog);

            int[] status = new int[1];
            gl.GetProgram(prog, OpenGL.GL_LINK_STATUS, status);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] len = new int[1];
                gl.GetProgram(prog, OpenGL.GL_INFO_LOG_LENGTH, len);
                var log = new StringBuilder(len[0]);
                gl.GetProgramInfoLog(prog, len[0], IntPtr.Zero, log);
                Debug.WriteLine("Shader link error: " + log);
            }

            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            return prog;
        }

        private void LogShaderError(OpenGL gl, uint shader, string stage)
        {
            int[] status = new int[1];
            gl.GetShader(shader, OpenGL.GL_COMPILE_STATUS, status);
            if (status[0] != OpenGL.GL_FALSE) return;
            int[] len = new int[1];
            gl.GetShader(shader, OpenGL.GL_INFO_LOG_LENGTH, len);
            var log = new StringBuilder(len[0]);
            gl.GetShaderInfoLog(shader, len[0], IntPtr.Zero, log);
            Debug.WriteLine($"{stage} shader error: {log}");
        }

        private uint CreateTexture(OpenGL gl, TextureData tex)
        {
            // 查询 GPU 最大纹理尺寸，超出时降采样，否则直接使用原始数据
            int[] maxSizeArr = new int[1];
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, maxSizeArr);
            int maxSize = maxSizeArr[0];

            int uploadW = Math.Min(tex.Width,  maxSize);
            int uploadH = Math.Min(tex.Height, maxSize);
            byte[] uploadData = (uploadW != tex.Width || uploadH != tex.Height)
                ? DownsampleTextureRGBA(tex.Data, tex.Width, tex.Height, uploadW, uploadH)
                : tex.Data;

            var ids = new uint[1];
            gl.GenTextures(1, ids);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, ids[0]);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);

            IntPtr ptr = Marshal.AllocHGlobal(uploadData.Length);
            try
            {
                Marshal.Copy(uploadData, 0, ptr, uploadData.Length);
                gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA,
                    uploadW, uploadH, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            }

            return ids[0];
        }

        /// <summary>
        /// 对 RGBA（4字节/像素）纹理数据做最近邻降采样。
        /// UV 坐标为归一化 [0,1]，纹理尺寸缩小不影响映射正确性。
        /// </summary>
        private static byte[] DownsampleTextureRGBA(
            byte[] src, int srcW, int srcH, int dstW, int dstH)
        {
            float scaleX = (float)srcW / dstW;
            float scaleY = (float)srcH / dstH;
            var dst = new byte[dstW * dstH * 4];

            for (int y = 0; y < dstH; y++)
            {
                int srcY = Math.Min((int)(y * scaleY), srcH - 1);
                for (int x = 0; x < dstW; x++)
                {
                    int srcX = Math.Min((int)(x * scaleX), srcW - 1);
                    int si = (srcY * srcW + srcX) * 4;
                    int di = (y    * dstW + x)    * 4;
                    dst[di]     = src[si];
                    dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2];
                    dst[di + 3] = src[si + 3];
                }
            }

            return dst;
        }

        #endregion

        #region 视图操作

        public void ResetView(bool resetAll = true)
        {
            renderItem.ForEach(item => item.Dispose());
            renderItem.Clear();

            coordinationItem = new CxCoordinateSystemItem();
            coorTagItem = new CxCoordinationTagItem();
            colorBarItem = new CxColorBarItem();

            if (resetAll)
                ClearSurfaceItems();

            Invalidate();
        }

        public void SetViewCenter(CxPoint3D center)
        {
            camera.FocusOnPoint(new Vector3(center.X, center.Y, center.Z));
            Invalidate();
        }

        public void SetViewUpDirection(CxVector3D upDirection)
        {
            camera.SetDefaultUpView(new Vector3(upDirection.X, upDirection.Y, upDirection.Z));
        }

        protected override void DoGDIDraw(RenderEventArgs e) => base.DoGDIDraw(e);

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            camera?.LookAtMatrix(OpenGL);
        }

        #endregion

        #region 鼠标交互

        protected override void OnMouseDown(MouseEventArgs e)
        {
            isMouseDown = true;
            camera.RotationPoint = GetNearestSurfacePoint(e.X, e.Y).Location;
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            isMouseDown = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var pos = GetNearestSurfacePoint(e.X, e.Y);
            if (pos.Location.HasValue && !isMouseDown)
            {
                coorTagItem.Visible = true;
                coorTagItem.SetCoordinates(pos.Location.Value, pos.Intensity);
            }
            else
            {
                coorTagItem.Visible = false;
            }
            base.OnMouseMove(e);
        }

        private CxPoint3D? ScreenToWorldCoordinate(int mouseX, int mouseY)
        {
            var gl = OpenGL;
            int[] viewport = new int[4];
            gl.GetInteger(OpenGL.GL_VIEWPORT, viewport);
            int adjustedY = viewport[3] - mouseY;

            byte[] depthBuffer = new byte[4];
            gl.ReadPixels(mouseX, adjustedY, 1, 1, OpenGL.GL_DEPTH_COMPONENT, OpenGL.GL_FLOAT, depthBuffer);
            float depth = BitConverter.ToSingle(depthBuffer, 0);

            if (Math.Abs(depth - 1.0f) < 0.00001f) return null;

            var obj = gl.UnProject(mouseX, adjustedY, depth);
            return new CxPoint3D((float)obj[0], (float)obj[1], (float)obj[2]);
        }

        private (CxPoint3D? Location, byte? Intensity) GetNearestSurfacePoint(int mouseX, int mouseY)
        {
            var pos = ScreenToWorldCoordinate(mouseX, mouseY);
            if (!pos.HasValue) return (null, null);
            var world = pos.Value;

            List<ICxObjRenderItem> snapshot;
            lock (_resourceLock)
                snapshot = new List<ICxObjRenderItem>(_surfaceItems);

            if (snapshot.Count == 0) return (null, null);

            CxPoint3D? bestPoint = null;
            byte? bestIntensity = null;
            float bestDist = float.MaxValue;

            foreach (var cur in snapshot)
            {
                if (cur == null || cur.IsDisposed) continue;

                if (cur is CxMeshItem || cur is CxMeshAdvancedItem)
                {
                    if (0f < bestDist) { bestDist = 0f; bestPoint = world; bestIntensity = null; }
                    continue;
                }

                CxSurface surface = null;
                if      (cur is CxSurfaceItem        si) surface = si.Surface;
                else if (cur is CxSurfaceAdvancedItem ai) surface = ai.Surface;
                if (surface == null) continue;

                if (surface.Type != SurfaceType.Surface)
                {
                    if (0f < bestDist) { bestDist = 0f; bestPoint = world; bestIntensity = null; }
                    continue;
                }

                int xi = (int)((world.X - surface.XOffset) / surface.XScale);
                int yi = (int)((world.Y - surface.YOffset) / surface.YScale);
                if (xi < 0 || xi >= surface.Width || yi < 0 || yi >= surface.Length) continue;

                float threshold = 5 * (surface.XScale * surface.XScale
                                     + surface.YScale * surface.YScale
                                     + surface.ZScale * surface.ZScale);

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = xi + dx, ny = yi + dy;
                        if (nx < 0 || nx >= surface.Width || ny < 0 || ny >= surface.Length) continue;

                        int idx = ny * surface.Width + nx;
                        float z = surface.Data[idx] == -32768
                            ? float.NegativeInfinity
                            : surface.ZOffset + surface.Data[idx] * surface.ZScale;
                        if (float.IsInfinity(z)) continue;

                        float x = surface.XOffset + nx * surface.XScale;
                        float y = surface.YOffset + ny * surface.YScale;
                        float d = (x - world.X) * (x - world.X)
                                + (y - world.Y) * (y - world.Y)
                                + (z - world.Z) * (z - world.Z);

                        if (d < bestDist && d < threshold)
                        {
                            bestDist = d;
                            bestPoint = new CxPoint3D(x, y, z);
                            bestIntensity = (surface.Intensity != null && surface.Intensity.Length > idx)
                                ? surface.Intensity[idx] : (byte?)null;
                        }
                    }
                }
            }

            return (bestPoint, bestIntensity);
        }

        #endregion

        #region 菜单事件

        private void d2DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = !mi.Checked;
            camera.Enable2DView = mi.Checked;
            Box3D? combined;
            lock (_resourceLock) combined = GetCombinedBoundingBox();
            camera?.FitView(combined);
        }

        private void toolStripMenuItem_ViewModeClick(object sender, EventArgs e)
        {
            foreach (var item in viewModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            camera.ViewMode = (ViewMode)Enum.Parse(typeof(ViewMode), mi.Text);
            Box3D? combined;
            lock (_resourceLock) combined = GetCombinedBoundingBox();
            camera?.FitView(combined);
        }

        private void toolStripMenuItem_SurfaceModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceMode = (SurfaceMode)Enum.Parse(typeof(SurfaceMode), mi.Text);
        }

        private void toolStripMenuItem_SurfaceColorModeClick(object sender, EventArgs e)
        {
            foreach (var item in surfaceColorModeToolStripMenuItem.DropDownItems)
                ((ToolStripMenuItem)item).Checked = false;
            var mi = (ToolStripMenuItem)sender;
            mi.Checked = true;
            SurfaceColorMode = (SurfaceColorMode)Enum.Parse(typeof(SurfaceColorMode), mi.Text);
        }

        #endregion
    }
}
