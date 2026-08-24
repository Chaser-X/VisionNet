namespace DemoFrom
{
    partial class DemoFrom
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage3D = new System.Windows.Forms.TabPage();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.cxDisplay1 = new VisionNet.Controls.CxDisplay();
            this.cxDisplay2 = new VisionNet.Controls.CxDisplay();
            this.btn_meshToSurface = new System.Windows.Forms.Button();
            this.btn_surfaceToMesh = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.btn_addSeg3D = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.btn_test = new System.Windows.Forms.Button();
            this.btn_testPose = new System.Windows.Forms.Button();
            this.btn_ioSave = new System.Windows.Forms.Button();
            this.btn_ioLoadMesh = new System.Windows.Forms.Button();
            this.btn_ioLoadSurface = new System.Windows.Forms.Button();
            this.btn_dragMark = new System.Windows.Forms.Button();
            this.btn_boxDemo = new System.Windows.Forms.Button();
            this.btn_segDemo = new System.Windows.Forms.Button();
            this.btn_polyDemo = new System.Windows.Forms.Button();
            this.btn_diffDemo = new System.Windows.Forms.Button();
            this.btn_diffSurfaceDemo = new System.Windows.Forms.Button();
            this.btn_diffPointDemo = new System.Windows.Forms.Button();
            this.btn_colorRangeDemo = new System.Windows.Forms.Button();
            this.btn_diffRangeDemo = new System.Windows.Forms.Button();
            this.btn_mixedViewDemo = new System.Windows.Forms.Button();
            this.lbl_markPos = new System.Windows.Forms.Label();
            this.tabPage2D = new System.Windows.Forms.TabPage();
            this.tabControl1.SuspendLayout();
            this.tabPage3D.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.cxDisplay1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cxDisplay2)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage3D);
            this.tabControl1.Controls.Add(this.tabPage2D);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1457, 897);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage3D
            // 
            this.tabPage3D.Controls.Add(this.splitContainer1);
            this.tabPage3D.Location = new System.Drawing.Point(4, 25);
            this.tabPage3D.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage3D.Name = "tabPage3D";
            this.tabPage3D.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage3D.Size = new System.Drawing.Size(1449, 868);
            this.tabPage3D.TabIndex = 0;
            this.tabPage3D.Text = "3D 显示";
            this.tabPage3D.UseVisualStyleBackColor = true;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Location = new System.Drawing.Point(4, 4);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.btn_meshToSurface);
            this.splitContainer1.Panel2.Controls.Add(this.btn_surfaceToMesh);
            this.splitContainer1.Panel2.Controls.Add(this.button1);
            this.splitContainer1.Panel2.Controls.Add(this.btn_addSeg3D);
            this.splitContainer1.Panel2.Controls.Add(this.button2);
            this.splitContainer1.Panel2.Controls.Add(this.btn_test);
            this.splitContainer1.Panel2.Controls.Add(this.btn_testPose);
            this.splitContainer1.Panel2.Controls.Add(this.btn_ioSave);
            this.splitContainer1.Panel2.Controls.Add(this.btn_ioLoadMesh);
            this.splitContainer1.Panel2.Controls.Add(this.btn_ioLoadSurface);
            this.splitContainer1.Panel2.Controls.Add(this.btn_dragMark);
            this.splitContainer1.Panel2.Controls.Add(this.btn_boxDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_segDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_polyDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_diffDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_diffSurfaceDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_diffPointDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_colorRangeDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_diffRangeDemo);
            this.splitContainer1.Panel2.Controls.Add(this.btn_mixedViewDemo);
            this.splitContainer1.Panel2.Controls.Add(this.lbl_markPos);
            this.splitContainer1.Size = new System.Drawing.Size(1441, 860);
            this.splitContainer1.SplitterDistance = 1277;
            this.splitContainer1.SplitterWidth = 5;
            this.splitContainer1.TabIndex = 1;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.cxDisplay1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.cxDisplay2);
            this.splitContainer2.Size = new System.Drawing.Size(1277, 860);
            this.splitContainer2.SplitterDistance = 628;
            this.splitContainer2.SplitterWidth = 5;
            this.splitContainer2.TabIndex = 0;
            // 
            // cxDisplay1
            // 
            this.cxDisplay1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cxDisplay1.DrawFPS = false;
            this.cxDisplay1.FrameRate = 10;
            this.cxDisplay1.IsLeftHanded = false;
            this.cxDisplay1.Location = new System.Drawing.Point(0, 0);
            this.cxDisplay1.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.cxDisplay1.Name = "cxDisplay1";
            this.cxDisplay1.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL2_1;
            this.cxDisplay1.RenderContextType = SharpGL.RenderContextType.FBO;
            this.cxDisplay1.RenderTrigger = SharpGL.RenderTrigger.Manual;
            this.cxDisplay1.ShowCoordinateSystem = false;
            this.cxDisplay1.Size = new System.Drawing.Size(628, 860);
            this.cxDisplay1.SurfaceColorMode = VisionNet.Controls.SurfaceColorMode.ColorWithIntensity;
            this.cxDisplay1.SurfaceMode = VisionNet.Controls.SurfaceMode.PointCloud;
            this.cxDisplay1.SurfaceViewMode = VisionNet.Controls.ViewMode.None;
            this.cxDisplay1.TabIndex = 1;
            // 
            // cxDisplay2
            // 
            this.cxDisplay2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cxDisplay2.DrawFPS = false;
            this.cxDisplay2.FrameRate = 10;
            this.cxDisplay2.IsLeftHanded = false;
            this.cxDisplay2.Location = new System.Drawing.Point(0, 0);
            this.cxDisplay2.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.cxDisplay2.Name = "cxDisplay2";
            this.cxDisplay2.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL2_1;
            this.cxDisplay2.RenderContextType = SharpGL.RenderContextType.FBO;
            this.cxDisplay2.RenderTrigger = SharpGL.RenderTrigger.Manual;
            this.cxDisplay2.ShowCoordinateSystem = false;
            this.cxDisplay2.Size = new System.Drawing.Size(644, 860);
            this.cxDisplay2.SurfaceColorMode = VisionNet.Controls.SurfaceColorMode.ColorWithIntensity;
            this.cxDisplay2.SurfaceMode = VisionNet.Controls.SurfaceMode.PointCloud;
            this.cxDisplay2.SurfaceViewMode = VisionNet.Controls.ViewMode.Top;
            this.cxDisplay2.TabIndex = 2;
            // 
            // btn_meshToSurface
            // 
            this.btn_meshToSurface.Location = new System.Drawing.Point(29, 345);
            this.btn_meshToSurface.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_meshToSurface.Name = "btn_meshToSurface";
            this.btn_meshToSurface.Size = new System.Drawing.Size(123, 28);
            this.btn_meshToSurface.TabIndex = 0;
            this.btn_meshToSurface.Text = "Mesh→Surface";
            this.btn_meshToSurface.UseVisualStyleBackColor = true;
            this.btn_meshToSurface.Click += new System.EventHandler(this.btn_meshToSurface_Click);
            // 
            // btn_surfaceToMesh
            // 
            this.btn_surfaceToMesh.Location = new System.Drawing.Point(29, 287);
            this.btn_surfaceToMesh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_surfaceToMesh.Name = "btn_surfaceToMesh";
            this.btn_surfaceToMesh.Size = new System.Drawing.Size(123, 28);
            this.btn_surfaceToMesh.TabIndex = 0;
            this.btn_surfaceToMesh.Text = "Surface→Mesh";
            this.btn_surfaceToMesh.UseVisualStyleBackColor = true;
            this.btn_surfaceToMesh.Click += new System.EventHandler(this.btn_surfaceToMesh_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(29, 174);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(123, 28);
            this.button1.TabIndex = 0;
            this.button1.Text = "Reset";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // btn_addSeg3D
            // 
            this.btn_addSeg3D.Location = new System.Drawing.Point(29, 103);
            this.btn_addSeg3D.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_addSeg3D.Name = "btn_addSeg3D";
            this.btn_addSeg3D.Size = new System.Drawing.Size(123, 28);
            this.btn_addSeg3D.TabIndex = 0;
            this.btn_addSeg3D.Text = "AddSegment3D";
            this.btn_addSeg3D.UseVisualStyleBackColor = true;
            this.btn_addSeg3D.Click += new System.EventHandler(this.btn_addSeg3D_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(29, 230);
            this.button2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(123, 28);
            this.button2.TabIndex = 0;
            this.button2.Text = "Tranform";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // btn_test
            // 
            this.btn_test.Location = new System.Drawing.Point(29, 46);
            this.btn_test.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_test.Name = "btn_test";
            this.btn_test.Size = new System.Drawing.Size(123, 28);
            this.btn_test.TabIndex = 0;
            this.btn_test.Text = "Test";
            this.btn_test.UseVisualStyleBackColor = true;
            this.btn_test.Click += new System.EventHandler(this.btn_test_Click);
            // 
            // btn_testPose
            // 
            this.btn_testPose.Location = new System.Drawing.Point(29, 401);
            this.btn_testPose.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_testPose.Name = "btn_testPose";
            this.btn_testPose.Size = new System.Drawing.Size(123, 28);
            this.btn_testPose.TabIndex = 0;
            this.btn_testPose.Text = "Test Pose";
            this.btn_testPose.UseVisualStyleBackColor = true;
            this.btn_testPose.Click += new System.EventHandler(this.btn_testPose_Click);
            // 
            // btn_ioSave
            // 
            this.btn_ioSave.Location = new System.Drawing.Point(29, 458);
            this.btn_ioSave.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_ioSave.Name = "btn_ioSave";
            this.btn_ioSave.Size = new System.Drawing.Size(123, 28);
            this.btn_ioSave.TabIndex = 0;
            this.btn_ioSave.Text = "Save R/L";
            this.btn_ioSave.UseVisualStyleBackColor = true;
            this.btn_ioSave.Click += new System.EventHandler(this.btn_ioSave_Click);
            // 
            // btn_ioLoadMesh
            // 
            this.btn_ioLoadMesh.Location = new System.Drawing.Point(29, 508);
            this.btn_ioLoadMesh.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_ioLoadMesh.Name = "btn_ioLoadMesh";
            this.btn_ioLoadMesh.Size = new System.Drawing.Size(123, 28);
            this.btn_ioLoadMesh.TabIndex = 0;
            this.btn_ioLoadMesh.Text = "Load Mesh";
            this.btn_ioLoadMesh.UseVisualStyleBackColor = true;
            this.btn_ioLoadMesh.Click += new System.EventHandler(this.btn_ioLoadMesh_Click);
            // 
            // btn_ioLoadSurface
            // 
            this.btn_ioLoadSurface.Location = new System.Drawing.Point(29, 558);
            this.btn_ioLoadSurface.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_ioLoadSurface.Name = "btn_ioLoadSurface";
            this.btn_ioLoadSurface.Size = new System.Drawing.Size(123, 28);
            this.btn_ioLoadSurface.TabIndex = 0;
            this.btn_ioLoadSurface.Text = "Load Surface";
            this.btn_ioLoadSurface.UseVisualStyleBackColor = true;
            this.btn_ioLoadSurface.Click += new System.EventHandler(this.btn_ioLoadSurface_Click);
            // 
            // btn_dragMark
            // 
            this.btn_dragMark.Location = new System.Drawing.Point(29, 598);
            this.btn_dragMark.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_dragMark.Name = "btn_dragMark";
            this.btn_dragMark.Size = new System.Drawing.Size(123, 28);
            this.btn_dragMark.TabIndex = 0;
            this.btn_dragMark.Text = "拖动MARK";
            this.btn_dragMark.UseVisualStyleBackColor = true;
            this.btn_dragMark.Click += new System.EventHandler(this.btn_dragMark_Click);
            // 
            // btn_boxDemo
            // 
            this.btn_boxDemo.Location = new System.Drawing.Point(29, 684);
            this.btn_boxDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_boxDemo.Name = "btn_boxDemo";
            this.btn_boxDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_boxDemo.TabIndex = 0;
            this.btn_boxDemo.Text = "Box3D 拖拽";
            this.btn_boxDemo.UseVisualStyleBackColor = true;
            this.btn_boxDemo.Click += new System.EventHandler(this.btn_boxDemo_Click);
            // 
            // btn_segDemo
            // 
            this.btn_segDemo.Location = new System.Drawing.Point(29, 733);
            this.btn_segDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_segDemo.Name = "btn_segDemo";
            this.btn_segDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_segDemo.TabIndex = 0;
            this.btn_segDemo.Text = "Segment 拖拽";
            this.btn_segDemo.UseVisualStyleBackColor = true;
            this.btn_segDemo.Click += new System.EventHandler(this.btn_segDemo_Click);
            // 
            // btn_polyDemo
            // 
            this.btn_polyDemo.Location = new System.Drawing.Point(29, 782);
            this.btn_polyDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_polyDemo.Name = "btn_polyDemo";
            this.btn_polyDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_polyDemo.TabIndex = 0;
            this.btn_polyDemo.Text = "Polygon 拖拽";
            this.btn_polyDemo.UseVisualStyleBackColor = true;
            this.btn_polyDemo.Click += new System.EventHandler(this.btn_polyDemo_Click);
            // 
            // btn_diffDemo
            // 
            this.btn_diffDemo.Location = new System.Drawing.Point(29, 832);
            this.btn_diffDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_diffDemo.Name = "btn_diffDemo";
            this.btn_diffDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_diffDemo.TabIndex = 0;
            this.btn_diffDemo.Text = "Diff Demo";
            this.btn_diffDemo.UseVisualStyleBackColor = true;
            this.btn_diffDemo.Click += new System.EventHandler(this.btn_diffDemo_Click);
            //
            // btn_diffSurfaceDemo
            //
            this.btn_diffSurfaceDemo.Location = new System.Drawing.Point(29, 864);
            this.btn_diffSurfaceDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_diffSurfaceDemo.Name = "btn_diffSurfaceDemo";
            this.btn_diffSurfaceDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_diffSurfaceDemo.TabIndex = 1;
            this.btn_diffSurfaceDemo.Text = "Diff Surface";
            this.btn_diffSurfaceDemo.UseVisualStyleBackColor = true;
            this.btn_diffSurfaceDemo.Click += new System.EventHandler(this.btn_diffSurfaceDemo_Click);
            //
            // btn_diffPointDemo
            //
            this.btn_diffPointDemo.Location = new System.Drawing.Point(29, 896);
            this.btn_diffPointDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_diffPointDemo.Name = "btn_diffPointDemo";
            this.btn_diffPointDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_diffPointDemo.TabIndex = 2;
            this.btn_diffPointDemo.Text = "Diff PointCloud";
            this.btn_diffPointDemo.UseVisualStyleBackColor = true;
            this.btn_diffPointDemo.Click += new System.EventHandler(this.btn_diffPointDemo_Click);
            //
            // btn_colorRangeDemo
            //
            this.btn_colorRangeDemo.Location = new System.Drawing.Point(29, 928);
            this.btn_colorRangeDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_colorRangeDemo.Name = "btn_colorRangeDemo";
            this.btn_colorRangeDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_colorRangeDemo.TabIndex = 3;
            this.btn_colorRangeDemo.Text = "Color Range";
            this.btn_colorRangeDemo.UseVisualStyleBackColor = true;
            this.btn_colorRangeDemo.Click += new System.EventHandler(this.btn_colorRangeDemo_Click);
            //
            // btn_diffRangeDemo
            //
            this.btn_diffRangeDemo.Location = new System.Drawing.Point(29, 960);
            this.btn_diffRangeDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_diffRangeDemo.Name = "btn_diffRangeDemo";
            this.btn_diffRangeDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_diffRangeDemo.TabIndex = 4;
            this.btn_diffRangeDemo.Text = "Diff Range";
            this.btn_diffRangeDemo.UseVisualStyleBackColor = true;
            this.btn_diffRangeDemo.Click += new System.EventHandler(this.btn_diffRangeDemo_Click);
            //
            // btn_mixedViewDemo
            //
            this.btn_mixedViewDemo.Location = new System.Drawing.Point(29, 992);
            this.btn_mixedViewDemo.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_mixedViewDemo.Name = "btn_mixedViewDemo";
            this.btn_mixedViewDemo.Size = new System.Drawing.Size(123, 28);
            this.btn_mixedViewDemo.TabIndex = 5;
            this.btn_mixedViewDemo.Text = "Mesh+PC View";
            this.btn_mixedViewDemo.UseVisualStyleBackColor = true;
            this.btn_mixedViewDemo.Click += new System.EventHandler(this.btn_mixedViewDemo_Click);
            // 
            // lbl_markPos
            // 
            this.lbl_markPos.Font = new System.Drawing.Font("Consolas", 8F);
            this.lbl_markPos.Location = new System.Drawing.Point(7, 635);
            this.lbl_markPos.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lbl_markPos.Name = "lbl_markPos";
            this.lbl_markPos.Size = new System.Drawing.Size(207, 44);
            this.lbl_markPos.TabIndex = 1;
            this.lbl_markPos.Text = "X:--  Y:--  Z:--";
            // 
            // tabPage2D
            // 
            this.tabPage2D.Location = new System.Drawing.Point(4, 25);
            this.tabPage2D.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage2D.Name = "tabPage2D";
            this.tabPage2D.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPage2D.Size = new System.Drawing.Size(1449, 868);
            this.tabPage2D.TabIndex = 1;
            this.tabPage2D.Text = "2D 显示";
            this.tabPage2D.UseVisualStyleBackColor = true;
            // 
            // DemoFrom
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1457, 897);
            this.Controls.Add(this.tabControl1);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "DemoFrom";
            this.Text = "VisionNet Demo";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DemoFrom_FormClosing);
            this.Load += new System.EventHandler(this.DemoFrom_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage3D.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.cxDisplay1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cxDisplay2)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage3D;
        private System.Windows.Forms.TabPage tabPage2D;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btn_test;
        private System.Windows.Forms.Button btn_addSeg3D;
        private VisionNet.Controls.CxDisplay cxDisplay2;
        private VisionNet.Controls.CxDisplay cxDisplay1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btn_surfaceToMesh;
        private System.Windows.Forms.Button btn_meshToSurface;
        private System.Windows.Forms.Button btn_testPose;
        private System.Windows.Forms.Button btn_ioSave;
        private System.Windows.Forms.Button btn_ioLoadMesh;
        private System.Windows.Forms.Button btn_ioLoadSurface;
        private System.Windows.Forms.Button btn_dragMark;
        private System.Windows.Forms.Button btn_boxDemo;
        private System.Windows.Forms.Button btn_segDemo;
        private System.Windows.Forms.Button btn_polyDemo;
        private System.Windows.Forms.Button btn_diffDemo;
        private System.Windows.Forms.Button btn_diffSurfaceDemo;
        private System.Windows.Forms.Button btn_diffPointDemo;
        private System.Windows.Forms.Button btn_colorRangeDemo;
        private System.Windows.Forms.Button btn_diffRangeDemo;
        private System.Windows.Forms.Button btn_mixedViewDemo;
        private System.Windows.Forms.Label lbl_markPos;
    }
}
