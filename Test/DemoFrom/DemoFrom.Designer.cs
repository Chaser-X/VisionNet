namespace DemoFrom
{
    partial class DemoFrom
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.cxDisplay1 = new VisionNet.Controls.CxDisplay();
            this.cxDisplay2 = new VisionNet.Controls.CxDisplay();
            this.button1 = new System.Windows.Forms.Button();
            this.btn_addSeg3D = new System.Windows.Forms.Button();
            this.btn_test = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
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
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.button1);
            this.splitContainer1.Panel2.Controls.Add(this.btn_addSeg3D);
            this.splitContainer1.Panel2.Controls.Add(this.button2);
            this.splitContainer1.Panel2.Controls.Add(this.btn_test);
            this.splitContainer1.Size = new System.Drawing.Size(918, 443);
            this.splitContainer1.SplitterDistance = 749;
            this.splitContainer1.TabIndex = 1;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.cxDisplay1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.cxDisplay2);
            this.splitContainer2.Size = new System.Drawing.Size(749, 443);
            this.splitContainer2.SplitterDistance = 370;
            this.splitContainer2.TabIndex = 0;
            // 
            // cxDisplay1
            // 
            this.cxDisplay1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cxDisplay1.DrawFPS = false;
            this.cxDisplay1.FrameRate = 10;
            this.cxDisplay1.Location = new System.Drawing.Point(0, 0);
            this.cxDisplay1.Margin = new System.Windows.Forms.Padding(4);
            this.cxDisplay1.Name = "cxDisplay1";
            this.cxDisplay1.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL2_1;
            this.cxDisplay1.RenderContextType = SharpGL.RenderContextType.FBO;
            this.cxDisplay1.RenderTrigger = SharpGL.RenderTrigger.Manual;
            this.cxDisplay1.ShowCoordinateSystem = false;
            this.cxDisplay1.Size = new System.Drawing.Size(370, 443);
            this.cxDisplay1.SurfaceColorMode = VisionNet.Controls.SurfaceColorMode.ColorWithIntensity;
            this.cxDisplay1.SurfaceMode = VisionNet.Controls.SurfaceMode.PointCloud;
            this.cxDisplay1.SurfaceViewMode = VisionNet.Controls.ViewMode.Top;
            this.cxDisplay1.TabIndex = 1;
            // 
            // cxDisplay2
            // 
            this.cxDisplay2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cxDisplay2.DrawFPS = false;
            this.cxDisplay2.FrameRate = 10;
            this.cxDisplay2.Location = new System.Drawing.Point(0, 0);
            this.cxDisplay2.Margin = new System.Windows.Forms.Padding(4);
            this.cxDisplay2.Name = "cxDisplay2";
            this.cxDisplay2.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL2_1;
            this.cxDisplay2.RenderContextType = SharpGL.RenderContextType.FBO;
            this.cxDisplay2.RenderTrigger = SharpGL.RenderTrigger.Manual;
            this.cxDisplay2.ShowCoordinateSystem = false;
            this.cxDisplay2.Size = new System.Drawing.Size(375, 443);
            this.cxDisplay2.SurfaceColorMode = VisionNet.Controls.SurfaceColorMode.ColorWithIntensity;
            this.cxDisplay2.SurfaceMode = VisionNet.Controls.SurfaceMode.PointCloud;
            this.cxDisplay2.SurfaceViewMode = VisionNet.Controls.ViewMode.Top;
            this.cxDisplay2.TabIndex = 2;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(22, 141);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(92, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Reset";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // btn_addSeg3D
            // 
            this.btn_addSeg3D.Location = new System.Drawing.Point(22, 84);
            this.btn_addSeg3D.Name = "btn_addSeg3D";
            this.btn_addSeg3D.Size = new System.Drawing.Size(92, 23);
            this.btn_addSeg3D.TabIndex = 0;
            this.btn_addSeg3D.Text = "AddSegment3D";
            this.btn_addSeg3D.UseVisualStyleBackColor = true;
            this.btn_addSeg3D.Click += new System.EventHandler(this.btn_addSeg3D_Click);
            // 
            // btn_test
            // 
            this.btn_test.Location = new System.Drawing.Point(22, 37);
            this.btn_test.Name = "btn_test";
            this.btn_test.Size = new System.Drawing.Size(92, 23);
            this.btn_test.TabIndex = 0;
            this.btn_test.Text = "Test";
            this.btn_test.UseVisualStyleBackColor = true;
            this.btn_test.Click += new System.EventHandler(this.btn_test_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(22, 187);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(92, 23);
            this.button2.TabIndex = 0;
            this.button2.Text = "Tranform";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // DemoFrom
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(918, 443);
            this.Controls.Add(this.splitContainer1);
            this.Name = "DemoFrom";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DemoFrom_FormClosing);
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
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btn_test;
        private System.Windows.Forms.Button btn_addSeg3D;
        private VisionNet.Controls.CxDisplay cxDisplay2;
        private VisionNet.Controls.CxDisplay cxDisplay1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
    }
}

