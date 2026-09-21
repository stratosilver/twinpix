// =====================================================================
//  TwinPix - Find and manage duplicate images
//  Win32 (WinForms) application in C#, buildable with nothing but the
//  compiler shipped with Windows:
//    %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
//  See build.bat
//
//  Duplicate criteria: file name (copy suffixes ignored)
//                    + size in bytes + extension
//  Option: content check (MD5) for confirmation.
// =====================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TwinPix
{
    // -----------------------------------------------------------------
    //  Entry point
    // -----------------------------------------------------------------
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // -----------------------------------------------------------------
    //  Model
    // -----------------------------------------------------------------
    public class FileEntry
    {
        public string FullPath;
        public string FileName;
        public string Extension;
        public long Size;
        public DateTime Modified;
        public bool InPreferred;
        public bool Keep;
        public string Hash;

        public string DirectoryPath
        {
            get
            {
                try { return Path.GetDirectoryName(FullPath); }
                catch { return ""; }
            }
        }
    }

    public class DupGroup
    {
        public string BaseName = "";
        public string Extension = "";
        public long Size;
        public List<FileEntry> Files = new List<FileEntry>();

        public long Wasted
        {
            get { return Files.Count > 1 ? Size * (Files.Count - 1) : 0; }
        }

        public FileEntry Kept
        {
            get
            {
                for (int i = 0; i < Files.Count; i++)
                    if (Files[i].Keep) return Files[i];
                return null;
            }
        }
    }

    public class ScanOptions
    {
        public string Root = "";
        public string Preferred = "";
        public bool Recursive = true;
        public bool IgnoreCopySuffix = true;
        public bool IgnoreNumericSuffix = false;
        public bool CompareContent = false;
        public HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public class ScanResult
    {
        public List<DupGroup> Groups = new List<DupGroup>();
        public int FilesScanned;
        public int Errors;
    }

    // -----------------------------------------------------------------
    //  Scan engine
    // -----------------------------------------------------------------
    public static class Scanner
    {
        static readonly Regex RxParen =
            new Regex(@"[\s_\-]*\(\s*\d{1,4}\s*\)$", RegexOptions.Compiled);

        static readonly Regex RxCopyWord =
            new Regex(@"[\s_\-]*(copie|copy|copia|kopie|kopia|duplicata|duplicate|dup)([\s_\-]*\d{1,4})?$",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static readonly Regex RxNumeric =
            new Regex(@"[\s_\-]+\d{1,4}$", RegexOptions.Compiled);

        /// <summary>Normalized base name used as the grouping key.</summary>
        public static string NormalizeName(string fileNameWithoutExt, ScanOptions o)
        {
            string s = fileNameWithoutExt == null ? "" : fileNameWithoutExt.Trim();

            if (o.IgnoreCopySuffix)
            {
                bool changed = true;
                while (changed && s.Length > 0)
                {
                    changed = false;
                    string t = RxParen.Replace(s, "");
                    if (t != s) { s = t; changed = true; }
                    t = RxCopyWord.Replace(s, "");
                    if (t != s) { s = t; changed = true; }
                    s = s.TrimEnd(' ', '_', '-', '.');
                }
            }

            if (o.IgnoreNumericSuffix)
            {
                string t = RxNumeric.Replace(s, "");
                if (t.Length > 0) s = t;
                s = s.TrimEnd(' ', '_', '-', '.');
            }

            if (s.Length == 0) s = fileNameWithoutExt == null ? "" : fileNameWithoutExt.Trim();
            return s.ToLowerInvariant();
        }

        public static bool IsUnder(string path, string folder)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(path)) return false;
            try
            {
                string f = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
                string p = Path.GetFullPath(path);
                return p.StartsWith(f + Path.DirectorySeparatorChar,
                                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static string Md5(string path)
        {
            using (var md5 = MD5.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite, 65536))
            {
                byte[] h = md5.ComputeHash(fs);
                var sb = new StringBuilder(h.Length * 2);
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        static void Walk(string dir, ScanOptions o, List<string> acc,
                         ScanResult res, BackgroundWorker bw)
        {
            if (bw != null && bw.CancellationPending) return;

            string[] files = null;
            try { files = Directory.GetFiles(dir); }
            catch { res.Errors++; }

            if (files != null)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string ext = "";
                    try { ext = Path.GetExtension(files[i]); }
                    catch { continue; }
                    if (ext != null && o.Extensions.Contains(ext)) acc.Add(files[i]);
                }
                if (bw != null) bw.ReportProgress(0, "Scanning: " + acc.Count + " image(s) found...");
            }

            if (!o.Recursive) return;

            string[] subs = null;
            try { subs = Directory.GetDirectories(dir); }
            catch { res.Errors++; }
            if (subs == null) return;

            for (int i = 0; i < subs.Length; i++)
            {
                if (bw != null && bw.CancellationPending) return;
                Walk(subs[i], o, acc, res, bw);
            }
        }

        public static ScanResult Scan(ScanOptions o, BackgroundWorker bw)
        {
            var res = new ScanResult();
            var paths = new List<string>();
            Walk(o.Root, o, paths, res, bw);
            res.FilesScanned = paths.Count;

            // Grouping: normalized name | extension | size
            var map = new Dictionary<string, DupGroup>(StringComparer.Ordinal);

            for (int i = 0; i < paths.Count; i++)
            {
                if (bw != null && bw.CancellationPending) return res;
                string p = paths[i];
                FileInfo fi;
                try { fi = new FileInfo(p); if (!fi.Exists) continue; }
                catch { res.Errors++; continue; }

                string ext = (fi.Extension ?? "").ToLowerInvariant();
                string baseName = NormalizeName(Path.GetFileNameWithoutExtension(p), o);
                string key = baseName + "|" + ext + "|" + fi.Length.ToString(CultureInfo.InvariantCulture);

                DupGroup g;
                if (!map.TryGetValue(key, out g))
                {
                    g = new DupGroup();
                    g.BaseName = baseName;
                    g.Extension = ext;
                    g.Size = fi.Length;
                    map[key] = g;
                }

                var e = new FileEntry();
                e.FullPath = fi.FullName;
                e.FileName = fi.Name;
                e.Extension = ext;
                e.Size = fi.Length;
                try { e.Modified = fi.LastWriteTime; }
                catch { e.Modified = DateTime.MinValue; }
                e.InPreferred = IsUnder(fi.FullName, o.Preferred);
                g.Files.Add(e);

                if (bw != null && (i % 200) == 0)
                    bw.ReportProgress(0, "Grouping: " + (i + 1) + " / " + paths.Count);
            }

            var groups = new List<DupGroup>();
            foreach (var kv in map)
                if (kv.Value.Files.Count > 1) groups.Add(kv.Value);

            // Optional content check: split each group by MD5
            if (o.CompareContent)
            {
                var refined = new List<DupGroup>();
                int done = 0;
                foreach (var g in groups)
                {
                    if (bw != null && bw.CancellationPending) return res;
                    var byHash = new Dictionary<string, DupGroup>(StringComparer.Ordinal);
                    foreach (var f in g.Files)
                    {
                        string h;
                        try { h = Md5(f.FullPath); }
                        catch { h = "ERR:" + f.FullPath; res.Errors++; }
                        f.Hash = h;
                        DupGroup sub;
                        if (!byHash.TryGetValue(h, out sub))
                        {
                            sub = new DupGroup();
                            sub.BaseName = g.BaseName;
                            sub.Extension = g.Extension;
                            sub.Size = g.Size;
                            byHash[h] = sub;
                        }
                        sub.Files.Add(f);
                    }
                    foreach (var kv in byHash)
                        if (kv.Value.Files.Count > 1) refined.Add(kv.Value);

                    done++;
                    if (bw != null)
                        bw.ReportProgress(0, "Checking content: " + done + " / " + groups.Count);
                }
                groups = refined;
            }

            foreach (var g in groups)
            {
                g.Files.Sort(delegate(FileEntry a, FileEntry b)
                {
                    return string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase);
                });
                AutoSelect(g, KeepRule.Preferred);
            }

            groups.Sort(delegate(DupGroup a, DupGroup b)
            {
                int c = b.Wasted.CompareTo(a.Wasted);
                if (c != 0) return c;
                return string.Compare(a.BaseName, b.BaseName, StringComparison.OrdinalIgnoreCase);
            });

            res.Groups = groups;
            return res;
        }

        public enum KeepRule { Preferred, Oldest, Newest, ShortestPath }

        public static void AutoSelect(DupGroup g, KeepRule rule)
        {
            FileEntry best = null;
            foreach (var f in g.Files)
                if (best == null || IsBetter(f, best, rule)) best = f;
            foreach (var f in g.Files) f.Keep = ReferenceEquals(f, best);
        }

        static int Depth(string p)
        {
            int n = 0;
            for (int i = 0; i < p.Length; i++)
                if (p[i] == Path.DirectorySeparatorChar || p[i] == '/') n++;
            return n;
        }

        static bool IsBetter(FileEntry a, FileEntry b, KeepRule rule)
        {
            // The preferred folder always wins.
            if (a.InPreferred != b.InPreferred) return a.InPreferred;

            switch (rule)
            {
                case KeepRule.Oldest:
                    if (a.Modified != b.Modified) return a.Modified < b.Modified;
                    break;
                case KeepRule.Newest:
                    if (a.Modified != b.Modified) return a.Modified > b.Modified;
                    break;
                case KeepRule.ShortestPath:
                    if (a.FullPath.Length != b.FullPath.Length)
                        return a.FullPath.Length < b.FullPath.Length;
                    break;
                default: // Preferred
                    int da = Depth(a.FullPath), db = Depth(b.FullPath);
                    if (da != db) return da < db;
                    if (a.Modified != b.Modified) return a.Modified < b.Modified;
                    break;
            }
            return string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase) < 0;
        }
    }

    // -----------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------
    public static class Util
    {
        // Single application font: one point larger than the system default,
        // which naturally grows text boxes, check boxes and list rows.
        public static readonly Font UiFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        public static readonly Font UiFontBold = new Font(UiFont, FontStyle.Bold);

        // Shared control heights and the margin kept from the window edge.
        public const int RowHeight = 32;
        public const int ButtonHeight = 32;
        public const int SideMargin = 9;

        public static string FormatSize(long bytes)
        {
            double b = bytes;
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
            return (i == 0 ? b.ToString("0", CultureInfo.CurrentCulture)
                           : b.ToString("0.##", CultureInfo.CurrentCulture)) + " " + u[i];
        }

        public static Image LoadThumb(string path, int w, int h)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite, 65536))
            using (var img = Image.FromStream(fs, false, false))
            {
                double rw = (double)w / img.Width;
                double rh = (double)h / img.Height;
                double r = Math.Min(Math.Min(rw, rh), 1.0);
                int nw = Math.Max(1, (int)(img.Width * r));
                int nh = Math.Max(1, (int)(img.Height * r));
                var bmp = new Bitmap(nw, nh);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, 0, 0, nw, nh);
                }
                return bmp;
            }
        }

        public static void OpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Cannot open the file:\r\n" + ex.Message); }
        }

        public static void ShowInExplorer(string path)
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { MessageBox.Show("Cannot open Explorer:\r\n" + ex.Message); }
        }

        public static string UniqueDestination(string dir, string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            string candidate = Path.Combine(dir, fileName);
            int i = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(dir, name + " (" + i + ")" + ext);
                i++;
                if (i > 9999) break;
            }
            return candidate;
        }
    }

    // -----------------------------------------------------------------
    //  One file thumbnail card
    // -----------------------------------------------------------------
    public class FileCard : Panel
    {
        public FileEntry Entry;
        public RadioButton Rb;
        readonly PictureBox _pic;
        readonly Label _lblName, _lblDir, _lblInfo;

        static readonly Color KeepBack = Color.FromArgb(226, 245, 228);
        static readonly Color KeepBorder = Color.FromArgb(46, 139, 87);
        static readonly Color NormalBack = Color.White;

        public FileCard(FileEntry e, EventHandler onKeepChanged, ToolTip tip)
        {
            Entry = e;
            Font = Util.UiFont;
            Width = 224;
            Height = 290;
            Margin = new Padding(8);
            BackColor = NormalBack;
            BorderStyle = BorderStyle.FixedSingle;

            _pic = new PictureBox();
            _pic.Location = new Point(6, 6);
            _pic.Size = new Size(206, 140);
            _pic.SizeMode = PictureBoxSizeMode.CenterImage;
            _pic.BackColor = Color.FromArgb(244, 244, 244);
            _pic.Cursor = Cursors.Hand;
            Controls.Add(_pic);

            _lblName = new Label();
            _lblName.Location = new Point(6, 152);
            _lblName.Size = new Size(206, 20);
            _lblName.AutoEllipsis = true;
            _lblName.Font = Util.UiFontBold;
            _lblName.Text = e.FileName;
            Controls.Add(_lblName);

            _lblDir = new Label();
            _lblDir.Location = new Point(6, 174);
            _lblDir.Size = new Size(206, 36);
            _lblDir.AutoEllipsis = true;
            _lblDir.ForeColor = Color.DimGray;
            _lblDir.Text = e.DirectoryPath;
            Controls.Add(_lblDir);

            _lblInfo = new Label();
            _lblInfo.Location = new Point(6, 212);
            _lblInfo.Size = new Size(206, 20);
            _lblInfo.ForeColor = Color.DimGray;
            _lblInfo.Text = Util.FormatSize(e.Size) + "  -  " + e.Modified.ToString("yyyy-MM-dd HH:mm");
            Controls.Add(_lblInfo);

            if (e.InPreferred)
            {
                var star = new Label();
                star.Location = new Point(6, 234);
                star.Size = new Size(206, 20);
                star.ForeColor = KeepBorder;
                star.Text = "* preferred folder";
                Controls.Add(star);
            }

            Rb = new RadioButton();
            Rb.Location = new Point(6, 258);
            Rb.AutoSize = false;
            Rb.Size = new Size(206, 26);
            Rb.Text = "Keep this file";
            Rb.Checked = e.Keep;
            Rb.CheckedChanged += onKeepChanged;
            Controls.Add(Rb);

            string tipText = e.FullPath + "\r\n" + Util.FormatSize(e.Size)
                             + "\r\nModified " + e.Modified.ToString("yyyy-MM-dd HH:mm:ss")
                             + (string.IsNullOrEmpty(e.Hash) ? "" : "\r\nMD5 " + e.Hash)
                             + "\r\n\r\nDouble-click: open the image";
            tip.SetToolTip(_pic, tipText);
            tip.SetToolTip(_lblName, tipText);
            tip.SetToolTip(_lblDir, tipText);
            tip.SetToolTip(_lblInfo, tipText);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open image", null, delegate { Util.OpenFile(Entry.FullPath); });
            menu.Items.Add("Open containing folder", null, delegate { Util.ShowInExplorer(Entry.FullPath); });
            menu.Items.Add("Copy path", null, delegate
            {
                try { Clipboard.SetText(Entry.FullPath); }
                catch { }
            });
            ContextMenuStrip = menu;
            _pic.ContextMenuStrip = menu;
            _lblName.ContextMenuStrip = menu;
            _lblDir.ContextMenuStrip = menu;

            EventHandler dbl = delegate { Util.OpenFile(Entry.FullPath); };
            _pic.DoubleClick += dbl;
            _lblName.DoubleClick += dbl;
            DoubleClick += dbl;

            EventHandler pick = delegate { Rb.Checked = true; };
            _pic.Click += pick;
            _lblName.Click += pick;
            _lblDir.Click += pick;
            _lblInfo.Click += pick;
            Click += pick;

            LoadThumbnail();
            UpdateStyle();
        }

        void LoadThumbnail()
        {
            try
            {
                _pic.Image = Util.LoadThumb(Entry.FullPath, 202, 136);
                if (_pic.Image != null && (_pic.Image.Width > 206 || _pic.Image.Height > 140))
                    _pic.SizeMode = PictureBoxSizeMode.Zoom;
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

        public void UpdateStyle()
        {
            bool keep = Rb.Checked;
            BackColor = keep ? KeepBack : NormalBack;
            _lblName.ForeColor = keep ? KeepBorder : SystemColors.ControlText;
            Rb.ForeColor = keep ? KeepBorder : SystemColors.ControlText;
            Rb.Font = keep ? Util.UiFontBold : Util.UiFont;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _pic != null && _pic.Image != null)
            {
                var img = _pic.Image;
                _pic.Image = null;
                img.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // -----------------------------------------------------------------
    //  Main window
    // -----------------------------------------------------------------
    public class MainForm : Form
    {
        static readonly Color AccentScan = Color.FromArgb(0, 102, 184);   // Windows blue
        static readonly Color AccentStop = Color.FromArgb(196, 89, 17);   // cancel orange
        static readonly Color AccentMove = Color.FromArgb(46, 125, 70);   // action green

        const string DefaultExtensions =
            ".jpg;.jpeg;.jpe;.jfif;.png;.gif;.bmp;.tif;.tiff;.webp;.heic;.heif;.ico;.psd;.svg;.raw;.cr2;.nef;.arw;.dng;.orf;.rw2";

        TextBox _txtRoot, _txtPreferred, _txtQuarantine, _txtExt;
        Button _btnRoot, _btnPreferred, _btnQuarantine, _btnScan;
        CheckBox _chkRecursive, _chkCopySuffix, _chkNumSuffix, _chkContent, _chkPreserveTree, _chkApplyAll;
        ListView _lv;
        FlowLayoutPanel _cards;
        Label _lblGroupTitle, _lblSummary;
        Button _btnMoveGroup, _btnMoveAll, _btnExport;
        Button _btnKeepPreferred, _btnKeepOldest, _btnKeepNewest, _btnKeepShortest;
        StatusStrip _status;
        ToolStripStatusLabel _statusLabel;
        ToolStripProgressBar _progress;
        ToolTip _tip = new ToolTip();

        BackgroundWorker _worker;
        List<DupGroup> _groups = new List<DupGroup>();
        DupGroup _current;
        bool _suspend;

        public MainForm()
        {
            Text = "TwinPix - duplicate images";
            Font = Util.UiFont;
            Width = 1240;
            Height = 840;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(960, 640);
            BuildUi();
        }

        // ---------------------- User interface ------------------------
        void BuildUi()
        {
            // ----- centre area (added first: it gets the remaining space)
            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterWidth = 6;

            var leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Fill;
            _lv = new ListView();
            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.MultiSelect = false;
            _lv.HideSelection = false;
            _lv.GridLines = true;
            _lv.Columns.Add("Name", 200);
            _lv.Columns.Add("Ext", 60);
            _lv.Columns.Add("Size", 90, HorizontalAlignment.Right);
            _lv.Columns.Add("Copies", 70, HorizontalAlignment.Right);
            _lv.Columns.Add("Reclaimable", 110, HorizontalAlignment.Right);
            _lv.Columns.Add("Keeping in", 260);
            _lv.SelectedIndexChanged += LvSelectionChanged;
            leftPanel.Controls.Add(_lv);
            var lblLeft = new Label();
            lblLeft.Dock = DockStyle.Top;
            lblLeft.Height = 26;
            lblLeft.Padding = new Padding(0, 6, 0, 0);
            lblLeft.Text = "Duplicate groups";
            lblLeft.Font = Util.UiFontBold;
            leftPanel.Controls.Add(lblLeft);
            split.Panel1.Controls.Add(leftPanel);

            var rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;

            _cards = new FlowLayoutPanel();
            _cards.Dock = DockStyle.Fill;
            _cards.AutoScroll = true;
            _cards.BackColor = Color.FromArgb(250, 250, 250);
            _cards.Padding = new Padding(6);
            rightPanel.Controls.Add(_cards);

            var toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.AutoSize = true;
            toolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            toolbar.WrapContents = true;
            toolbar.Padding = new Padding(2);
            var lblKeep = new Label();
            lblKeep.Text = "Auto-select:";
            lblKeep.AutoSize = true;
            lblKeep.MinimumSize = new Size(0, Util.RowHeight);
            lblKeep.TextAlign = ContentAlignment.MiddleLeft;
            lblKeep.Margin = new Padding(2, 4, 2, 4);
            toolbar.Controls.Add(lblKeep);
            _btnKeepPreferred = MakeButton("Preferred folder", 145, delegate { ApplyRule(Scanner.KeepRule.Preferred); });
            _btnKeepOldest = MakeButton("Oldest", 118, delegate { ApplyRule(Scanner.KeepRule.Oldest); });
            _btnKeepNewest = MakeButton("Newest", 118, delegate { ApplyRule(Scanner.KeepRule.Newest); });
            _btnKeepShortest = MakeButton("Shortest path", 135, delegate { ApplyRule(Scanner.KeepRule.ShortestPath); });
            toolbar.Controls.Add(_btnKeepPreferred);
            toolbar.Controls.Add(_btnKeepOldest);
            toolbar.Controls.Add(_btnKeepNewest);
            toolbar.Controls.Add(_btnKeepShortest);
            _chkApplyAll = MakeCheck("all groups", true);
            toolbar.Controls.Add(_chkApplyAll);
            rightPanel.Controls.Add(toolbar);

            _lblGroupTitle = new Label();
            _lblGroupTitle.Dock = DockStyle.Top;
            _lblGroupTitle.Height = 26;
            _lblGroupTitle.Padding = new Padding(0, 6, 0, 0);
            _lblGroupTitle.Font = Util.UiFontBold;
            _lblGroupTitle.AutoEllipsis = true;
            _lblGroupTitle.Text = "No group selected";
            rightPanel.Controls.Add(_lblGroupTitle);

            split.Panel2.Controls.Add(rightPanel);

            // The centre area sits in a padded host so the two panels keep the
            // same margin from the window edge as the top and bottom bands.
            var centre = new Panel();
            centre.Dock = DockStyle.Fill;
            centre.Padding = new Padding(Util.SideMargin, 0, Util.SideMargin, 2);
            centre.Controls.Add(split);
            Controls.Add(centre);

            // ----- top band
            var top = new TableLayoutPanel();
            top.Dock = DockStyle.Top;
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.ColumnCount = 4;
            top.Padding = new Padding(Util.SideMargin - 3, 6, Util.SideMargin - 3, 6);
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

            top.Controls.Add(MakeLabel("Folder to scan:"), 0, 0);
            _txtRoot = MakeText();
            top.Controls.Add(_txtRoot, 1, 0);
            _btnRoot = MakeButton("Browse...", 88, delegate { Browse(_txtRoot, "Folder to scan"); });
            top.Controls.Add(_btnRoot, 2, 0);
            _btnScan = MakeButton("SCAN", 150, delegate { StartScan(); });
            _btnScan.Height = Util.ButtonHeight + 4;
            HighlightButton(_btnScan, AccentScan);
            AcceptButton = _btnScan;
            top.Controls.Add(_btnScan, 3, 0);

            top.Controls.Add(MakeLabel("Preferred folder:"), 0, 1);
            _txtPreferred = MakeText();
            _tip.SetToolTip(_txtPreferred,
                "Images inside this folder (and its subfolders) are kept by default.");
            top.Controls.Add(_txtPreferred, 1, 1);
            _btnPreferred = MakeButton("Browse...", 88, delegate { Browse(_txtPreferred, "Preferred folder"); });
            top.Controls.Add(_btnPreferred, 2, 1);
            var btnClearPref = MakeButton("Clear", 142, delegate { _txtPreferred.Text = ""; });
            top.Controls.Add(btnClearPref, 3, 1);

            var opts = new FlowLayoutPanel();
            opts.AutoSize = true;
            opts.WrapContents = true;
            opts.Dock = DockStyle.Fill;
            _chkRecursive = MakeCheck("Include subfolders", true);
            _chkCopySuffix = MakeCheck("Ignore copy suffixes: (1), - Copy, copy 2", true);
            _chkNumSuffix = MakeCheck("Also ignore _1 / -1", false);
            _chkContent = MakeCheck("Check content (MD5)", false);
            _tip.SetToolTip(_chkContent, "Slower: confirms the files really are identical.");
            opts.Controls.Add(_chkRecursive);
            opts.Controls.Add(_chkCopySuffix);
            opts.Controls.Add(_chkNumSuffix);
            opts.Controls.Add(_chkContent);
            top.Controls.Add(opts, 0, 2);
            top.SetColumnSpan(opts, 4);

            top.Controls.Add(MakeLabel("Scanned extensions:"), 0, 3);
            _txtExt = MakeText();
            _txtExt.Text = DefaultExtensions;
            top.Controls.Add(_txtExt, 1, 3);
            top.SetColumnSpan(_txtExt, 3);

            Controls.Add(top);

            // ----- bottom band
            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Bottom;
            bottom.AutoSize = true;
            bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bottom.ColumnCount = 4;
            bottom.Padding = new Padding(Util.SideMargin - 3, 6, Util.SideMargin - 3, 6);
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));

            bottom.Controls.Add(MakeLabel("Move duplicates to:"), 0, 0);
            _txtQuarantine = MakeText();
            bottom.Controls.Add(_txtQuarantine, 1, 0);
            _btnQuarantine = MakeButton("Browse...", 88, delegate { Browse(_txtQuarantine, "Destination folder for duplicates"); });
            bottom.Controls.Add(_btnQuarantine, 2, 0);
            _chkPreserveTree = MakeCheck("Keep folder structure", true);
            bottom.Controls.Add(_chkPreserveTree, 3, 0);

            var actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.Dock = DockStyle.Fill;
            actions.WrapContents = true;
            _btnMoveGroup = MakeButton("Move duplicates in group", 235, delegate { MoveSelected(false); });
            _btnMoveAll = MakeButton("Move ALL duplicates", 215, delegate { MoveSelected(true); });
            HighlightButton(_btnMoveAll, AccentMove);
            _btnExport = MakeButton("Export CSV", 145, delegate { ExportCsv(); });
            actions.Controls.Add(_btnMoveGroup);
            actions.Controls.Add(_btnMoveAll);
            actions.Controls.Add(_btnExport);
            _lblSummary = new Label();
            _lblSummary.AutoSize = true;
            _lblSummary.Padding = new Padding(12, 10, 0, 0);
            _lblSummary.Text = "";
            actions.Controls.Add(_lblSummary);
            bottom.Controls.Add(actions, 0, 1);
            bottom.SetColumnSpan(actions, 4);

            Controls.Add(bottom);

            // ----- status bar
            _status = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Ready.");
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _progress = new ToolStripProgressBar();
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.Visible = false;
            _status.Items.Add(_statusLabel);
            _status.Items.Add(_progress);
            Controls.Add(_status);

            split.Panel1MinSize = 320;
            split.Panel2MinSize = 380;
            split.SplitterDistance = 560;
            EnableActions(false);
        }

        Label MakeLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Height = Util.RowHeight;
            return l;
        }

        TextBox MakeText()
        {
            var t = new TextBox();
            t.Dock = DockStyle.Fill;
            t.Margin = new Padding(3, 5, 3, 5);
            return t;
        }

        Button MakeButton(string text, int width, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(width, Util.ButtonHeight);
            b.Padding = new Padding(10, 0, 10, 0);
            b.Margin = new Padding(3, 4, 3, 4);
            b.Click += onClick;
            return b;
        }

        CheckBox MakeCheck(string text, bool check)
        {
            var c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.MinimumSize = new Size(0, Util.RowHeight);
            c.TextAlign = ContentAlignment.MiddleLeft;
            c.CheckAlign = ContentAlignment.MiddleLeft;
            c.Checked = check;
            c.Margin = new Padding(4, 2, 12, 2);
            return c;
        }

        /// <summary>Highlights a primary button with an accent colour.</summary>
        void HighlightButton(Button b, Color back)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.UseVisualStyleBackColor = false;
            b.Font = Util.UiFontBold;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = ControlPaint.Dark(back, 0.1f);
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.2f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.05f);
        }

        void Browse(TextBox target, string description)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = description;
                dlg.ShowNewFolderButton = true;
                if (Directory.Exists(target.Text)) dlg.SelectedPath = target.Text;
                else if (Directory.Exists(_txtRoot.Text)) dlg.SelectedPath = _txtRoot.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) target.Text = dlg.SelectedPath;
            }
        }

        void EnableActions(bool on)
        {
            _btnMoveGroup.Enabled = on;
            _btnMoveAll.Enabled = on;
            _btnMoveAll.BackColor = on ? AccentMove : SystemColors.ControlDark;
            _btnMoveAll.FlatAppearance.BorderColor = ControlPaint.Dark(_btnMoveAll.BackColor, 0.1f);
            _btnExport.Enabled = on;
            _btnKeepPreferred.Enabled = on;
            _btnKeepOldest.Enabled = on;
            _btnKeepNewest.Enabled = on;
            _btnKeepShortest.Enabled = on;
        }

        // ---------------------- Scanning ------------------------------
        void StartScan()
        {
            if (_worker != null && _worker.IsBusy)
            {
                _worker.CancelAsync();
                _statusLabel.Text = "Cancelling...";
                return;
            }

            string root = _txtRoot.Text.Trim();
            if (!Directory.Exists(root))
            {
                MessageBox.Show(this, "Choose a valid folder to scan.", "TwinPix",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pref = _txtPreferred.Text.Trim();
            if (pref.Length > 0 && !Directory.Exists(pref))
            {
                MessageBox.Show(this, "The preferred folder does not exist.", "TwinPix",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var o = new ScanOptions();
            o.Root = root;
            o.Preferred = pref;
            o.Recursive = _chkRecursive.Checked;
            o.IgnoreCopySuffix = _chkCopySuffix.Checked;
            o.IgnoreNumericSuffix = _chkNumSuffix.Checked;
            o.CompareContent = _chkContent.Checked;
            foreach (string raw in _txtExt.Text.Split(new char[] { ';', ',', ' ' },
                                                      StringSplitOptions.RemoveEmptyEntries))
            {
                string e = raw.Trim().ToLowerInvariant();
                if (e.Length == 0) continue;
                if (!e.StartsWith(".")) e = "." + e;
                o.Extensions.Add(e);
            }
            if (o.Extensions.Count == 0)
            {
                MessageBox.Show(this, "Enter at least one extension.", "TwinPix",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ClearResults();
            _btnScan.Text = "CANCEL";
            HighlightButton(_btnScan, AccentStop);
            _progress.Visible = true;
            _statusLabel.Text = "Scanning...";
            Cursor = Cursors.AppStarting;

            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += delegate(object s, DoWorkEventArgs e)
            {
                e.Result = Scanner.Scan(o, (BackgroundWorker)s);
            };
            _worker.ProgressChanged += delegate(object s, ProgressChangedEventArgs e)
            {
                _statusLabel.Text = Convert.ToString(e.UserState);
            };
            _worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                Cursor = Cursors.Default;
                _progress.Visible = false;
                _btnScan.Text = "SCAN";
                HighlightButton(_btnScan, AccentScan);

                if (e.Error != null)
                {
                    _statusLabel.Text = "Error: " + e.Error.Message;
                    MessageBox.Show(this, e.Error.ToString(), "Error during the scan",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (e.Cancelled || e.Result == null)
                {
                    _statusLabel.Text = "Scan cancelled.";
                    return;
                }

                var res = (ScanResult)e.Result;
                _groups = res.Groups;
                FillGroups();
                _statusLabel.Text = res.FilesScanned + " image(s) scanned - "
                                  + _groups.Count + " duplicate group(s)"
                                  + (res.Errors > 0 ? " - " + res.Errors + " unreadable item(s)" : "");
                EnableActions(_groups.Count > 0);
                if (_groups.Count == 0)
                    MessageBox.Show(this, "No duplicates found with these criteria.", "TwinPix",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _worker.RunWorkerAsync();
        }

        void ClearResults()
        {
            _lv.Items.Clear();
            ClearCards();
            _groups = new List<DupGroup>();
            _current = null;
            _lblGroupTitle.Text = "No group selected";
            _lblSummary.Text = "";
            EnableActions(false);
        }

        void ClearCards()
        {
            var old = new List<Control>();
            foreach (Control c in _cards.Controls) old.Add(c);
            _cards.Controls.Clear();
            foreach (var c in old) c.Dispose();
        }

        void FillGroups()
        {
            _lv.BeginUpdate();
            _lv.Items.Clear();
            foreach (var g in _groups)
            {
                var it = new ListViewItem(g.BaseName);
                it.SubItems.Add(g.Extension);
                it.SubItems.Add(Util.FormatSize(g.Size));
                it.SubItems.Add(g.Files.Count.ToString(CultureInfo.InvariantCulture));
                it.SubItems.Add(Util.FormatSize(g.Wasted));
                it.SubItems.Add(KeptText(g));
                it.Tag = g;
                _lv.Items.Add(it);
            }
            _lv.EndUpdate();
            UpdateSummary();
            if (_lv.Items.Count > 0) _lv.Items[0].Selected = true;
        }

        static string KeptText(DupGroup g)
        {
            var k = g.Kept;
            return k == null ? "(none)" : k.DirectoryPath;
        }

        void UpdateSummary()
        {
            long wasted = 0;
            int dups = 0;
            foreach (var g in _groups) { wasted += g.Wasted; dups += g.Files.Count - 1; }
            _lblSummary.Text = _groups.Count + " group(s) - " + dups
                             + " duplicate(s) - " + Util.FormatSize(wasted) + " reclaimable";
        }

        void LvSelectionChanged(object sender, EventArgs e)
        {
            if (_lv.SelectedItems.Count == 0) { _current = null; ClearCards(); return; }
            _current = _lv.SelectedItems[0].Tag as DupGroup;
            ShowGroup(_current);
        }

        void ShowGroup(DupGroup g)
        {
            ClearCards();
            if (g == null)
            {
                _lblGroupTitle.Text = "No group selected";
                return;
            }
            _lblGroupTitle.Text = "Group: " + g.BaseName + g.Extension + "  -  "
                                + Util.FormatSize(g.Size) + "  -  " + g.Files.Count + " files"
                                + "   (click the image to keep)";
            _cards.SuspendLayout();
            _suspend = true;
            foreach (var f in g.Files)
                _cards.Controls.Add(new FileCard(f, CardKeepChanged, _tip));
            _suspend = false;
            _cards.ResumeLayout();
        }

        void CardKeepChanged(object sender, EventArgs e)
        {
            if (_suspend) return;
            var rb = sender as RadioButton;
            if (rb == null || !rb.Checked) return;

            foreach (Control c in _cards.Controls)
            {
                var card = c as FileCard;
                if (card == null) continue;
                if (!ReferenceEquals(card.Rb, rb) && card.Rb.Checked)
                {
                    _suspend = true;
                    card.Rb.Checked = false;
                    _suspend = false;
                }
                card.Entry.Keep = ReferenceEquals(card.Rb, rb);
                card.UpdateStyle();
            }
            RefreshCurrentRow();
        }

        void RefreshCurrentRow()
        {
            if (_current == null) return;
            foreach (ListViewItem it in _lv.Items)
            {
                if (ReferenceEquals(it.Tag, _current))
                {
                    it.SubItems[5].Text = KeptText(_current);
                    break;
                }
            }
        }

        void ApplyRule(Scanner.KeepRule rule)
        {
            if (_groups.Count == 0) return;
            if (_chkApplyAll.Checked)
            {
                foreach (var g in _groups) Scanner.AutoSelect(g, rule);
                foreach (ListViewItem it in _lv.Items)
                {
                    var g = it.Tag as DupGroup;
                    if (g != null) it.SubItems[5].Text = KeptText(g);
                }
            }
            else if (_current != null)
            {
                Scanner.AutoSelect(_current, rule);
                RefreshCurrentRow();
            }
            ShowGroup(_current);
        }

        // ---------------------- Moving --------------------------------
        void MoveSelected(bool all)
        {
            var targets = new List<DupGroup>();
            if (all) targets.AddRange(_groups);
            else if (_current != null) targets.Add(_current);

            if (targets.Count == 0)
            {
                MessageBox.Show(this, "No group selected.", "TwinPix",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string dest = _txtQuarantine.Text.Trim();
            if (dest.Length == 0)
            {
                MessageBox.Show(this, "Enter the folder the duplicates should be moved to.", "TwinPix",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Destination folder cannot be used:\r\n" + ex.Message,
                                "TwinPix", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string root = _txtRoot.Text.Trim();
            if (Scanner.IsUnder(dest, root))
            {
                var r = MessageBox.Show(this,
                    "The destination folder is inside the folder being scanned.\r\n"
                    + "Moved duplicates will be found again by the next scan.\r\n\r\nContinue?",
                    "TwinPix", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            int toMove = 0, noKeep = 0;
            foreach (var g in targets)
            {
                if (g.Kept == null) { noKeep++; continue; }
                toMove += g.Files.Count - 1;
            }
            if (toMove == 0)
            {
                MessageBox.Show(this, "Nothing to move (no image marked as the one to keep).",
                                "TwinPix", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string msg = "Move " + toMove + " file(s) to:\r\n" + dest;
            if (noKeep > 0) msg += "\r\n\r\n" + noKeep + " group(s) will be skipped (no file marked as the one to keep).";
            if (MessageBox.Show(this, msg, "Confirmation",
                                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            int moved = 0;
            var errors = new List<string>();
            Cursor = Cursors.WaitCursor;

            foreach (var g in targets)
            {
                var keep = g.Kept;
                if (keep == null) continue;

                var movedEntries = new List<FileEntry>();
                foreach (var f in g.Files)
                {
                    if (ReferenceEquals(f, keep)) continue;
                    try
                    {
                        string targetDir = dest;
                        if (_chkPreserveTree.Checked && Scanner.IsUnder(f.FullPath, root))
                        {
                            string rel = Path.GetDirectoryName(f.FullPath)
                                             .Substring(Path.GetFullPath(root)
                                             .TrimEnd(Path.DirectorySeparatorChar).Length)
                                             .TrimStart(Path.DirectorySeparatorChar);
                            if (rel.Length > 0) targetDir = Path.Combine(dest, rel);
                        }
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                        string finalPath = Util.UniqueDestination(targetDir, f.FileName);
                        File.Move(f.FullPath, finalPath);
                        movedEntries.Add(f);
                        moved++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(f.FullPath + " : " + ex.Message);
                    }
                }
                foreach (var f in movedEntries) g.Files.Remove(f);
            }

            // drop groups that no longer hold duplicates
            var remaining = new List<DupGroup>();
            foreach (var g in _groups)
                if (g.Files.Count > 1) remaining.Add(g);
            _groups = remaining;

            Cursor = Cursors.Default;
            FillGroups();
            if (_lv.Items.Count == 0) { _current = null; ClearCards(); _lblGroupTitle.Text = "No group left"; }
            EnableActions(_groups.Count > 0);

            string report = moved + " file(s) moved.";
            if (errors.Count > 0)
            {
                report += "\r\n\r\n" + errors.Count + " failure(s):\r\n"
                        + string.Join("\r\n", errors.Take(15).ToArray());
                if (errors.Count > 15) report += "\r\n...";
            }
            _statusLabel.Text = moved + " file(s) moved to " + dest;
            MessageBox.Show(this, report, "TwinPix", MessageBoxButtons.OK,
                            errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // ---------------------- Export CSV ----------------------------
        void ExportCsv()
        {
            if (_groups.Count == 0) return;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV file (*.csv)|*.csv";
                dlg.FileName = "duplicates.csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Group;File;Folder;Size (bytes);Modified;Action");
                    int n = 0;
                    foreach (var g in _groups)
                    {
                        n++;
                        foreach (var f in g.Files)
                        {
                            sb.AppendLine(string.Join(";", new string[]
                            {
                                n.ToString(CultureInfo.InvariantCulture),
                                Csv(f.FileName),
                                Csv(f.DirectoryPath),
                                f.Size.ToString(CultureInfo.InvariantCulture),
                                f.Modified.ToString("yyyy-MM-dd HH:mm:ss"),
                                f.Keep ? "KEEP" : "duplicate"
                            }));
                        }
                    }
                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    _statusLabel.Text = "Export written: " + dlg.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Export failed:\r\n" + ex.Message, "TwinPix",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        static string Csv(string s)
        {
            if (s == null) return "";
            if (s.IndexOf(';') >= 0 || s.IndexOf('"') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_worker != null && _worker.IsBusy) _worker.CancelAsync();
            base.OnFormClosing(e);
        }
    }
}
