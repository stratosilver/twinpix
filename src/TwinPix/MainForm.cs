// =====================================================================
//  TwinPix - main window
//
//  Behaviour only: every control and every pixel of the layout lives in
//  MainForm.Designer.cs, which Visual Studio's form designer reads and
//  rewrites. Nothing here creates a control, so the design surface and
//  the running window can never disagree.
//
//  PHP note: a desktop program is not a request that starts, runs and
//  ends. The window stays open and Windows calls the methods below when
//  something happens - a click, a resize, a key. Two rules follow:
//    - the window may only be touched from its own thread (the "UI
//      thread"), which must never be kept busy, or the window freezes;
//    - long work (scanning, moving files) therefore runs on a
//      BackgroundWorker, which reports back on the UI thread.
// =====================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TwinPix
{
    /// <summary>
    /// The main window. PHP note: "partial" - the class continues in
    /// MainForm.Designer.cs, which declares and lays out the controls
    /// (the _lv, _cboRoot, _btnScan... fields used here).
    /// </summary>
    public partial class MainForm : Form
    {
        // Keys of the remembered values in FolderHistory.
        private const string KeyScan = "scan";
        private const string KeyDestination = "destination";
        private const string KeyWindow = "window";

        private const string AppVersion = "1.0";

        /// <summary>Share of the window given to the lists when it first opens.</summary>
        private const double ListWidthRatio = 0.6;

        /// <summary>Share of the left column given to the group list, the folder list taking the rest.</summary>
        private const double GroupListHeightRatio = 0.6;

        /// <summary>Height of every field and every button of the form, in pixels.</summary>
        private const int FieldHeight = 38;

        /// <summary>The cards always go two to a row, each half the panel's width.</summary>
        private const int CardsPerRow = 2;

        /// <summary>At most this many lines of a list are shown in a report dialog.</summary>
        private const int ReportLines = 8;

        // Fingerprints survive from one scan to the next, and from one run of
        // the program to the next: a second visual scan decodes almost nothing.
        private readonly FingerprintCache _fingerprints = new FingerprintCache();
        private bool _cacheLoaded;

        private readonly FolderHistory _history = new FolderHistory();

        // Scanning
        private BackgroundWorker _scanWorker;
        private ScanOptions _scanOptions;       // what the running (or last) scan was asked
        private bool _hasScanned;               // a scan has filled the list at least once
        private string _scannedRoot = "";       // full path of the folder the list was built from
        private List<DupGroup> _groups = new List<DupGroup>();
        private DupGroup _current;              // the group whose cards are shown

        // Removing duplicates
        private BackgroundWorker _removalWorker;
        private DuplicateRemover _remover;
        private RemovalJournal _journal;
        private RemovalSettings _removalSettings;
        private List<RemovalItem> _removalItems;

        // Guards against events echoing while the code itself updates controls.
        private bool _suspendRules;             // the Keep check boxes
        private bool _suspendCards;             // the Keep buttons of the cards
        private bool _fillingFolders;           // the Preferred folders list

        // Folders ticked in the Preferred folders list, full paths. Kept apart
        // from the list itself so a new scan of the same folder keeps the ticks.
        private readonly HashSet<string> _preferred = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<ScannedFolder> _folders = new List<ScannedFolder>();
        private string _foldersRoot = "";       // the scanned folder the list was built for

        // Sort state of the duplicate-group list.
        private int _sortColumn = GroupComparer.ColumnReclaimable;
        private bool _sortAscending;            // largest first

        public MainForm()
        {
            ToolStripManager.RenderMode = ToolStripManagerRenderMode.System;
            InitializeComponent();              // builds every control: see MainForm.Designer.cs

            // What the designer cannot hold: values that depend on the machine,
            // on an embedded resource, or on a property it does not serialize.
            Icon appIcon = UiStyle.AppIcon();
            if (appIcon != null) Icon = appIcon;

            _chkTrash.Enabled = Native.IsWindows;
            _progress.Visible = false;

            // Defaults: visual matching, strict sensitivity. The sensitivity is
            // chosen first; selecting the mode then runs MatchModeChanged, which
            // enables the sensitivity box and ticks "keep the best copy".
            _cboSensitivity.SelectedIndex = 0;    // Strict - re-saved copies
            _cboMatch.SelectedIndex = 1;          // Same picture (visual)

            SyncRuleMenu();
            TrashModeChanged();
            EnableActions(false);

            _history.Load();
            FillCombo(_cboRoot, KeyScan);
            FillCombo(_cboQuarantine, KeyDestination);
            RestorePlacement();
        }

        /// <summary>True while a scan or a removal runs: nothing else may start.</summary>
        private bool IsBusy
        {
            get { return IsScanning || IsRemoving; }
        }

        private bool IsScanning
        {
            get { return _scanWorker != null && _scanWorker.IsBusy; }
        }

        private bool IsRemoving
        {
            get { return _removalWorker != null && _removalWorker.IsBusy; }
        }

        // ---------------------- Window plumbing -----------------------

        /// <summary>
        /// Gives each group box the height its content ended up needing, and
        /// hooks the handlers that keep the layout right while the window is
        /// resized. An auto-sizing GroupBox wrapped around a docked auto-sizing
        /// grid can send the .NET Framework layout engine into a loop, so the
        /// size is taken once, after the first layout pass.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FitGroupBox(_sourceBox, _sourceGrid);
            FitGroupBox(_destBox, _destGrid);

            // Each band follows the height of its grid, should a row ever change
            // height (a caption that wraps, a row added in the designer).
            _sourceGrid.SizeChanged += (s, a) => FitGroupBox(_sourceBox, _sourceGrid);
            _destGrid.SizeChanged += (s, a) => FitGroupBox(_destBox, _destGrid);

            // The cards follow the width of the right-hand panel.
            _cards.SizeChanged += (s, a) => LayoutCards();

            // The splitters are drawn as a band with a grip.
            foreach (SplitContainer sc in new[] { _split, _splitLists })
            {
                SplitContainer target = sc;     // one variable per loop turn, captured by the lambdas
                sc.Paint += PaintSplitter;
                sc.SplitterMoved += (s, a) => target.Invalidate();
                sc.SizeChanged += (s, a) => target.Invalidate();
            }

            // The extensions field is a multi-line box kept to one centred line.
            _txtExt.SizeChanged += (s, a) => Native.CenterSingleLine(_txtExt);
            _txtExt.TextChanged += (s, a) => KeepOnOneLine(_txtExt);

            // A resized combo box puts its text field back at the top: centre it again.
            foreach (ComboBox c in new[] { _cboRoot, _cboQuarantine })
            {
                ComboBox target = c;
                c.SizeChanged += (s, a) => Native.CenterComboEdit(target);
            }
        }

        /// <summary>The finishing touches that need a live window handle.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            Native.UseExplorerTheme(_lv);
            Native.EnableDoubleBuffer(_lv);
            Native.UseExplorerTheme(_lvFolders);
            Native.EnableDoubleBuffer(_lvFolders);
            Native.SetCueBanner(_cboRoot, "Folder to search for duplicates");
            Native.SetCueBanner(_cboQuarantine, "Where the duplicates are moved");
            ApplyFieldHeights();

            ApplySplitLayout();
            ApplyListsLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (IsRemoving)
            {
                // Closing now would stop the moves half-way through: the files
                // would be safe, but the list and the report would be lost.
                e.Cancel = true;
                Native.Show(this, "TwinPix", "Duplicates are being moved",
                            "Wait until it is done, or press STOP first.",
                            MessageBoxButtons.OK, Native.DialogIcon.Information);
                return;
            }
            if (IsScanning) _scanWorker.CancelAsync();
            if (_cacheLoaded) _fingerprints.Save();
            SavePlacement();
            _history.Add(KeyScan, _cboRoot.Text);
            _history.Add(KeyDestination, _cboQuarantine.Text);
            _history.Save();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// The combo boxes get their height in the designer: DrawMode is
        /// OwnerDrawFixed and ItemHeight sets the height of the selection field
        /// (the box is ItemHeight + 6). Two things are left for run time, and
        /// need a live window handle: in the editable boxes (folder to scan,
        /// destination) the text field is moved to the middle of that height,
        /// and the extensions box - a multi-line box, the only kind of TextBox
        /// that can be taller than its font - gets its line centred.
        /// </summary>
        private void ApplyFieldHeights()
        {
            foreach (ComboBox c in new[] { _cboRoot, _cboQuarantine })
                Native.CenterComboEdit(c);
            _txtExt.MinimumSize = new Size(0, FieldHeight);
            _txtExt.Height = FieldHeight;
            Native.CenterSingleLine(_txtExt);
        }

        /// <summary>
        /// Draws one entry of an owner-drawn combo box - the selection field or a
        /// row of the drop-down list - as the system would: themed colours,
        /// greyed when the box is disabled, text centred on the row's height.
        /// </summary>
        private void Combo_DrawItem(object sender, DrawItemEventArgs e)
        {
            var combo = sender as ComboBox;      // PHP note: "as" gives null instead of failing
            if (combo == null) return;
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < combo.Items.Count)
            {
                bool disabled = (e.State & DrawItemState.Disabled) != 0 || !combo.Enabled;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color fore = disabled ? SystemColors.GrayText
                           : selected ? SystemColors.HighlightText
                           : combo.ForeColor;
                Rectangle r = Rectangle.FromLTRB(e.Bounds.Left + 3, e.Bounds.Top,
                                                 e.Bounds.Right - 2, e.Bounds.Bottom);
                TextRenderer.DrawText(e.Graphics, combo.GetItemText(combo.Items[e.Index]),
                                      combo.Font, r, fore,
                                      TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                                      | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                                      | TextFormatFlags.SingleLine);
            }
            e.DrawFocusRectangle();
        }

        /// <summary>A pasted line break becomes a separator: the field holds one line.</summary>
        private static void KeepOnOneLine(TextBox t)
        {
            if (t.Text.IndexOf('\r') < 0 && t.Text.IndexOf('\n') < 0) return;
            int caret = t.SelectionStart;
            t.Text = t.Text.Replace("\r\n", ";").Replace('\r', ';').Replace('\n', ';');
            t.SelectionStart = Math.Min(caret, t.Text.Length);
        }

        /// <summary>
        /// Sizes every card to half the panel's width. Room for the vertical
        /// scroll bar is always kept, so the bar showing up or going away never
        /// changes the widths.
        /// </summary>
        private void LayoutCards()
        {
            int usable = _cards.Width - _cards.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth;
            if (usable <= 0 || _cards.Controls.Count == 0) return;

            _cards.SuspendLayout();
            foreach (Control c in _cards.Controls)
            {
                var card = c as FileCard;
                if (card == null) continue;
                card.SetCardWidth(usable / CardsPerRow - card.Margin.Horizontal);
            }
            _cards.ResumeLayout(true);
        }

        private static void FitGroupBox(GroupBox box, Control content)
        {
            if (box == null || content == null) return;
            const int CaptionBand = 22;
            int inner = Math.Max(content.Height, content.PreferredSize.Height);
            int needed = inner + box.Padding.Vertical + CaptionBand;
            if (needed > 0 && needed != box.Height) box.Height = needed;
        }

        /// <summary>
        /// Draws the two splitters - between the lists and the thumbnails, and
        /// between the group list and the folder list - as a visible band with a
        /// grip in its middle, so it is plain where to grab them.
        /// </summary>
        private static void PaintSplitter(object sender, PaintEventArgs e)
        {
            var split = sender as SplitContainer;
            if (split == null) return;
            Rectangle r = split.SplitterRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;

            using (var band = new SolidBrush(UiStyle.DividerGray))
                e.Graphics.FillRectangle(band, r);

            bool across = split.Orientation == Orientation.Horizontal;     // a horizontal bar
            using (var edge = new Pen(UiStyle.BorderGray))
            {
                if (across)
                {
                    e.Graphics.DrawLine(edge, r.Left, r.Top, r.Right, r.Top);
                    e.Graphics.DrawLine(edge, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
                }
                else
                {
                    e.Graphics.DrawLine(edge, r.Left, r.Top, r.Left, r.Bottom);
                    e.Graphics.DrawLine(edge, r.Right - 1, r.Top, r.Right - 1, r.Bottom);
                }
            }

            // grip: five dots across the middle of the band
            using (var dot = new SolidBrush(SystemColors.ControlDark))
            {
                int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
                for (int i = -2; i <= 2; i++)
                {
                    int x = across ? cx + i * 6 : cx;
                    int y = across ? cy : cy + i * 6;
                    e.Graphics.FillRectangle(dot, x - 1, y - 1, 3, 3);
                }
            }
        }

        /// <summary>The duplicate groups get 60 % of the left column, the folders the rest.</summary>
        private void ApplyListsLayout()
        {
            try
            {
                int h = _splitLists.Height;
                if (h < 100) return;
                _splitLists.Panel1MinSize = 0;
                _splitLists.Panel2MinSize = 0;
                _splitLists.SplitterDistance = (int)(h * GroupListHeightRatio);
                _splitLists.Panel1MinSize = Math.Min(80, h / 4);
                _splitLists.Panel2MinSize = Math.Min(80, h / 4);
            }
            catch (InvalidOperationException) { }   // a layout detail is never worth losing the window over
            catch (ArgumentException) { }
        }

        /// <summary>
        /// Gives the lists 60 % of the width. The minimums are cleared first,
        /// then the position, then the minimums again: each of the three
        /// properties is validated against the other two, and any other order
        /// can be rejected on a narrow window.
        /// </summary>
        private void ApplySplitLayout()
        {
            try
            {
                int width = _split.Width;
                if (width < 100) return;

                int min1 = Math.Min(320, width / 4);
                int min2 = Math.Min(244, width / 4);    // one card plus its scrollbar

                _split.Panel1MinSize = 0;
                _split.Panel2MinSize = 0;

                int wanted = (int)(width * ListWidthRatio);
                int max = width - _split.SplitterWidth - min2;
                if (wanted > max) wanted = max;
                if (wanted < min1) wanted = min1;
                if (wanted < 0) wanted = 0;
                _split.SplitterDistance = wanted;

                _split.Panel1MinSize = min1;
                _split.Panel2MinSize = min2;
            }
            catch (InvalidOperationException) { }   // a layout detail is never worth losing the window over
            catch (ArgumentException) { }
        }

        private void ShowAbout()
        {
            Native.Show(this, "About TwinPix", "TwinPix " + AppVersion,
                "Finds duplicate images - by content, or by what the picture "
                + "actually looks like - then moves the copies you do not keep to a "
                + "folder of your choice or to the Recycle Bin, after checking each "
                + "one again against the copy that stays.\r\n\r\n"
                + "Built with the C# compiler shipped with Windows.",
                MessageBoxButtons.OK, Native.DialogIcon.Information);
        }

        /// <summary>
        /// The Recycle Bin needs no destination: the folder field, its Browse
        /// button and the folder-structure box are greyed out while it is ticked.
        /// </summary>
        private void TrashModeChanged()
        {
            bool toFolder = !_chkTrash.Checked;
            _lblQuarantine.Enabled = toFolder;
            _cboQuarantine.Enabled = toFolder;
            _btnQuarantine.Enabled = toFolder;
            _chkPreserveTree.Enabled = toFolder;
            _btnMoveAll.Text = toFolder ? "MOVE &ALL" : "TRASH &ALL";
            _miMoveAll.Text = toFolder ? "Move &all duplicates" : "Send &all duplicates to the trash";
        }

        private void EnableActions(bool on)
        {
            _miMoveAll.Enabled = on;
            _miExport.Enabled = on;
            _btnMoveAll.Enabled = on;
            // the keep rules stay available: they are settings, not actions
        }

        // ---------------------- Folder fields -------------------------

        /// <summary>Loads a field's remembered folders, keeping what is typed in it.</summary>
        private void FillCombo(ComboBox c, string key)
        {
            string current = c.Text;
            c.Items.Clear();
            List<string> list = _history.Get(key);
            foreach (string folder in list) c.Items.Add(folder);
            c.Text = current.Length > 0 ? current : (list.Count > 0 ? list[0] : "");
        }

        /// <summary>Moves the field's current folder to the top of its history.</summary>
        private void RememberFolder(ComboBox c, string key)
        {
            _history.Add(key, c.Text);
            _history.Save();
            FillCombo(c, key);
        }

        /// <summary>Empties one field's drop-down list, on disk as well.</summary>
        private void ClearHistory(ComboBox c, string key)
        {
            _history.Clear(key);
            _history.Save();
            FillCombo(c, key);
        }

        private void Browse(ComboBox target, string description, string historyKey)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = description;
                dlg.ShowNewFolderButton = true;
                if (Directory.Exists(target.Text)) dlg.SelectedPath = target.Text;
                else if (Directory.Exists(_cboRoot.Text)) dlg.SelectedPath = _cboRoot.Text;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                target.Text = dlg.SelectedPath;
                RememberFolder(target, historyKey);
            }
        }

        // ---------------------- Scanning ------------------------------

        /// <summary>Starts a scan - or, while one runs, cancels it (the button then reads CANCEL).</summary>
        private void StartScan()
        {
            if (IsRemoving) return;
            if (IsScanning)
            {
                _scanWorker.CancelAsync();
                _statusLabel.Text = "Cancelling...";
                return;
            }

            string root = _cboRoot.Text.Trim();
            if (!PathHelper.IsAbsolute(root) || !Directory.Exists(root))
            {
                Native.Show(this, "TwinPix", "Choose a folder to scan",
                            "Enter a full path, such as D:\\Photos, or use Browse. "
                            + "The path is empty, incomplete, or no longer exists on this computer.",
                            MessageBoxButtons.OK, Native.DialogIcon.Warning);
                return;
            }

            ScanOptions options = ReadScanOptions(root);
            if (options.Extensions.Count == 0)
            {
                Native.Show(this, "TwinPix", "Enter at least one extension",
                            "The scan needs to know which files count as images, for example .jpg;.png",
                            MessageBoxButtons.OK, Native.DialogIcon.Warning);
                return;
            }

            RememberFolder(_cboRoot, KeyScan);

            ClearResults();
            _btnScan.Text = "CANCEL";
            _progress.Visible = true;
            _statusLabel.Text = "Scanning...";
            Cursor = Cursors.AppStarting;

            _scanOptions = options;
            _scanWorker = new BackgroundWorker();
            _scanWorker.WorkerReportsProgress = true;
            _scanWorker.WorkerSupportsCancellation = true;
            _scanWorker.DoWork += ScanWorker_DoWork;
            _scanWorker.ProgressChanged += Worker_ProgressChanged;
            _scanWorker.RunWorkerCompleted += ScanWorker_RunWorkerCompleted;
            _scanWorker.RunWorkerAsync(options);
        }

        /// <summary>The scan options, from the controls.</summary>
        private ScanOptions ReadScanOptions(string root)
        {
            var o = new ScanOptions();
            o.Root = root;
            o.Recursive = _chkRecursive.Checked;
            o.Mode = SelectedMode;
            o.MaxDistance = SelectedDistance;
            o.FineCheck = o.Mode == MatchMode.Visual && _cboSensitivity.SelectedIndex == 1;   // Normal
            if (o.Mode == MatchMode.Visual)
            {
                // Read from disk once per run, and only when it is of use.
                if (!_cacheLoaded)
                {
                    Cursor = Cursors.WaitCursor;
                    _fingerprints.Load();
                    Cursor = Cursors.Default;
                    _cacheLoaded = true;
                }
                o.Cache = _fingerprints;
            }

            foreach (string raw in _txtExt.Text.Split(new[] { ';', ',', ' ', '\r', '\n' },
                                                      StringSplitOptions.RemoveEmptyEntries))
            {
                string ext = raw.Trim().ToLowerInvariant();
                if (ext.Length == 0) continue;
                if (!ext.StartsWith(".", StringComparison.Ordinal)) ext = "." + ext;
                o.Extensions.Add(ext);
            }

            // The destination of the moves is never scanned: the copies set
            // aside there would come back as duplicates, and a keep rule could
            // pick one of them as the copy to keep - sending the original after
            // it. See ScanOptions.ExcludedFolders.
            string dest = _cboQuarantine.Text.Trim();
            if (PathHelper.IsAbsolute(dest) && PathHelper.IsStrictlyUnder(dest, root))
                o.ExcludedFolders.Add(dest);
            return o;
        }

        /// <summary>
        /// Runs on the background thread: must not touch any control.
        /// PHP note: this and the two handlers below are attached with "+="
        /// in StartScan; the worker calls them at the right time, on the right
        /// thread.
        /// </summary>
        private void ScanWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            e.Result = Scanner.Scan((ScanOptions)e.Argument, worker);
            // Scan() returns what it has when it is stopped; saying so here is
            // what makes RunWorkerCompleted report a cancellation rather than
            // an empty result.
            if (worker.CancellationPending) e.Cancel = true;
        }

        /// <summary>Progress of a scan or of a removal, back on the UI thread: into the status bar.</summary>
        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            _statusLabel.Text = Convert.ToString(e.UserState, CultureInfo.CurrentCulture);
        }

        /// <summary>The scan is over. Back on the UI thread: the lists are filled.</summary>
        private void ScanWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Cursor = Cursors.Default;
            _progress.Visible = false;
            _btnScan.Text = "&SCAN";

            if (e.Error != null)
            {
                _statusLabel.Text = "Error: " + e.Error.Message;
                Native.Show(this, "TwinPix", "The scan failed", e.Error.Message,
                            MessageBoxButtons.OK, Native.DialogIcon.Error);
                return;
            }
            if (e.Cancelled || e.Result == null)
            {
                _statusLabel.Text = "Scan cancelled.";
                return;
            }

            var result = (ScanResult)e.Result;
            _hasScanned = true;
            _groups = result.Groups;
            _scannedRoot = PathHelper.NormalizeFolder(_scanOptions.Root);
            FillGroups();
            FillFolders(_scanOptions.Root, result.Folders);
            ApplyRule();          // honour whatever is ticked in the Keep bar and folder list

            bool visual = _scanOptions.Mode == MatchMode.Visual;
            _statusLabel.Text = ScanReport(result, visual);
            if (visual) _fingerprints.Save();

            EnableActions(_groups.Count > 0);
            if (_groups.Count == 0)
                Native.Show(this, "TwinPix", "No duplicates found",
                            visual
                            ? "No two images in this folder look like the same picture.\r\n\r\n"
                              + "A looser sensitivity finds copies that were cropped or retouched."
                            : "No two images in this folder have exactly the same bytes.",
                            MessageBoxButtons.OK, Native.DialogIcon.Information);
        }

        /// <summary>One line for the status bar: what the scan found and what it left out.</summary>
        private string ScanReport(ScanResult result, bool visual)
        {
            string report = result.FilesScanned + " image(s) scanned - " + _groups.Count + " duplicate group(s)";
            if (visual)
            {
                report += " - " + result.Fingerprinted + " fingerprinted";
                if (result.FromCache > 0) report += ", " + result.FromCache + " from cache";
                if (result.Skipped > 0) report += " - " + result.Skipped + " too small, too plain or multi-page";
            }
            if (result.LinksIgnored > 0) report += " - " + result.LinksIgnored + " link(s) ignored";
            if (result.OnlineOnly > 0) report += " - " + result.OnlineOnly + " online-only file(s) left out";
            if (result.FoldersLeftOut > 0) report += " - " + result.FoldersLeftOut + " system, linked or destination folder(s) left out";
            if (result.Errors > 0) report += " - " + result.Errors + " unreadable item(s)";
            return report;
        }

        private void ClearResults()
        {
            _lv.Items.Clear();
            ClearCards();
            _groups = new List<DupGroup>();
            _current = null;
            _lblGroupTitle.Text = "No group selected";
            _paneGroups.Text = "";
            _paneDuplicates.Text = "";
            _paneReclaimable.Text = "";
            EnableActions(false);
        }

        private void ClearCards()
        {
            var old = new List<Control>();
            foreach (Control c in _cards.Controls) old.Add(c);
            _cards.Controls.Clear();
            foreach (Control c in old) c.Dispose();
        }

        private void FillGroups()
        {
            _lv.BeginUpdate();
            _lv.ListViewItemSorter = null;      // one sort pass at the end, not one per row
            _lv.Items.Clear();
            foreach (DupGroup g in _groups)
            {
                var it = new ListViewItem(g.DisplayName);
                it.ImageKey = EnsureFileIcon(g.Extension);
                it.SubItems.Add(g.Extension);
                it.SubItems.Add(Format.FileSize(g.Size));
                it.SubItems.Add(g.Files.Count.ToString(CultureInfo.InvariantCulture));
                it.SubItems.Add(Format.FileSize(g.Wasted));
                it.SubItems.Add(KeptText(g));
                it.Tag = g;
                _lv.Items.Add(it);
            }
            _lv.EndUpdate();
            ApplySort();
            UpdateSummary();
            if (_lv.Items.Count > 0) _lv.Items[0].Selected = true;
        }

        private static string KeptText(DupGroup g)
        {
            FileEntry k = g.Kept;
            return k == null ? "(none)" : k.DirectoryPath;
        }

        private void UpdateSummary()
        {
            long wasted = 0;
            int dups = 0;
            foreach (DupGroup g in _groups) { wasted += g.Wasted; dups += g.Files.Count - 1; }
            _paneGroups.Text = _groups.Count + " group(s)";
            _paneDuplicates.Text = dups + " duplicate(s)";
            _paneReclaimable.Text = Format.FileSize(wasted) + " reclaimable";
        }

        private void ApplySort()
        {
            _lv.ListViewItemSorter = new GroupComparer(_sortColumn, _sortAscending);
            _lv.Sort();
            Native.SetSortArrow(_lv, _sortColumn, _sortAscending);
        }

        /// <summary>Shows the cards of one group on the right-hand side.</summary>
        private void ShowGroup(DupGroup g)
        {
            ClearCards();
            if (g == null)
            {
                _lblGroupTitle.Text = "No group selected";
                return;
            }
            // the panel is narrow by default, so the title stays short
            string sizes = g.SizesDiffer
                         ? Format.FileSize(g.SizeMin) + " to " + Format.FileSize(g.Size)
                         : Format.FileSize(g.Size);
            _lblGroupTitle.Text = sizes + " " + g.Extension + "  -  " + g.Files.Count + " files";
            _tip.SetToolTip(_lblGroupTitle,
                g.Visual
                ? g.Files.Count + " files showing the same picture, " + sizes
                  + "\r\nThe copies may differ in size, format and quality."
                  + "\r\nClick an image to mark it as the one to keep."
                : g.Files.Count + " files of exactly " + Format.FileSize(g.Size)
                  + " with the " + g.Extension + " extension"
                  + "\r\nClick an image to mark it as the one to keep.");
            _cards.SuspendLayout();
            _suspendCards = true;
            foreach (FileEntry f in g.Files)
            {
                var card = new FileCard();
                card.Bind(f, g.Visual, _tip);
                card.KeepChanged += CardKeepChanged;
                _cards.Controls.Add(card);
            }
            LayoutCards();
            _suspendCards = false;
            _cards.ResumeLayout();
        }

        /// <summary>One card has been picked: every other one in the group lets go.</summary>
        private void CardKeepChanged(object sender, EventArgs e)
        {
            if (_suspendCards) return;
            var chosen = sender as FileCard;
            if (chosen == null || !chosen.KeepChecked) return;

            _suspendCards = true;
            foreach (Control c in _cards.Controls)
            {
                var card = c as FileCard;
                if (card == null) continue;
                bool keep = ReferenceEquals(card, chosen);
                card.KeepChecked = keep;
                if (card.Entry != null) card.Entry.Keep = keep;
                card.UpdateStyle();
            }
            _suspendCards = false;
            RefreshCurrentRow();
        }

        /// <summary>Both the kept file and its folder change with the selection.</summary>
        private static void RefreshRow(ListViewItem item, DupGroup g)
        {
            item.Text = g.DisplayName;
            item.SubItems[GroupComparer.ColumnKeptIn].Text = KeptText(g);
        }

        private void RefreshCurrentRow()
        {
            if (_current == null) return;
            foreach (ListViewItem it in _lv.Items)
            {
                if (ReferenceEquals(it.Tag, _current))
                {
                    RefreshRow(it, _current);
                    break;
                }
            }
        }

        // ---------------------- Keep rules ----------------------------

        /// <summary>The rule boxes, exactly one of which is always ticked.</summary>
        private CheckBox[] RuleChecks
        {
            get
            {
                return new[] { _chkKeepOldest, _chkKeepNewest, _chkKeepShortest, _chkKeepBest, _chkKeepLargest };
            }
        }

        private KeepRule CurrentRule
        {
            get
            {
                if (_chkKeepOldest.Checked) return KeepRule.Oldest;
                if (_chkKeepNewest.Checked) return KeepRule.Newest;
                if (_chkKeepBest.Checked) return KeepRule.BestResolution;
                if (_chkKeepLargest.Checked) return KeepRule.LargestFile;
                return KeepRule.ShortestPath;
            }
        }

        /// <summary>
        /// Keeps the rule boxes mutually exclusive - and never all of them off
        /// - then re-applies the selection to every group.
        /// </summary>
        private void RuleChanged(CheckBox source)
        {
            if (_suspendRules) return;
            _suspendRules = true;
            try
            {
                if (source != null)
                {
                    if (!source.Checked)
                    {
                        source.Checked = true;      // a rule is always active
                    }
                    else
                    {
                        foreach (CheckBox c in RuleChecks)
                            if (c != source) c.Checked = false;
                    }
                }
                SyncRuleMenu();
            }
            finally { _suspendRules = false; }

            ApplyRule();
        }

        /// <summary>
        /// Visual matching puts copies of different sizes in one group, where
        /// "shortest path" says nothing useful, so the rule that keeps the best
        /// copy is turned on with it. The list is then rebuilt.
        /// </summary>
        private void MatchModeChanged()
        {
            bool visual = SelectedMode == MatchMode.Visual;
            _cboSensitivity.Enabled = visual;
            _lblSensitivity.Enabled = visual;
            if (visual && !_chkKeepBest.Checked) _chkKeepBest.Checked = true;
            ScanOptionChanged();
        }

        private MatchMode SelectedMode
        {
            get { return _cboMatch.SelectedIndex == 1 ? MatchMode.Visual : MatchMode.Content; }
        }

        /// <summary>Bits out of 64 that two copies may differ by, from the Sensitivity list.</summary>
        private int SelectedDistance
        {
            get
            {
                switch (_cboSensitivity.SelectedIndex)
                {
                    case 0: return 3;       // Strict: a re-saved or resized copy
                    case 2: return 10;      // Loose: cropped edges, a light retouch
                    default: return 6;      // Normal
                }
            }
        }

        /// <summary>
        /// "Include subfolders", "Matching" and "Sensitivity" change what a scan
        /// finds, so the folder is searched again the moment one of them changes.
        /// Before the first scan there is nothing to refresh, and while a scan is
        /// running the new setting simply applies to the next one.
        /// </summary>
        private void ScanOptionChanged()
        {
            if (!_hasScanned || IsBusy) return;
            StartScan();
        }

        // ---------------------- Preferred folders ---------------------

        /// <summary>
        /// Lists every folder below the scanned one, each with a box that marks it
        /// as preferred. Ticks survive a new scan of the same folder; a different
        /// folder starts with none.
        /// </summary>
        private void FillFolders(string root, List<ScannedFolder> folders)
        {
            // NormalizeFolder keeps the separator of a drive root: "C:\", never
            // "C:" - which Windows reads as "the current folder of drive C".
            string fullRoot = PathHelper.NormalizeFolder(root);
            if (!string.Equals(fullRoot, _foldersRoot, StringComparison.OrdinalIgnoreCase))
                _preferred.Clear();
            _foldersRoot = fullRoot;

            _folders = new List<ScannedFolder>();
            foreach (ScannedFolder f in folders)
            {
                // the same form as FileEntry.DirectoryPath, which the ticks are compared with
                f.Path = PathHelper.NormalizeFolder(f.Path);
                // the scanned folder itself is not offered: preferring it would prefer everything
                if (string.Equals(f.Path, fullRoot, StringComparison.OrdinalIgnoreCase)) continue;
                _folders.Add(f);
            }
            _folders.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));

            // ticks on folders that are gone are dropped
            _preferred.IntersectWith(_folders.Select(f => f.Path));

            Dictionary<string, int> inGroups = FilesInGroupsPerFolder();

            _fillingFolders = true;
            _lvFolders.BeginUpdate();
            try
            {
                _lvFolders.Items.Clear();
                foreach (ScannedFolder f in _folders)
                {
                    var it = new ListViewItem(RelativeFolder(f.Path));
                    it.SubItems.Add(f.Images.ToString(CultureInfo.InvariantCulture));
                    int n;
                    inGroups.TryGetValue(f.Path, out n);
                    it.SubItems.Add(n.ToString(CultureInfo.InvariantCulture));
                    it.Tag = f;
                    it.Checked = _preferred.Contains(f.Path);
                    if (n == 0) it.ForeColor = SystemColors.GrayText;
                    _lvFolders.Items.Add(it);
                }
            }
            finally
            {
                _lvFolders.EndUpdate();
                _fillingFolders = false;
            }
            _lblFolders.Text = _folders.Count == 0 ? "&Preferred folders - no subfolder" : "&Preferred folders";
            SyncPreferredBox();
        }

        /// <summary>After a move the counts change; the ticks and the order do not.</summary>
        private void RefreshFolderCounts()
        {
            Dictionary<string, int> inGroups = FilesInGroupsPerFolder();
            _lvFolders.BeginUpdate();
            foreach (ListViewItem it in _lvFolders.Items)
            {
                var f = it.Tag as ScannedFolder;
                if (f == null) continue;
                int n;
                inGroups.TryGetValue(f.Path, out n);
                it.SubItems[2].Text = n.ToString(CultureInfo.InvariantCulture);
                it.ForeColor = n == 0 ? SystemColors.GrayText : SystemColors.WindowText;
            }
            _lvFolders.EndUpdate();
        }

        /// <summary>How many files of the duplicate groups sit directly in each folder.</summary>
        private Dictionary<string, int> FilesInGroupsPerFolder()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DupGroup g in _groups)
                foreach (FileEntry f in g.Files)
                {
                    string dir = f.DirectoryPath;
                    int n;
                    counts.TryGetValue(dir, out n);     // n stays 0 when the folder is not counted yet
                    counts[dir] = n + 1;
                }
            return counts;
        }

        private string RelativeFolder(string path)
        {
            string relative = PathHelper.RelativePath(path, _foldersRoot);
            return string.IsNullOrEmpty(relative) ? PathHelper.FullPathOrSelf(path) : relative;
        }

        /// <summary>
        /// The Preferred folders box of the Keep bar follows the list: available
        /// and ticked as soon as one folder is, greyed out and clear when none is.
        /// </summary>
        private void SyncPreferredBox()
        {
            bool any = _preferred.Count > 0;
            if (_chkKeepPreferred.Enabled == any) return;
            _suspendRules = true;
            _chkKeepPreferred.Enabled = any;
            _chkKeepPreferred.Checked = any;
            _suspendRules = false;
            SyncRuleMenu();
        }

        /// <summary>
        /// One folder ticked or unticked: every folder below it follows, then the
        /// selection is redone at once. Unticking a subfolder afterwards leaves
        /// its parent ticked - the parent's own files stay preferred.
        /// </summary>
        private void PreferredFolderToggled(ListViewItem item)
        {
            if (_fillingFolders || item == null) return;
            var f = item.Tag as ScannedFolder;
            if (f == null) return;

            bool on = item.Checked;
            _fillingFolders = true;                 // the ticks below are not new clicks
            _lvFolders.BeginUpdate();
            try
            {
                foreach (ListViewItem it in _lvFolders.Items)
                {
                    var sub = it.Tag as ScannedFolder;
                    if (sub == null) continue;
                    bool concerned = ReferenceEquals(sub, f) || PathHelper.IsStrictlyUnder(sub.Path, f.Path);
                    if (!concerned) continue;
                    if (it.Checked != on) it.Checked = on;
                    if (on) _preferred.Add(sub.Path);
                    else _preferred.Remove(sub.Path);
                }
            }
            finally
            {
                _lvFolders.EndUpdate();
                _fillingFolders = false;
            }
            SyncPreferredBox();
            ApplyRule();
        }

        /// <summary>Is this file directly inside one of the ticked folders?</summary>
        private bool IsInPreferredFolder(FileEntry f)
        {
            if (_preferred.Count == 0 || f == null) return false;
            string dir = f.DirectoryPath;
            return dir.Length > 0 && _preferred.Contains(dir);
        }

        private void SyncRuleMenu()
        {
            _miKeepPreferred.Checked = _chkKeepPreferred.Checked;
            _miKeepPreferred.Enabled = _chkKeepPreferred.Enabled;
            _miKeepOldest.Checked = _chkKeepOldest.Checked;
            _miKeepNewest.Checked = _chkKeepNewest.Checked;
            _miKeepShortest.Checked = _chkKeepShortest.Checked;
            _miKeepBest.Checked = _chkKeepBest.Checked;
            _miKeepLargest.Checked = _chkKeepLargest.Checked;
        }

        /// <summary>Applies the current rule to every group, always.</summary>
        private void ApplyRule()
        {
            if (_groups.Count == 0) return;
            bool preferFolder = _chkKeepPreferred.Enabled && _chkKeepPreferred.Checked;

            // The ticks can change at any time after the scan, so which files
            // count as preferred is worked out again here.
            foreach (DupGroup g in _groups)
                foreach (FileEntry f in g.Files)
                    f.InPreferred = preferFolder && IsInPreferredFolder(f);

            foreach (DupGroup g in _groups) KeepSelector.AutoSelect(g, CurrentRule, preferFolder);
            foreach (ListViewItem it in _lv.Items)
            {
                var g = it.Tag as DupGroup;
                if (g != null) RefreshRow(it, g);
            }
            if (_sortColumn == GroupComparer.ColumnKeptFile || _sortColumn == GroupComparer.ColumnKeptIn) _lv.Sort();
            ShowGroup(_current);
        }

        // ---------------------- Removing duplicates -------------------
        //
        //  1. RemoveDuplicates (UI thread) validates the destination, fixes the
        //     list of files (DuplicateRemover.Plan), checks the Recycle Bin,
        //     asks for confirmation and opens the journal.
        //  2. RemovalWorker_DoWork (background) checks every file once more
        //     against its kept copy and, in folder mode, moves it.
        //  3. RemovalWorker_RunWorkerCompleted (UI thread) sends the checked
        //     files to the Recycle Bin (bin mode), then updates the lists and
        //     reports what happened to every file.

        /// <summary>MOVE ALL / TRASH ALL: removes the duplicates of every group.</summary>
        private void RemoveDuplicates()
        {
            if (IsBusy) return;
            if (_groups.Count == 0)
            {
                Native.Show(this, "TwinPix", "Nothing to move",
                            "Scan a folder first: the list holds no duplicate group.",
                            MessageBoxButtons.OK, Native.DialogIcon.Information);
                return;
            }

            var settings = new RemovalSettings();
            settings.ToRecycleBin = _chkTrash.Checked;
            settings.KeepFolderStructure = _chkPreserveTree.Checked;
            settings.ScanRoot = _scannedRoot;
            if (!settings.ToRecycleBin)
            {
                string dest = CheckDestination();
                if (dest == null) return;
                settings.Destination = dest;
            }

            List<RemovalItem> items = DuplicateRemover.Plan(_groups);
            if (items.Count == 0)
            {
                Native.Show(this, "TwinPix", "Nothing to move",
                            "No image is marked as the one to keep, so every copy would be lost.",
                            MessageBoxButtons.OK, Native.DialogIcon.Information);
                return;
            }

            if (settings.ToRecycleBin)
            {
                string why = RecycleBin.WhyNotAvailable(items.Select(i => i.Duplicate.FullPath).ToList());
                if (why != null)
                {
                    Native.Show(this, "TwinPix", "The Recycle Bin cannot be used here",
                                why + "\r\n\r\nNothing was moved. Untick \"Move to trash\" to move the "
                                + "duplicates to a folder of your choice instead.",
                                MessageBoxButtons.OK, Native.DialogIcon.Warning);
                    return;
                }
            }

            string journalPath = settings.ToRecycleBin
                               ? RemovalJournal.RecycleBinJournalPath
                               : Path.Combine(settings.Destination, RemovalJournal.FileName);
            if (!Native.ConfirmRisky(this, "TwinPix", ConfirmTitle(settings, items.Count),
                                     ConfirmText(settings, journalPath)))
                return;

            try { _journal = RemovalJournal.Open(journalPath); }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
                Native.Show(this, "TwinPix", "The journal cannot be written",
                            ex.Message + "\r\n\r\nNothing was moved: every move is recorded in\r\n" + journalPath,
                            MessageBoxButtons.OK, Native.DialogIcon.Error);
                return;
            }

            _removalSettings = settings;
            _removalItems = items;
            _remover = new DuplicateRemover(settings, _journal);
            SetRemoving(true);

            _removalWorker = new BackgroundWorker();
            _removalWorker.WorkerReportsProgress = true;
            _removalWorker.WorkerSupportsCancellation = true;
            _removalWorker.DoWork += RemovalWorker_DoWork;
            _removalWorker.ProgressChanged += Worker_ProgressChanged;
            _removalWorker.RunWorkerCompleted += RemovalWorker_RunWorkerCompleted;
            _removalWorker.RunWorkerAsync();
        }

        /// <summary>
        /// The destination folder, created if needed, as a full path - or null
        /// after telling the user why it cannot be used.
        /// </summary>
        private string CheckDestination()
        {
            string dest = _cboQuarantine.Text.Trim();
            string problem = null;
            if (dest.Length == 0)
                problem = "Fill in the destination folder at the bottom of the window, or tick Move to trash.";
            else if (!PathHelper.IsAbsolute(dest))
                problem = "Enter a full path, such as D:\\Duplicates - a partial path would depend on "
                        + "the program's current folder.";
            else if (_scannedRoot.Length > 0 && PathHelper.IsSameOrUnder(_scannedRoot, dest))
                problem = "The destination is the scanned folder, or contains it. Choose a separate folder, "
                        + "so that the duplicates set aside never mix with the pictures they duplicate.";
            if (problem != null)
            {
                Native.Show(this, "TwinPix", "Choose where the duplicates should go", problem,
                            MessageBoxButtons.OK, Native.DialogIcon.Warning);
                return null;
            }

            try { Directory.CreateDirectory(dest); }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
                Native.Show(this, "TwinPix", "The destination folder cannot be used", ex.Message,
                            MessageBoxButtons.OK, Native.DialogIcon.Error);
                return null;
            }
            RememberFolder(_cboQuarantine, KeyDestination);
            return PathHelper.NormalizeFolder(dest);
        }

        private static string ConfirmTitle(RemovalSettings settings, int count)
        {
            return settings.ToRecycleBin
                 ? "Send " + count + " duplicate file(s) to the Recycle Bin?"
                 : "Move " + count + " duplicate file(s)?";
        }

        private string ConfirmText(RemovalSettings settings, string journalPath)
        {
            var sb = new StringBuilder();
            if (settings.ToRecycleBin)
                sb.Append("They go to the Windows Recycle Bin, and can be restored from there.");
            else
                sb.Append("They are moved to:\r\n").Append(settings.Destination).Append("\r\nNothing is deleted.");
            sb.Append("\r\n\r\nThe copy marked in each group stays where it is. Just before each file "
                      + "moves, it is checked once more against that copy; a file that no longer "
                      + "matches it stays where it is.");
            if (_groups.Any(g => g.Visual))
                sb.Append("\r\n\r\nThese groups were matched by appearance, not by content: two different "
                          + "photos of the same scene can end up together. If in doubt, look through "
                          + "the groups first.");
            sb.Append("\r\n\r\nEvery file moved is listed in:\r\n").Append(journalPath);
            return sb.ToString();
        }

        /// <summary>
        /// Locks the window while files are being moved - nothing may change the
        /// groups meanwhile - except for the MOVE ALL button, which becomes STOP.
        /// </summary>
        private void SetRemoving(bool busy)
        {
            _menu.Enabled = !busy;
            _sourceBox.Enabled = !busy;
            _centre.Enabled = !busy;
            _chkTrash.Enabled = !busy && Native.IsWindows;
            _progress.Visible = busy;
            Cursor = busy ? Cursors.AppStarting : Cursors.Default;
            if (busy)
            {
                _lblQuarantine.Enabled = false;
                _cboQuarantine.Enabled = false;
                _btnQuarantine.Enabled = false;
                _chkPreserveTree.Enabled = false;
                _btnMoveAll.Text = "&STOP";
            }
            else
            {
                TrashModeChanged();                 // puts the destination controls and the caption back
            }
        }

        /// <summary>Background thread: checks every file and, in folder mode, moves it.</summary>
        private void RemovalWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _remover.Run(_removalItems, (BackgroundWorker)sender);
        }

        /// <summary>UI thread: finishes a Recycle Bin removal, then reports.</summary>
        private void RemovalWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bool stopped = _removalWorker.CancellationPending;
            try
            {
                if (e.Error == null && !stopped && _removalSettings.ToRecycleBin)
                    _remover.SendCheckedToRecycleBin(this, _removalItems);
            }
            finally
            {
                // PHP note: "finally" runs whatever happened above, exceptions
                // included - the journal is always closed and the window unlocked.
                _journal.Dispose();
                SetRemoving(false);
            }

            FinishRemoval(_removalItems, _removalSettings, _journal.Path, stopped, e.Error);
            _journal = null;
            _remover = null;
            _removalItems = null;
        }

        /// <summary>Takes the files that left out of their groups, refreshes the lists and reports.</summary>
        private void FinishRemoval(List<RemovalItem> items, RemovalSettings settings, string journalPath,
                                   bool stopped, Exception error)
        {
            int removed = 0, leftInPlace = 0, failed = 0, notReached = 0;
            var reasons = new Dictionary<string, int>();
            var failures = new List<string>();
            foreach (RemovalItem item in items)
            {
                switch (item.Outcome)
                {
                    case RemovalOutcome.Moved:
                    case RemovalOutcome.Recycled:
                        item.Group.Files.Remove(item.Duplicate);
                        removed++;
                        break;
                    case RemovalOutcome.LeftInPlace:
                        leftInPlace++;
                        string reason = item.Detail ?? "not checked";
                        int n;
                        reasons.TryGetValue(reason, out n);
                        reasons[reason] = n + 1;
                        break;
                    case RemovalOutcome.Failed:
                        failed++;
                        failures.Add(item.Duplicate.FullPath + " : " + item.Detail);
                        break;
                    default:
                        notReached++;
                        break;
                }
            }

            // drop the groups that no longer hold duplicates
            var remaining = new List<DupGroup>();
            foreach (DupGroup g in _groups)
                if (g.Files.Count > 1) { g.Recompute(); remaining.Add(g); }
            _groups = remaining;

            FillGroups();
            RefreshFolderCounts();
            if (_lv.Items.Count == 0) { _current = null; ClearCards(); _lblGroupTitle.Text = "No group left"; }
            EnableActions(_groups.Count > 0);

            string where = settings.ToRecycleBin ? "the Recycle Bin" : settings.Destination;
            string verb = settings.ToRecycleBin ? "sent to the Recycle Bin" : "moved";
            _statusLabel.Text = removed + " file(s) " + verb
                              + (leftInPlace + failed > 0 ? " - " + (leftInPlace + failed) + " left in place" : "");

            var sb = new StringBuilder();
            if (removed > 0) sb.Append("They are now in ").Append(where).Append(".\r\n\r\n");
            if (stopped && notReached > 0)
                sb.Append(notReached).Append(" file(s) were not handled: the operation was stopped.\r\n\r\n");
            if (error != null)
                sb.Append("The operation stopped on an error: ").Append(error.Message).Append("\r\n\r\n");
            if (leftInPlace > 0)
            {
                sb.Append(leftInPlace).Append(" file(s) left in place after the last check:\r\n");
                foreach (KeyValuePair<string, int> kv in reasons.OrderByDescending(r => r.Value).Take(ReportLines))
                    sb.Append("  ").Append(kv.Value).Append(" x ").Append(kv.Key).Append("\r\n");
                sb.Append("\r\n");
            }
            if (failed > 0)
            {
                sb.Append(failed).Append(" file(s) could not be moved:\r\n");
                foreach (string line in failures.Take(ReportLines)) sb.Append("  ").Append(line).Append("\r\n");
                if (failures.Count > ReportLines) sb.Append("  ...\r\n");
                sb.Append("\r\n");
            }
            if (removed > 0) sb.Append("Journal: ").Append(journalPath);

            bool clean = leftInPlace == 0 && failed == 0 && error == null && !stopped;
            Native.Show(this, "TwinPix", removed + " file(s) " + verb, sb.ToString().TrimEnd(),
                        MessageBoxButtons.OK, clean ? Native.DialogIcon.Information : Native.DialogIcon.Warning);
        }

        // ---------------------- Export CSV ----------------------------

        /// <summary>Writes the whole list - every file of every group, kept or not - to a CSV file.</summary>
        private void ExportCsv()
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
                    sb.AppendLine("Group;File;Folder;Size (bytes);Dimensions;Modified;Action");
                    int n = 0;
                    foreach (DupGroup g in _groups)
                    {
                        n++;
                        foreach (FileEntry f in g.Files)
                        {
                            sb.AppendLine(string.Join(";", new[]
                            {
                                n.ToString(CultureInfo.InvariantCulture),
                                Csv(f.FileName),
                                Csv(f.DirectoryPath),
                                f.Size.ToString(CultureInfo.InvariantCulture),
                                f.Dimensions,
                                f.Modified.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                                f.Keep ? "KEEP" : "duplicate"
                            }));
                        }
                    }
                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    _statusLabel.Text = "Export written: " + dlg.FileName;
                }
                catch (Exception ex)
                {
                    if (!PathHelper.IsFileSystemError(ex)) throw;
                    Native.Show(this, "TwinPix", "The export failed", ex.Message,
                                MessageBoxButtons.OK, Native.DialogIcon.Error);
                }
            }
        }

        /// <summary>Quotes a CSV field when it holds a separator or a quote.</summary>
        private static string Csv(string s)
        {
            if (s == null) return "";
            if (s.IndexOf(';') >= 0 || s.IndexOf('"') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        /// <summary>Caches the shell icon of an extension and returns its key in the image list.</summary>
        private string EnsureFileIcon(string extension)
        {
            if (_fileIcons == null || string.IsNullOrEmpty(extension)) return null;
            if (_fileIcons.Images.ContainsKey(extension)) return extension;
            Icon icon = Native.FileTypeIcon(extension);
            if (icon == null) return null;
            try
            {
                _fileIcons.Images.Add(extension, icon);     // the image list keeps its own copy
                return extension;
            }
            catch { return null; }              // no icon is only cosmetic
            finally { icon.Dispose(); }
        }

        // ---------------------- Window placement -----------------------

        /// <summary>Restores the size and position saved when the window last closed.</summary>
        private void RestorePlacement()
        {
            string saved = _history.GetValue(KeyWindow);
            if (saved == null) return;
            string[] parts = saved.Split(',');
            if (parts.Length != 5) return;

            int x, y, w, h;
            if (!int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out y)
                || !int.TryParse(parts[2], out w) || !int.TryParse(parts[3], out h)) return;
            if (w < MinimumSize.Width || h < MinimumSize.Height) return;

            var bounds = new Rectangle(x, y, w, h);
            if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds)))
                return;                         // that monitor is gone: keep the default position

            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            if (parts[4] == "1") WindowState = FormWindowState.Maximized;
        }

        private void SavePlacement()
        {
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _history.SetValue(KeyWindow, b.X + "," + b.Y + "," + b.Width + "," + b.Height
                              + "," + (WindowState == FormWindowState.Maximized ? "1" : "0"));
        }

        // ---------------------- Designer event handlers -----------------
        //  Wired in MainForm.Designer.cs. Thin wrappers: the designer needs a
        //  named method per event, and the work itself stays in the methods
        //  above.

        private void BtnRoot_Click(object sender, EventArgs e)
        {
            Browse(_cboRoot, "Folder to scan", KeyScan);
        }

        private void BtnQuarantine_Click(object sender, EventArgs e)
        {
            Browse(_cboQuarantine, "Destination folder for duplicates", KeyDestination);
        }

        private void BtnScan_Click(object sender, EventArgs e) { StartScan(); }

        private void Scan_Click(object sender, EventArgs e) { StartScan(); }

        /// <summary>MOVE ALL - or STOP while files are being moved.</summary>
        private void MoveAll_Click(object sender, EventArgs e)
        {
            if (IsRemoving)
            {
                _removalWorker.CancelAsync();
                _statusLabel.Text = "Stopping after the current file...";
                return;
            }
            RemoveDuplicates();
        }

        private void Export_Click(object sender, EventArgs e) { ExportCsv(); }

        private void Exit_Click(object sender, EventArgs e) { Close(); }

        private void MiAbout_Click(object sender, EventArgs e) { ShowAbout(); }

        private void ClearScanHistory_Click(object sender, EventArgs e)
        {
            ClearHistory(_cboRoot, KeyScan);
        }

        private void ClearDestinationHistory_Click(object sender, EventArgs e)
        {
            ClearHistory(_cboQuarantine, KeyDestination);
        }

        private void ChkTrash_CheckedChanged(object sender, EventArgs e) { TrashModeChanged(); }

        private void ChkRecursive_CheckedChanged(object sender, EventArgs e) { ScanOptionChanged(); }

        private void CboMatch_SelectedIndexChanged(object sender, EventArgs e) { MatchModeChanged(); }

        private void CboSensitivity_SelectedIndexChanged(object sender, EventArgs e) { ScanOptionChanged(); }

        private void LvFolders_ItemChecked(object sender, ItemCheckedEventArgs e) { PreferredFolderToggled(e.Item); }

        private void ChkKeepPreferred_CheckedChanged(object sender, EventArgs e) { RuleChanged(null); }

        private void ChkKeepOldest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepOldest); }

        private void ChkKeepNewest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepNewest); }

        private void ChkKeepShortest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepShortest); }

        private void ChkKeepBest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepBest); }

        private void ChkKeepLargest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepLargest); }

        private void MiKeepPreferred_Click(object sender, EventArgs e)
        {
            if (_chkKeepPreferred.Enabled)
                _chkKeepPreferred.Checked = !_chkKeepPreferred.Checked;
        }

        private void MiKeepOldest_Click(object sender, EventArgs e) { _chkKeepOldest.Checked = true; }

        private void MiKeepNewest_Click(object sender, EventArgs e) { _chkKeepNewest.Checked = true; }

        private void MiKeepShortest_Click(object sender, EventArgs e) { _chkKeepShortest.Checked = true; }

        private void MiKeepBest_Click(object sender, EventArgs e) { _chkKeepBest.Checked = true; }

        private void MiKeepLargest_Click(object sender, EventArgs e) { _chkKeepLargest.Checked = true; }

        /// <summary>A click on a header sorts by that column, a second click reverses it.</summary>
        private void Lv_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sortColumn)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = e.Column;
                // text columns read best A to Z, numbers largest first
                _sortAscending = e.Column == GroupComparer.ColumnKeptFile
                              || e.Column == GroupComparer.ColumnExtension
                              || e.Column == GroupComparer.ColumnKeptIn;
            }
            ApplySort();
        }

        private void Lv_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_lv.SelectedItems.Count == 0) { _current = null; ClearCards(); return; }
            _current = _lv.SelectedItems[0].Tag as DupGroup;
            ShowGroup(_current);
        }
    }
}
