namespace TwinPix
{
    partial class FileCard
    {
        /// <summary>Components owned by the card: its context menu.</summary>
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseThumbnail();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._menu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this._miOpen = new System.Windows.Forms.ToolStripMenuItem();
            this._miFolder = new System.Windows.Forms.ToolStripMenuItem();
            this._miCopyPath = new System.Windows.Forms.ToolStripMenuItem();
            this._pic = new System.Windows.Forms.PictureBox();
            this._lblName = new System.Windows.Forms.Label();
            this._lblDir = new System.Windows.Forms.Label();
            this._lblInfo = new System.Windows.Forms.Label();
            this._lblNote = new System.Windows.Forms.Label();
            this._rb = new System.Windows.Forms.RadioButton();
            this._menu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._pic)).BeginInit();
            this.SuspendLayout();
            //
            // _menu
            //
            this._menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._miOpen,
            this._miFolder,
            this._miCopyPath});
            this._menu.Name = "_menu";
            this._menu.Size = new System.Drawing.Size(211, 70);
            //
            // _miOpen
            //
            this._miOpen.Name = "_miOpen";
            this._miOpen.Size = new System.Drawing.Size(210, 22);
            this._miOpen.Text = "Open image";
            this._miOpen.Click += new System.EventHandler(this.MiOpen_Click);
            //
            // _miFolder
            //
            this._miFolder.Name = "_miFolder";
            this._miFolder.Size = new System.Drawing.Size(210, 22);
            this._miFolder.Text = "Open containing folder";
            this._miFolder.Click += new System.EventHandler(this.MiFolder_Click);
            //
            // _miCopyPath
            //
            this._miCopyPath.Name = "_miCopyPath";
            this._miCopyPath.Size = new System.Drawing.Size(210, 22);
            this._miCopyPath.Text = "Copy path";
            this._miCopyPath.Click += new System.EventHandler(this.MiCopyPath_Click);
            //
            // _pic
            //
            this._pic.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(244)))), ((int)(((byte)(244)))), ((int)(((byte)(244)))));
            this._pic.ContextMenuStrip = this._menu;
            this._pic.Cursor = System.Windows.Forms.Cursors.Hand;
            this._pic.Location = new System.Drawing.Point(6, 6);
            this._pic.Name = "_pic";
            this._pic.Size = new System.Drawing.Size(206, 140);
            this._pic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this._pic.TabIndex = 0;
            this._pic.TabStop = false;
            this._pic.Click += new System.EventHandler(this.Card_Click);
            this._pic.DoubleClick += new System.EventHandler(this.Card_DoubleClick);
            //
            // _lblName
            //
            this._lblName.AutoEllipsis = true;
            this._lblName.ContextMenuStrip = this._menu;
            this._lblName.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblName.Location = new System.Drawing.Point(6, 152);
            this._lblName.Name = "_lblName";
            this._lblName.Size = new System.Drawing.Size(206, 20);
            this._lblName.TabIndex = 1;
            this._lblName.Text = "file name";
            this._lblName.Click += new System.EventHandler(this.Card_Click);
            this._lblName.DoubleClick += new System.EventHandler(this.Card_DoubleClick);
            //
            // _lblDir
            //
            this._lblDir.AutoEllipsis = true;
            this._lblDir.ContextMenuStrip = this._menu;
            this._lblDir.ForeColor = System.Drawing.Color.DimGray;
            this._lblDir.Location = new System.Drawing.Point(6, 174);
            this._lblDir.Name = "_lblDir";
            this._lblDir.Size = new System.Drawing.Size(206, 36);
            this._lblDir.TabIndex = 2;
            this._lblDir.Text = "folder";
            this._lblDir.Click += new System.EventHandler(this.Card_Click);
            //
            // _lblInfo
            //
            this._lblInfo.AutoEllipsis = true;
            this._lblInfo.ContextMenuStrip = this._menu;
            this._lblInfo.ForeColor = System.Drawing.Color.DimGray;
            this._lblInfo.Location = new System.Drawing.Point(6, 212);
            this._lblInfo.Name = "_lblInfo";
            this._lblInfo.Size = new System.Drawing.Size(206, 20);
            this._lblInfo.TabIndex = 3;
            this._lblInfo.Text = "size - dimensions - date";
            this._lblInfo.Click += new System.EventHandler(this.Card_Click);
            //
            // _lblNote
            //
            this._lblNote.AutoEllipsis = true;
            this._lblNote.ContextMenuStrip = this._menu;
            this._lblNote.ForeColor = System.Drawing.Color.DimGray;
            this._lblNote.Location = new System.Drawing.Point(6, 234);
            this._lblNote.Name = "_lblNote";
            this._lblNote.Size = new System.Drawing.Size(206, 20);
            this._lblNote.TabIndex = 4;
            this._lblNote.Visible = false;
            this._lblNote.Click += new System.EventHandler(this.Card_Click);
            //
            // _rb
            //
            this._rb.AutoSize = false;
            this._rb.Location = new System.Drawing.Point(6, 258);
            this._rb.Name = "_rb";
            this._rb.Size = new System.Drawing.Size(206, 26);
            this._rb.TabIndex = 5;
            this._rb.Text = "Keep this file";
            this._rb.UseVisualStyleBackColor = true;
            this._rb.CheckedChanged += new System.EventHandler(this.Rb_CheckedChanged);
            //
            // FileCard
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ContextMenuStrip = this._menu;
            this.Controls.Add(this._pic);
            this.Controls.Add(this._lblName);
            this.Controls.Add(this._lblDir);
            this.Controls.Add(this._lblInfo);
            this.Controls.Add(this._lblNote);
            this.Controls.Add(this._rb);
            this.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(8);
            this.Name = "FileCard";
            this.Size = new System.Drawing.Size(224, 290);
            this.Click += new System.EventHandler(this.Card_Click);
            this.DoubleClick += new System.EventHandler(this.Card_DoubleClick);
            this._menu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._pic)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip _menu;
        private System.Windows.Forms.ToolStripMenuItem _miOpen;
        private System.Windows.Forms.ToolStripMenuItem _miFolder;
        private System.Windows.Forms.ToolStripMenuItem _miCopyPath;
        private System.Windows.Forms.PictureBox _pic;
        private System.Windows.Forms.Label _lblName;
        private System.Windows.Forms.Label _lblDir;
        private System.Windows.Forms.Label _lblInfo;
        private System.Windows.Forms.Label _lblNote;
        private System.Windows.Forms.RadioButton _rb;
    }
}
