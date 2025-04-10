using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
        private ContextMenuStrip menu_right;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem viewModeToolStripMenuItem;
        private ToolStripMenuItem surfaceModeToolStripMenuItem;
        private ToolStripMenuItem pointCloudToolStripMenuItem;
        private ToolStripMenuItem meshToolStripMenuItem;
        private ToolStripMenuItem lineWidthToolStripMenuItem;
        private ToolStripMenuItem orthographicToolStripMenuItem;
        private ToolStripMenuItem topToolStripMenuItem1;
        private ToolStripMenuItem frontToolStripMenuItem;
        private ToolStripMenuItem leftToolStripMenuItem;
        private ToolStripMenuItem rightToolStripMenuItem;

        #region 资源释放
        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    components?.Dispose();
                }
                // 释放非托管资源（如果有）

                disposed = true;
            }
            base.Dispose(disposing);
        }
        #endregion
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menu_right = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.viewModeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.orthographicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.topToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.frontToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.leftToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rightToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.surfaceModeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pointCloudToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.meshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.surfaceColorModeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.colorMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.itensityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.colorWithIntensityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lineWidthToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lineWidthToolStripTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.d2DToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menu_right.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            this.SuspendLayout();
            // 
            // menu_right
            // 
            this.menu_right.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.d2DToolStripMenuItem,
            this.viewModeToolStripMenuItem,
            this.surfaceModeToolStripMenuItem,
            this.surfaceColorModeToolStripMenuItem,
            this.lineWidthToolStripMenuItem});
            this.menu_right.Name = "menu_right";
            this.menu_right.Size = new System.Drawing.Size(187, 136);
            // 
            // viewModeToolStripMenuItem
            // 
            this.viewModeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.orthographicToolStripMenuItem,
            this.topToolStripMenuItem1,
            this.frontToolStripMenuItem,
            this.leftToolStripMenuItem,
            this.rightToolStripMenuItem});
            this.viewModeToolStripMenuItem.Name = "viewModeToolStripMenuItem";
            this.viewModeToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.viewModeToolStripMenuItem.Text = "ViewMode";
            // 
            // orthographicToolStripMenuItem
            // 
            this.orthographicToolStripMenuItem.Name = "orthographicToolStripMenuItem";
            this.orthographicToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.orthographicToolStripMenuItem.Text = "None";
            this.orthographicToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_ViewModeClick);
            // 
            // topToolStripMenuItem1
            // 
            this.topToolStripMenuItem1.Name = "topToolStripMenuItem1";
            this.topToolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
            this.topToolStripMenuItem1.Text = "Top";
            this.topToolStripMenuItem1.Click += new System.EventHandler(this.toolStripMenuItem_ViewModeClick);
            // 
            // frontToolStripMenuItem
            // 
            this.frontToolStripMenuItem.Checked = true;
            this.frontToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.frontToolStripMenuItem.Name = "frontToolStripMenuItem";
            this.frontToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.frontToolStripMenuItem.Text = "Front";
            this.frontToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_ViewModeClick);
            // 
            // leftToolStripMenuItem
            // 
            this.leftToolStripMenuItem.Name = "leftToolStripMenuItem";
            this.leftToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.leftToolStripMenuItem.Text = "Left";
            this.leftToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_ViewModeClick);
            // 
            // rightToolStripMenuItem
            // 
            this.rightToolStripMenuItem.Name = "rightToolStripMenuItem";
            this.rightToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.rightToolStripMenuItem.Text = "Right";
            this.rightToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_ViewModeClick);
            // 
            // surfaceModeToolStripMenuItem
            // 
            this.surfaceModeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pointCloudToolStripMenuItem,
            this.meshToolStripMenuItem});
            this.surfaceModeToolStripMenuItem.Name = "surfaceModeToolStripMenuItem";
            this.surfaceModeToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.surfaceModeToolStripMenuItem.Text = "SurfaceMode";
            // 
            // pointCloudToolStripMenuItem
            // 
            this.pointCloudToolStripMenuItem.Name = "pointCloudToolStripMenuItem";
            this.pointCloudToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.pointCloudToolStripMenuItem.Tag = "1";
            this.pointCloudToolStripMenuItem.Text = "PointCloud";
            this.pointCloudToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_SurfaceModeClick);
            // 
            // meshToolStripMenuItem
            // 
            this.meshToolStripMenuItem.Name = "meshToolStripMenuItem";
            this.meshToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.meshToolStripMenuItem.Tag = "2";
            this.meshToolStripMenuItem.Text = "Mesh";
            this.meshToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_SurfaceModeClick);
            // 
            // surfaceColorModeToolStripMenuItem
            // 
            this.surfaceColorModeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.colorMapToolStripMenuItem,
            this.itensityToolStripMenuItem,
            this.colorWithIntensityToolStripMenuItem});
            this.surfaceColorModeToolStripMenuItem.Name = "surfaceColorModeToolStripMenuItem";
            this.surfaceColorModeToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.surfaceColorModeToolStripMenuItem.Text = "SurfaceColorMode";
            // 
            // colorMapToolStripMenuItem
            // 
            this.colorMapToolStripMenuItem.Name = "colorMapToolStripMenuItem";
            this.colorMapToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.colorMapToolStripMenuItem.Text = "Color";
            this.colorMapToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_SurfaceColorModeClick);
            // 
            // itensityToolStripMenuItem
            // 
            this.itensityToolStripMenuItem.Name = "itensityToolStripMenuItem";
            this.itensityToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.itensityToolStripMenuItem.Text = "Intensity";
            this.itensityToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_SurfaceColorModeClick);
            // 
            // colorWithIntensityToolStripMenuItem
            // 
            this.colorWithIntensityToolStripMenuItem.Name = "colorWithIntensityToolStripMenuItem";
            this.colorWithIntensityToolStripMenuItem.Size = new System.Drawing.Size(182, 22);
            this.colorWithIntensityToolStripMenuItem.Text = "ColorWithIntensity";
            this.colorWithIntensityToolStripMenuItem.Click += new System.EventHandler(this.toolStripMenuItem_SurfaceColorModeClick);
            // 
            // lineWidthToolStripMenuItem
            // 
            this.lineWidthToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lineWidthToolStripTextBox});
            this.lineWidthToolStripMenuItem.Name = "lineWidthToolStripMenuItem";
            this.lineWidthToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.lineWidthToolStripMenuItem.Text = "LineWidth";
            this.lineWidthToolStripMenuItem.Click += new System.EventHandler(this.lineWidthToolStripMenuItem_Click);
            // 
            // lineWidthToolStripTextBox
            // 
            this.lineWidthToolStripTextBox.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lineWidthToolStripTextBox.Name = "lineWidthToolStripTextBox";
            this.lineWidthToolStripTextBox.Size = new System.Drawing.Size(180, 23);
            this.lineWidthToolStripTextBox.Text = "1";
            // 
            // d2DToolStripMenuItem
            // 
            this.d2DToolStripMenuItem.Name = "d2DToolStripMenuItem";
            this.d2DToolStripMenuItem.Size = new System.Drawing.Size(186, 22);
            this.d2DToolStripMenuItem.Text = "2D View";
            this.d2DToolStripMenuItem.Click += new System.EventHandler(this.d2DToolStripMenuItem_Click);
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

        private ToolStripTextBox lineWidthToolStripTextBox;
        private ToolStripMenuItem surfaceColorModeToolStripMenuItem;
        private ToolStripMenuItem colorMapToolStripMenuItem;
        private ToolStripMenuItem itensityToolStripMenuItem;
        private ToolStripMenuItem colorWithIntensityToolStripMenuItem;
        private ToolStripMenuItem d2DToolStripMenuItem;
    }
}
