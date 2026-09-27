// =====================================================================
//  TwinPix - one file thumbnail card
//
//  The card shown on the right-hand side for every copy of a group:
//  preview, name, folder, size and the "Keep this file" button. Its
//  layout lives in FileCard.Designer.cs and can be edited on Visual
//  Studio's design surface; Bind() is what fills it at run time.
// =====================================================================

using System;
using System.Drawing;
using System.Windows.Forms;

namespace TwinPix
{
    public partial class FileCard : UserControl
    {
        static readonly Color KeepBack = Color.FromArgb(226, 245, 228);
        static readonly Color KeepBorder = Color.FromArgb(46, 139, 87);
        static readonly Color NormalBack = Color.White;

        bool _suspend;

        /// <summary>The file this card stands for, or null before Bind().</summary>
        public FileEntry Entry { get; private set; }

        /// <summary>Raised when this card becomes the one to keep.</summary>
        public event EventHandler KeepChanged;

        public FileCard()
        {
            InitializeComponent();
        }

        /// <summary>Whether this copy is the one marked to keep.</summary>
        public bool KeepChecked
        {
            get { return _rb.Checked; }
            set
            {
                if (_rb.Checked == value) return;
                _suspend = true;                 // a caller setting it is not a click
                try { _rb.Checked = value; }
                finally { _suspend = false; }
            }
        }

        /// <summary>
        /// Fills the card from one file. <paramref name="visual"/> adds the line
        /// saying how far this copy sits from the reference copy of its group;
        /// <paramref name="tip"/> is the window's shared tool tip.
        /// </summary>
        public void Bind(FileEntry e, bool visual, ToolTip tip)
        {
            Entry = e;
            if (e == null) return;

            _lblName.Text = e.FileName;
            _lblDir.Text = e.DirectoryPath;
            _lblInfo.Text = Util.FormatSize(e.Size)
                          + (e.Dimensions.Length > 0 ? "  -  " + e.Dimensions : "")
                          + "  -  " + e.Modified.ToString("yyyy-MM-dd");

            // One line for what marks this copy out: the preferred folder it
            // sits in, and - when the group was matched by appearance - how far
            // it is from the reference copy.
            string note = e.InPreferred ? "* preferred folder" : "";
            if (visual)
            {
                string match = e.Distance == 0 ? "same picture"
                                               : "differs by " + e.Distance + "/64";
                note = note.Length > 0 ? note + "  -  " + match : match;
            }
            _lblNote.Text = note;
            _lblNote.ForeColor = e.InPreferred ? KeepBorder : Color.DimGray;
            _lblNote.Visible = note.Length > 0;

            KeepChecked = e.Keep;

            if (tip != null)
            {
                string tipText = e.FullPath + "\r\n" + Util.FormatSize(e.Size)
                                 + (e.Dimensions.Length > 0 ? "\r\n" + e.Dimensions + " pixels" : "")
                                 + "\r\nModified " + e.Modified.ToString("yyyy-MM-dd HH:mm:ss")
                                 + (string.IsNullOrEmpty(e.Hash) ? "" : "\r\nMD5 " + e.Hash)
                                 + "\r\n\r\nDouble-click: open the image";
                tip.SetToolTip(_pic, tipText);
                tip.SetToolTip(_lblName, tipText);
                tip.SetToolTip(_lblDir, tipText);
                tip.SetToolTip(_lblInfo, tipText);
            }

            LoadThumbnail();
            UpdateStyle();
        }

        // The preview is decoded once at this size, then shrunk to fit the card:
        // big enough for a card on a wide panel, four to a row.
        const int ThumbMaxWidth = 480;
        const int ThumbMaxHeight = 326;

        void LoadThumbnail()
        {
            ReleaseThumbnail();
            try
            {
                _pic.Image = Util.LoadThumb(Entry.FullPath, ThumbMaxWidth, ThumbMaxHeight);
                UpdatePictureMode();
            }
            catch
            {
                _pic.Image = null;
                var l = new Label();
                l.Dock = DockStyle.Fill;
                l.TextAlign = ContentAlignment.MiddleCenter;
                l.ForeColor = Color.Gray;
                l.Text = "No preview";
                _pic.Controls.Add(l);
            }
        }

        /// <summary>
        /// Shrinks a preview larger than the picture box to fit it; a smaller one
        /// stays at its own size, centred, rather than being blown up blurry.
        /// </summary>
        void UpdatePictureMode()
        {
            Image img = _pic.Image;
            bool tooBig = img != null && (img.Width > _pic.Width || img.Height > _pic.Height);
            _pic.SizeMode = tooBig ? PictureBoxSizeMode.Zoom : PictureBoxSizeMode.CenterImage;
        }

        /// <summary>
        /// Resizes the card to <paramref name="width"/> pixels. The preview keeps
        /// the designer's 206 x 140 proportions and the lines below it move down
        /// with it; their spacing is the designer's.
        /// </summary>
        public void SetCardWidth(int width)
        {
            width = Math.Max(width, 80);
            if (width == Width) return;

            int inner = width - 18;                        // 6 px left, 12 px right, as designed
            int picHeight = Math.Max(40, inner * 140 / 206);

            SuspendLayout();
            _pic.SetBounds(6, 6, inner, picHeight);
            int y = 6 + picHeight + 6;
            _lblName.SetBounds(6, y, inner, 20); y += 22;
            _lblDir.SetBounds(6, y, inner, 36);  y += 38;
            _lblInfo.SetBounds(6, y, inner, 20); y += 22;
            _lblNote.SetBounds(6, y, inner, 20); y += 24;
            _rb.SetBounds(6, y, inner, 26);      y += 32;
            Size = new Size(width, y);
            ResumeLayout();

            UpdatePictureMode();
        }

        /// <summary>Green while this copy is the one kept.</summary>
        public void UpdateStyle()
        {
            bool keep = _rb.Checked;
            BackColor = keep ? KeepBack : NormalBack;
            _lblName.ForeColor = keep ? KeepBorder : SystemColors.ControlText;
            _rb.ForeColor = keep ? KeepBorder : SystemColors.ControlText;
            _rb.Font = keep ? Util.UiFontBold : Util.UiFont;
        }

        /// <summary>
        /// Frees the preview bitmap. The thumbnail is decoded into memory and the
        /// file closed at once, so nothing must keep the bitmap alive once the
        /// card is gone.
        /// </summary>
        void ReleaseThumbnail()
        {
            if (_pic == null) return;
            Image img = _pic.Image;
            if (img == null) return;
            _pic.Image = null;
            try { img.Dispose(); }
            catch { }
        }

        // ---------------------- Designer event handlers -----------------

        void Rb_CheckedChanged(object sender, EventArgs e)
        {
            UpdateStyle();
            if (_suspend || !_rb.Checked) return;
            if (Entry != null) Entry.Keep = true;
            if (KeepChanged != null) KeepChanged(this, EventArgs.Empty);
        }

        void Card_Click(object sender, EventArgs e)
        {
            _rb.Checked = true;
        }

        void Card_DoubleClick(object sender, EventArgs e)
        {
            if (Entry != null) Util.OpenFile(Entry.FullPath);
        }

        void MiOpen_Click(object sender, EventArgs e)
        {
            if (Entry != null) Util.OpenFile(Entry.FullPath);
        }

        void MiFolder_Click(object sender, EventArgs e)
        {
            if (Entry != null) Util.ShowInExplorer(Entry.FullPath);
        }

        void MiCopyPath_Click(object sender, EventArgs e)
        {
            if (Entry == null) return;
            try { Clipboard.SetText(Entry.FullPath); }
            catch { }
        }
    }
}
