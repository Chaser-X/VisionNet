using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SharpGL;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxDisplay : OpenGLControl
    {
        private CxCamera camera;

        // 渲染数据
        private CxSurfaceItem surfaceItem = null;
        private CxCoordinateSystemItem coordinationItem = new CxCoordinateSystemItem();
        private CxColorBarItem colorBarItem = new CxColorBarItem();
        private ContextMenuStrip menu_right;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem autoFitToolStripMenuItem;
        private ToolStripMenuItem viewModeToolStripMenuItem;
        private ToolStripMenuItem pointCloudToolStripMenuItem;
        private ToolStripMenuItem meshToolStripMenuItem;
        private ToolStripMenuItem heightMapToolStripMenuItem;
        private ToolStripMenuItem intensityToolStripMenuItem;
        private ToolStripMenuItem lineWidthToolStripMenuItem;
        private List<IRenderItem> renderItem = new List<IRenderItem>();

        public CxDisplay()
        {
            if (!DesignMode)
            {
                camera = new CxCamera(this);
                InitializeComponent();
            }
        }

        #region 操作方法
        /// <summary>
        /// 设置点云数据并自适应Z显示范围
        /// </summary>
        public void SetPointCloud(CxSurface inpointCloud, SurfaceMode surfaceMode)
        {
            var tempSuface = new CxSurface();
            var size = inpointCloud.Width * inpointCloud.Length;
            if (size > 6000000)
            {
                var points = inpointCloud.ToPoints();
                var ratio = points.Length / 6000000.0f;
                var width = inpointCloud.Width / ratio;
                var height = inpointCloud.Length / ratio;
                var xScale = inpointCloud.XScale * ratio;
                var yScale = inpointCloud.YScale * ratio;
                tempSuface = VisionOperator.UniformSuface(points, inpointCloud.Intensity, (int)width, (int)height, xScale, yScale, inpointCloud.ZScale, inpointCloud.XOffset, inpointCloud.YOffset, inpointCloud.ZOffset);
            }
            else
            {
                tempSuface = inpointCloud;
            }

            surfaceItem = new CxSurfaceItem(tempSuface ?? new CxSurface(), surfaceMode);
            camera.FitView(surfaceItem.BoundingBox); // 调整视图以适应点云数据
        }
        /// <summary>
        /// 添加线段
        /// </summary>
        public void SetSegment(Segment3D segment, Color color)
        {
            var segmentItem = renderItem.Find(x => x.GetType() == typeof(CxSegment3DItem));
            if (segmentItem == null)
            {
                segmentItem = new CxSegment3DItem(new Dictionary<Segment3D, Color> { { segment, color } });
                renderItem.Add(segmentItem);
            }
            else
            {
                var cxsegmentItem = segmentItem as CxSegment3DItem;
                if (cxsegmentItem.SegmentColors.ContainsKey(segment))
                {
                    cxsegmentItem.SegmentColors[segment] = color;
                }
                else
                {
                    cxsegmentItem.SegmentColors.Add(segment, color);
                }
            }
        }
        //添加点
        public void SetPoint(CxPoint3D point, Color color)
        {
            var pointItem = renderItem.Find(x => x.GetType() == typeof(CxPoint3DItem));
            if (pointItem == null)
            {
                pointItem = new CxPoint3DItem(new Dictionary<CxPoint3D, Color> { { point, color } });
                renderItem.Add(pointItem);
            }
            else
            {
                var cxpointItem = pointItem as CxPoint3DItem;
                if (cxpointItem.PointColors.ContainsKey(point))
                {
                    cxpointItem.PointColors[point] = color;
                }
                else
                {
                    cxpointItem.PointColors.Add(point, color);
                }
            }
        }
        //添加多边形
        public void SetPolygon(Polygon3D polygon, Color color)
        {
            var polygonItem = renderItem.Find(x => x.GetType() == typeof(CxPolygon3DItem));
            if (polygonItem == null)
            {
                polygonItem = new CxPolygon3DItem(new Dictionary<Polygon3D, Color> { { polygon, color } });
                renderItem.Add(polygonItem);
            }
            else
            {
                var cxpolygonItem = polygonItem as CxPolygon3DItem;
                if (cxpolygonItem.PolygonColors.ContainsKey(polygon))
                {
                    cxpolygonItem.PolygonColors[polygon] = color;
                }
                else
                {
                    cxpolygonItem.PolygonColors.Add(polygon, color);
                }
            }
        }
        /// <summary>
        /// 添加3D文本
        /// </summary>
        public void SetText(TextInfo text)
        {
            if (string.IsNullOrEmpty(text.Text)) return;
            //textInfos.Add(text);
        }
        #endregion
        /// <summary>
        /// 可视化元素渲染
        /// </summary>
        private void Render(OpenGL gl)
        {
            coordinationItem.DrawScreenPositionedAxes(gl);
            coordinationItem.Draw(gl);
            surfaceItem?.Draw(gl);
            if (surfaceItem != null &&
                ((int)surfaceItem.SurfaceMode & (int)SurfaceMode.HeightMap) == (int)SurfaceMode.HeightMap)
            {
                colorBarItem.SetRange(surfaceItem.ZMin, surfaceItem.ZMax);
                colorBarItem.Draw(gl);
            }
            foreach (var item in renderItem)
            {
                item.Draw(gl);
            }
        }

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
            OpenGL gl = OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            //// 启用深度测试
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LESS);
            gl.LoadIdentity();
            // 设置投影矩阵
            camera.LookAtMatrix(gl);
            // 应用视图变换并绘制所有元素
            Render(gl);
            // 禁用深度测试
            gl.Disable(OpenGL.GL_DEPTH_TEST);
        }
        protected override void DoGDIDraw(RenderEventArgs e)
        {
            base.DoGDIDraw(e);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            camera.LookAtMatrix(OpenGL);
        }
        public void Clear()
        {
            renderItem.Clear();
            Invalidate();
        }
        private void toolStripMenuItem_Click(object sender, EventArgs e)
        {
            camera?.FitView(surfaceItem?.BoundingBox);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menu_right = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.autoFitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewModeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pointCloudToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.meshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.heightMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.intensityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lineWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menu_right.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            this.SuspendLayout();
            // 
            // menu_right
            // 
            this.menu_right.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.autoFitToolStripMenuItem,
            this.viewModeToolStripMenuItem,
            this.lineWidthToolStripMenuItem});
            this.menu_right.Name = "menu_right";
            this.menu_right.Size = new System.Drawing.Size(181, 92);
            // 
            // autoFitToolStripMenuItem
            // 
            this.autoFitToolStripMenuItem.Name = "autoFitToolStripMenuItem";
            this.autoFitToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.autoFitToolStripMenuItem.Text = "Auto Fit";
            this.autoFitToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_Click);
            // 
            // viewModeToolStripMenuItem
            // 
            this.viewModeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pointCloudToolStripMenuItem,
            this.meshToolStripMenuItem,
            this.heightMapToolStripMenuItem,
            this.intensityToolStripMenuItem});
            this.viewModeToolStripMenuItem.Name = "viewModeToolStripMenuItem";
            this.viewModeToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.viewModeToolStripMenuItem.Text = "ViewMode";
            // 
            // pointCloudToolStripMenuItem
            // 
            this.pointCloudToolStripMenuItem.Name = "pointCloudToolStripMenuItem";
            this.pointCloudToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.pointCloudToolStripMenuItem.Text = "Point Cloud";
            // 
            // meshToolStripMenuItem
            // 
            this.meshToolStripMenuItem.Name = "meshToolStripMenuItem";
            this.meshToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.meshToolStripMenuItem.Text = "Mesh";
            // 
            // heightMapToolStripMenuItem
            // 
            this.heightMapToolStripMenuItem.Name = "heightMapToolStripMenuItem";
            this.heightMapToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.heightMapToolStripMenuItem.Text = "HeightMap";
            // 
            // intensityToolStripMenuItem
            // 
            this.intensityToolStripMenuItem.Name = "intensityToolStripMenuItem";
            this.intensityToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.intensityToolStripMenuItem.Text = "Intensity";
            // 
            // lineWidthToolStripMenuItem
            // 
            this.lineWidthToolStripMenuItem.Name = "lineWidthToolStripMenuItem";
            this.lineWidthToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.lineWidthToolStripMenuItem.Text = "LineWidth";
            // 
            // CxDisplay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.ContextMenuStrip = this.menu_right;
            this.Name = "CxDisplay";
            this.RenderContextType = SharpGL.RenderContextType.FBO;
            this.menu_right.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();
            this.ResumeLayout(false);

        }
    }
}


