// =====================================================================
//  TwinPix - main window
//
//  Behaviour only: every control and every pixel of the layout lives in
//  MainForm.Designer.cs, which Visual Studio's form designer reads and
//  rewrites. Nothing here creates a control, so the design surface and
//  the running window can never disagree.
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
    public partial class MainForm : Form
    {
        // Fingerprints survive from one scan to the next, and from one run of
        // the program to the next: a second visual scan decodes almost nothing.
        readonly FingerprintCache _fingerprints = new FingerprintCache();
        bool _cacheLoaded;

        bool _suspendRules;              // guards the check boxes against echoing
        string _appliedPreferred = "";   // preferred folder the current selection used
        bool _hasScanned;                // a scan has already filled the list at least once

        BackgroundWorker _worker;
        List<DupGroup> _groups = new List<DupGroup>();
        DupGroup _current;
        bool _suspend;

        // Remembered folders, one list per field.
        const string KeyScan = "scan";
        const string KeyPreferred = "preferred";
        const string KeyDestination = "destination";
        const string KeyWindow = "window";
        const string AppVersion = "1.0";
        readonly FolderHistory _history = new FolderHistory();

        // Share of the window given to the group list when it first opens.
        const double ListWidthRatio = 0.6;

        // Sort state of the duplicate-group list.
        int _sortColumn = 4;              // Reclaimable
        bool _sortAscending;              // largest first

        public MainForm()
        {
            ToolStripManager.RenderMode = ToolStripManagerRenderMode.System;
            InitializeComponent();

            // What the designer cannot hold: values that depend on the machine,
            // on an embedded resource, or on a property it does not serialize.
            Icon appIcon = Util.AppIcon();
            if (appIcon != null) Icon = appIcon;

            Image scanImage = SmallAppImage();
            if (scanImage != null)
            {
                _tsScan.Image = scanImage;
                _tsScan.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            }

            _chkTrash.Enabled = Native.IsWindows;
            _progress.Visible = false;
            _cboMatch.SelectedIndex = 0;
            _cboSensitivity.SelectedIndex = 1;
            _cboSensitivity.Enabled = false;      // visual matching only
            _lblSensitivity.Enabled = false;

            SyncRuleMenu();
            TrashModeChanged();
            EnableActions(false);

            _history.Load();
            FillCombo(_cboRoot, KeyScan);
            FillCombo(_cboPreferred, KeyPreferred);
            FillCombo(_cboQuarantine, KeyDestination);
            RestorePlacement();
        }

        // ---------------------- Window plumbing -----------------------

        /// <summary>
        /// Gives each group box the height its content ended up needing. An
        /// auto-sizing GroupBox wrapped around a docked auto-sizing grid can
        /// send the .NET Framework layout engine into a loop, so the size is
        /// taken once, after the first layout pass.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FitGroupBox(_sourceBox, _sourceGrid);
            FitGroupBox(_destBox, _destGrid);

            // the option check boxes wrap when the window narrows, which changes
            // the height the band needs
            _sourceGrid.SizeChanged += delegate { FitGroupBox(_sourceBox, _sourceGrid); };
            _destGrid.SizeChanged += delegate { FitGroupBox(_destBox, _destGrid); };

            // the cards follow the width of the right-hand panel
            _cards.SizeChanged += delegate { LayoutCards(); };
        }

        // The cards always go two to a row, each half the panel's width.
        const int CardsPerRow = 2;

        /// <summary>
        /// Sizes every card to half the panel's width. Room for the vertical
        /// scroll bar is always kept, so the bar showing up or going away never
        /// changes the widths.
        /// </summary>
        void LayoutCards()
        {
            const int perRow = CardsPerRow;
            int usable = _cards.Width - _cards.Padding.Horizontal
                       - SystemInformation.VerticalScrollBarWidth;
            if (usable <= 0 || _cards.Controls.Count == 0) return;

            _cards.SuspendLayout();
            foreach (Control c in _cards.Controls)
            {
                var card = c as FileCard;
                if (card == null) continue;
                card.SetCardWidth(usable / perRow - card.Margin.Horizontal);
            }
            _cards.ResumeLayout(true);
        }

        static void FitGroupBox(GroupBox box, Control content)
        {
            if (box == null || content == null) return;
            int inner = Math.Max(content.Height, content.PreferredSize.Height);
            int needed = inner + box.Padding.Vertical + 22;            // 22: the caption band
            if (needed > 0 && needed != box.Height) box.Height = needed;
        }

        /// <summary>The group list gets three quarters of the window by default.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // These need a live window handle, so they happen here.
            Native.UseExplorerTheme(_lv);
            Native.EnableDoubleBuffer(_lv);
            Native.SetCueBanner(_cboRoot, "Folder to search for duplicates");
            Native.SetCueBanner(_cboPreferred, "Optional - copies found here are kept");
            Native.SetCueBanner(_cboQuarantine, "Where the duplicates are moved");

            ApplySplitLayout();
        }

        /// <summary>
        /// Gives the group list three quarters of the width. The minimums are
        /// cleared first, then the position, then the minimums again: each of
        /// the three properties is validated against the other two, and any
        /// other order can be rejected on a narrow window.
        /// </summary>
        void ApplySplitLayout()
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
            catch
            {
                // a layout detail is never worth losing the window over
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_prefTimer != null) _prefTimer.Stop();
            if (_worker != null && _worker.IsBusy) _worker.CancelAsync();
            if (_cacheLoaded) _fingerprints.Save();
            SavePlacement();
            _history.Add(KeyScan, _cboRoot.Text);
            _history.Add(KeyPreferred, _cboPreferred.Text);
            _history.Add(KeyDestination, _cboQuarantine.Text);
            _history.Save();
            base.OnFormClosing(e);
        }

        /// <summary>16 px frame of the application icon, for the toolbar.</summary>
        static Image SmallAppImage()
        {
            try
            {
                Icon big = Util.AppIcon();
                if (big == null) return null;
                using (var small = new Icon(big, new Size(16, 16)))
                    return small.ToBitmap();
            }
            catch { return null; }
        }

        void ShowAbout()
        {
            Native.Show(this, "About TwinPix", "TwinPix " + AppVersion,
                "Finds duplicate images - by size and extension, by content, or by "
                + "what the picture actually looks like - then moves the copies you "
                + "do not keep to a folder of your choice or to the Recycle Bin.\r\n\r\n"
                + "Built with the C# compiler shipped with Windows.",
                MessageBoxButtons.OK, Native.DialogIcon.Information);
        }

        /// <summary>
        /// The Recycle Bin needs no destination: the folder field, its Browse
        /// button and the folder-structure box are greyed out while it is ticked.
        /// </summary>
        void TrashModeChanged()
        {
            if (_chkTrash == null) return;
            bool toFolder = !_chkTrash.Checked;
            _lblQuarantine.Enabled = toFolder;
            _cboQuarantine.Enabled = toFolder;
            _btnQuarantine.Enabled = toFolder;
            _chkPreserveTree.Enabled = toFolder;
            _tsMoveAll.Text = toFolder ? "Move all" : "Trash all";
            _miMoveAll.Text = toFolder ? "Move &all duplicates" : "Send &all duplicates to the trash";
        }

        void EnableActions(bool on)
        {
            _miMoveAll.Enabled = on;
            _miExport.Enabled = on;
            _tsMoveAll.Enabled = on;
            _tsExport.Enabled = on;
            // the keep rules stay available: they are settings, not actions
        }

        // ---------------------- Folder fields -------------------------

        /// <summary>Loads a field's remembered folders, keeping what is typed in it.</summary>
        void FillCombo(ComboBox c, string key)
        {
            string current = c.Text;
            c.Items.Clear();
            List<string> list = _history.Get(key);
            for (int i = 0; i < list.Count; i++) c.Items.Add(list[i]);
            c.Text = current.Length > 0 ? current : (list.Count > 0 ? list[0] : "");
        }

        /// <summary>Moves the field's current folder to the top of its history.</summary>
        void RememberFolder(ComboBox c, string key)
        {
            _history.Add(key, c.Text);
            _history.Save();
            FillCombo(c, key);
        }

        /// <summary>Empties one field's drop-down list, on disk as well.</summary>
        void ClearHistory(ComboBox c, string key)
        {
            _history.Clear(key);
            _history.Save();
            FillCombo(c, key);
        }

        void Browse(ComboBox target, string description, string historyKey)
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
        void StartScan()
        {
            if (_worker != null && _worker.IsBusy)
            {
                _worker.CancelAsync();
                _statusLabel.Text = "Cancelling...";
                return;
            }

            string root = _cboRoot.Text.Trim();
            if (!Directory.Exists(root))
            {
                Native.Show(this, "TwinPix", "Choose a folder to scan",
                            "The path is empty or no longer exists on this computer.",
                            MessageBoxButtons.OK, Native.DialogIcon.Warning);
                return;
            }

            string pref = _cboPreferred.Text.Trim();
            if (pref.Length > 0 && !Directory.Exists(pref))
            {
                Native.Show(this, "TwinPix", "The preferred folder does not exist",
                            "Correct the path, or clear the field to scan without a preferred folder.",
                            MessageBoxButtons.OK, Native.DialogIcon.Warning);
                return;
            }

            var o = new ScanOptions();
            o.Root = root;
            o.Preferred = pref;
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
                Native.Show(this, "TwinPix", "Enter at least one extension",
                            "The scan needs to know which files count as images, "
                            + "for example .jpg;.png",
                            MessageBoxButtons.OK, Native.DialogIcon.Warning);
                return;
            }

            RememberFolder(_cboRoot, KeyScan);
            if (pref.Length > 0) RememberFolder(_cboPreferred, KeyPreferred);

            ClearResults();
            _btnScan.Text = "CANCEL";
            _progress.Visible = true;
            _statusLabel.Text = "Scanning...";
            Cursor = Cursors.AppStarting;

            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += delegate(object s, DoWorkEventArgs e)
            {
                var self = (BackgroundWorker)s;
                e.Result = Scanner.Scan(o, self);
                // Scan() returns what it has when it is stopped; saying so here
                // is what makes RunWorkerCompleted report a cancellation rather
                // than an empty result.
                if (self.CancellationPending) e.Cancel = true;
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

                var res = (ScanResult)e.Result;
                _hasScanned = true;
                _groups = res.Groups;
                FillGroups();
                ApplyRule();          // honour whatever is ticked in the Keep bar

                string report = res.FilesScanned + " image(s) scanned - "
                              + _groups.Count + " duplicate group(s)";
                if (o.Mode == MatchMode.Visual)
                {
                    report += " - " + res.Fingerprinted + " fingerprinted";
                    if (res.FromCache > 0) report += ", " + res.FromCache + " from cache";
                    if (res.Skipped > 0) report += " - " + res.Skipped + " too small or too plain";
                    _fingerprints.Save();
                }
                if (res.Errors > 0) report += " - " + res.Errors + " unreadable item(s)";
                _statusLabel.Text = report;

                EnableActions(_groups.Count > 0);
                if (_groups.Count == 0)
                    Native.Show(this, "TwinPix", "No duplicates found",
                                o.Mode == MatchMode.Visual
                                ? "No two images in this folder look like the same picture.\r\n\r\n"
                                  + "A looser sensitivity finds copies that were cropped or retouched."
                                : "No two images in this folder have exactly the same bytes.",
                                MessageBoxButtons.OK, Native.DialogIcon.Information);
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
            _paneGroups.Text = "";
            _paneDuplicates.Text = "";
            _paneReclaimable.Text = "";
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
            _lv.ListViewItemSorter = null;      // one sort pass at the end, not one per row
            _lv.Items.Clear();
            foreach (var g in _groups)
            {
                var it = new ListViewItem(g.DisplayName);
                it.ImageKey = EnsureFileIcon(g.Extension);
                it.SubItems.Add(g.Extension);
                it.SubItems.Add(Util.FormatSize(g.Size));
                it.SubItems.Add(g.Files.Count.ToString(CultureInfo.InvariantCulture));
                it.SubItems.Add(Util.FormatSize(g.Wasted));
                it.SubItems.Add(KeptText(g));
                it.Tag = g;
                _lv.Items.Add(it);
            }
            _lv.EndUpdate();
            ApplySort();
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
            _paneGroups.Text = _groups.Count + " group(s)";
            _paneDuplicates.Text = dups + " duplicate(s)";
            _paneReclaimable.Text = Util.FormatSize(wasted) + " reclaimable";
        }

        void ApplySort()
        {
            _lv.ListViewItemSorter = new GroupComparer(_sortColumn, _sortAscending);
            _lv.Sort();
            Native.SetSortArrow(_lv, _sortColumn, _sortAscending);
        }

        void ShowGroup(DupGroup g)
        {
            ClearCards();
            if (g == null)
            {
                _lblGroupTitle.Text = "No group selected";
                return;
            }
            // the panel is narrow by default, so the title stays short
            string sizes = g.SizesDiffer
                         ? Util.FormatSize(g.SizeMin) + " to " + Util.FormatSize(g.Size)
                         : Util.FormatSize(g.Size);
            _lblGroupTitle.Text = sizes + " " + g.Extension
                                + "  -  " + g.Files.Count + " files";
            _tip.SetToolTip(_lblGroupTitle,
                g.Visual
                ? g.Files.Count + " files showing the same picture, " + sizes
                  + "\r\nThe copies may differ in size, format and quality."
                  + "\r\nClick an image to mark it as the one to keep."
                : g.Files.Count + " files of exactly " + Util.FormatSize(g.Size)
                  + " with the " + g.Extension + " extension"
                  + "\r\nClick an image to mark it as the one to keep.");
            _cards.SuspendLayout();
            _suspend = true;
            foreach (var f in g.Files)
            {
                var card = new FileCard();
                card.Bind(f, g.Visual, _tip);
                card.KeepChanged += CardKeepChanged;
                _cards.Controls.Add(card);
            }
            LayoutCards();
            _suspend = false;
            _cards.ResumeLayout();
        }

        /// <summary>One card has been picked: every other one in the group lets go.</summary>
        void CardKeepChanged(object sender, EventArgs e)
        {
            if (_suspend) return;
            var chosen = sender as FileCard;
            if (chosen == null || !chosen.KeepChecked) return;

            _suspend = true;
            foreach (Control c in _cards.Controls)
            {
                var card = c as FileCard;
                if (card == null) continue;
                bool keep = ReferenceEquals(card, chosen);
                card.KeepChecked = keep;
                if (card.Entry != null) card.Entry.Keep = keep;
                card.UpdateStyle();
            }
            _suspend = false;
            RefreshCurrentRow();
        }

        /// <summary>Both the kept file and its folder change with the selection.</summary>
        static void RefreshRow(ListViewItem item, DupGroup g)
        {
            item.Text = g.DisplayName;
            item.SubItems[5].Text = KeptText(g);
        }

        void RefreshCurrentRow()
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
        CheckBox[] RuleChecks
        {
            get
            {
                return new CheckBox[] { _chkKeepOldest, _chkKeepNewest, _chkKeepShortest,
                                        _chkKeepBest, _chkKeepLargest };
            }
        }

        Scanner.KeepRule CurrentRule
        {
            get
            {
                if (_chkKeepOldest.Checked) return Scanner.KeepRule.Oldest;
                if (_chkKeepNewest.Checked) return Scanner.KeepRule.Newest;
                if (_chkKeepBest.Checked) return Scanner.KeepRule.BestResolution;
                if (_chkKeepLargest.Checked) return Scanner.KeepRule.LargestFile;
                return Scanner.KeepRule.ShortestPath;
            }
        }

        /// <summary>
        /// Keeps the rule boxes mutually exclusive - and never all of them off
        /// - then re-applies the selection to every group.
        /// </summary>
        void RuleChanged(CheckBox source)
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
        void MatchModeChanged()
        {
            bool visual = SelectedMode == MatchMode.Visual;
            _cboSensitivity.Enabled = visual;
            _lblSensitivity.Enabled = visual;
            if (visual && !_chkKeepBest.Checked) _chkKeepBest.Checked = true;
            ScanOptionChanged();
        }

        MatchMode SelectedMode
        {
            get
            {
                if (_cboMatch == null) return MatchMode.Content;
                switch (_cboMatch.SelectedIndex)
                {
                    case 1: return MatchMode.Visual;
                    default: return MatchMode.Content;
                }
            }
        }

        /// <summary>Bits out of 64 that two copies may differ by.</summary>
        int SelectedDistance
        {
            get
            {
                if (_cboSensitivity == null) return 6;
                switch (_cboSensitivity.SelectedIndex)
                {
                    case 0: return 3;       // a re-saved or resized copy
                    case 2: return 10;      // cropped edges, a light retouch
                    default: return 6;
                }
            }
        }

        /// <summary>
        /// The Preferred folder box follows the field: ticked while a folder is
        /// given, greyed out and clear when the field is empty. Every change to
        /// the field also redoes the selection over the whole list, a short
        /// pause later so a path typed by hand does not re-sort every keystroke.
        /// </summary>
        void PreferredFolderChanged()
        {
            if (_chkKeepPreferred == null) return;
            bool given = _cboPreferred.Text.Trim().Length > 0;
            if (_chkKeepPreferred.Enabled != given)
            {
                _suspendRules = true;
                _chkKeepPreferred.Enabled = given;
                _chkKeepPreferred.Checked = given;
                _suspendRules = false;
                SyncRuleMenu();
            }

            // Any change to the field changes which copy is kept, so the list is
            // redone - after a short pause, so a path typed by hand is not
            // re-sorted letter by letter. A folder picked or pasted lands at once.
            if (_prefTimer != null) { _prefTimer.Stop(); _prefTimer.Start(); }
        }

        /// <summary>
        /// "Include subfolders", "Matching" and "Sensitivity" change what a scan
        /// finds, so the folder is searched again the moment one of them changes.
        /// Before the first scan there is nothing to refresh, and while a scan is
        /// running the new setting simply applies to the next one.
        /// </summary>
        void ScanOptionChanged()
        {
            if (!_hasScanned) return;
            if (_worker != null && _worker.IsBusy) return;
            StartScan();
        }

        /// <summary>Re-applies the selection when the folder actually changed.</summary>
        void PreferredFolderCommitted()
        {
            if (_chkKeepPreferred == null) return;
            if (_cboPreferred.Text.Trim() == _appliedPreferred) return;
            ApplyRule();
        }

        void SyncRuleMenu()
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
        void ApplyRule()
        {
            if (_groups.Count == 0) return;
            bool preferFolder = _chkKeepPreferred.Enabled && _chkKeepPreferred.Checked;

            // The field can be edited after the scan, so which files count as
            // preferred is worked out again here rather than trusted from then.
            string preferred = _cboPreferred.Text.Trim();
            _appliedPreferred = preferred;
            foreach (var g in _groups)
                foreach (var f in g.Files)
                    f.InPreferred = preferFolder && Scanner.IsUnder(f.FullPath, preferred);

            foreach (var g in _groups) Scanner.AutoSelect(g, CurrentRule, preferFolder);
            foreach (ListViewItem it in _lv.Items)
            {
                var g = it.Tag as DupGroup;
                if (g != null) RefreshRow(it, g);
            }
            if (_sortColumn == 0 || _sortColumn == 5) _lv.Sort();
            ShowGroup(_current);
        }

        // ---------------------- Moving --------------------------------
        void MoveAllDuplicates()
        {
            var targets = new List<DupGroup>();
            targets.AddRange(_groups);

            if (targets.Count == 0)
            {
                Native.Show(this, "TwinPix", "Nothing to move",
                            "Scan a folder first: the list holds no duplicate group.",
                            MessageBoxButtons.OK, Native.DialogIcon.Information);
                return;
            }

            bool toTrash = _chkTrash.Checked;
            string dest = _cboQuarantine.Text.Trim();
            string root = _cboRoot.Text.Trim();

            if (!toTrash)
            {
                if (dest.Length == 0)
                {
                    Native.Show(this, "TwinPix", "Choose where the duplicates should go",
                                "Fill in the destination folder at the bottom of the window, "
                                + "or tick Move to trash.",
                                MessageBoxButtons.OK, Native.DialogIcon.Warning);
                    return;
                }
                try
                {
                    if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
                }
                catch (Exception ex)
                {
                    Native.Show(this, "TwinPix", "The destination folder cannot be used", ex.Message,
                                MessageBoxButtons.OK, Native.DialogIcon.Error);
                    return;
                }

                RememberFolder(_cboQuarantine, KeyDestination);

                if (Scanner.IsUnder(dest, root))
                {
                    DialogResult r = Native.Show(this, "TwinPix",
                        "The destination is inside the folder being scanned",
                        "Duplicates moved there will be found again by the next scan.\r\n\r\n"
                        + "Continue anyway?",
                        MessageBoxButtons.YesNo, Native.DialogIcon.Warning);
                    if (r != DialogResult.Yes) return;
                }
            }

            int toMove = 0, noKeep = 0;
            foreach (var g in targets)
            {
                if (g.Kept == null) { noKeep++; continue; }
                toMove += g.Files.Count - 1;
            }
            if (toMove == 0)
            {
                Native.Show(this, "TwinPix", "Nothing to move",
                            "No image is marked as the one to keep, so every copy would be lost.",
                            MessageBoxButtons.OK, Native.DialogIcon.Information);
                return;
            }

            string detail = toTrash
                ? "They go to the Windows Recycle Bin, and can be put back from there.\r\n\r\n"
                  + "The copy marked in each group stays where it is."
                : "They are moved to:\r\n" + dest
                  + "\r\n\r\nThe copy marked in each group stays where it is. "
                  + "Nothing is deleted.";
            if (noKeep > 0)
                detail += "\r\n\r\n" + noKeep
                        + " group(s) will be skipped: no file is marked as the one to keep.";
            if (Native.Show(this, "TwinPix",
                            (toTrash ? "Send " : "Move ") + toMove + " duplicate file(s)"
                            + (toTrash ? " to the Recycle Bin?" : "?"), detail,
                            MessageBoxButtons.OKCancel, Native.DialogIcon.Warning) != DialogResult.OK)
                return;

            int moved = 0;
            var errors = new List<string>();
            Cursor = Cursors.WaitCursor;

            if (toTrash)
            {
                MoveToRecycleBin(targets, ref moved, errors);
                Cursor = Cursors.Default;
                FinishMove(moved, errors, "the Recycle Bin");
                return;
            }

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

            Cursor = Cursors.Default;
            FinishMove(moved, errors, dest);
        }

        /// <summary>
        /// Hands every duplicate of the given groups to the shell in one call, so
        /// the whole batch is a single entry in Explorer's Undo. The shell reports
        /// one code for the lot, so what actually left is checked file by file.
        /// </summary>
        void MoveToRecycleBin(List<DupGroup> targets, ref int moved, List<string> errors)
        {
            var owners = new List<DupGroup>();
            var victims = new List<FileEntry>();
            var paths = new List<string>();
            foreach (var g in targets)
            {
                var keep = g.Kept;
                if (keep == null) continue;
                foreach (var f in g.Files)
                {
                    if (ReferenceEquals(f, keep)) continue;
                    owners.Add(g);
                    victims.Add(f);
                    paths.Add(f.FullPath);
                }
            }
            if (paths.Count == 0) return;

            string failure = Native.SendToRecycleBin(this, paths);

            for (int i = 0; i < victims.Count; i++)
            {
                if (File.Exists(victims[i].FullPath))
                {
                    errors.Add(victims[i].FullPath
                               + (failure == null ? " : still on disk" : " : " + failure));
                    continue;
                }
                owners[i].Files.Remove(victims[i]);
                moved++;
            }
        }

        /// <summary>Rebuilds the list after a move and reports what happened.</summary>
        void FinishMove(int moved, List<string> errors, string where)
        {
            // drop groups that no longer hold duplicates
            var remaining = new List<DupGroup>();
            foreach (var g in _groups)
                if (g.Files.Count > 1) { g.Recompute(); remaining.Add(g); }
            _groups = remaining;

            FillGroups();
            if (_lv.Items.Count == 0) { _current = null; ClearCards(); _lblGroupTitle.Text = "No group left"; }
            EnableActions(_groups.Count > 0);

            string report = "They are now in " + where;
            if (errors.Count > 0)
            {
                report = errors.Count + " file(s) could not be moved:\r\n"
                       + string.Join("\r\n", errors.Take(10).ToArray());
                if (errors.Count > 10) report += "\r\n...";
            }
            _statusLabel.Text = moved + " file(s) moved to " + where;
            Native.Show(this, "TwinPix", moved + " file(s) moved", report, MessageBoxButtons.OK,
                        errors.Count > 0 ? Native.DialogIcon.Warning : Native.DialogIcon.Information);
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
                    sb.AppendLine("Group;File;Folder;Size (bytes);Dimensions;Modified;Action");
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
                                f.Dimensions,
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
                    Native.Show(this, "TwinPix", "The export failed", ex.Message,
                                MessageBoxButtons.OK, Native.DialogIcon.Error);
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

        /// <summary>Caches the shell icon of an extension and returns its key.</summary>
        string EnsureFileIcon(string extension)
        {
            if (_fileIcons == null || string.IsNullOrEmpty(extension)) return null;
            if (_fileIcons.Images.ContainsKey(extension)) return extension;
            Icon icon = Native.FileTypeIcon(extension);
            if (icon == null) return null;
            try { _fileIcons.Images.Add(extension, icon); }
            catch { return null; }
            finally { icon.Dispose(); }
            return extension;
        }

        // ---------------------- Window placement -----------------------

        /// <summary>Restores the size and position saved when the window last closed.</summary>
        void RestorePlacement()
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
            bool onScreen = false;
            foreach (Screen screen in Screen.AllScreens)
                if (screen.WorkingArea.IntersectsWith(bounds)) onScreen = true;
            if (!onScreen) return;           // that monitor is gone: keep the default position

            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            if (parts[4] == "1") WindowState = FormWindowState.Maximized;
        }

        void SavePlacement()
        {
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _history.SetValue(KeyWindow, b.X + "," + b.Y + "," + b.Width + "," + b.Height
                              + "," + (WindowState == FormWindowState.Maximized ? "1" : "0"));
        }

        // ---------------------- Designer event handlers -----------------
        //  Thin wrappers: the designer needs a named method per event, and
        //  the work itself stays in the methods above.

        void BtnRoot_Click(object sender, EventArgs e)
        {
            Browse(_cboRoot, "Folder to scan", KeyScan);
        }

        void BtnPreferred_Click(object sender, EventArgs e)
        {
            Browse(_cboPreferred, "Preferred folder", KeyPreferred);
        }

        void BtnClearPreferred_Click(object sender, EventArgs e)
        {
            _cboPreferred.Text = "";
        }

        void BtnQuarantine_Click(object sender, EventArgs e)
        {
            Browse(_cboQuarantine, "Destination folder for duplicates", KeyDestination);
        }

        void BtnScan_Click(object sender, EventArgs e) { StartScan(); }

        void Scan_Click(object sender, EventArgs e) { StartScan(); }

        void MoveAll_Click(object sender, EventArgs e) { MoveAllDuplicates(); }

        void Export_Click(object sender, EventArgs e) { ExportCsv(); }

        void Exit_Click(object sender, EventArgs e) { Close(); }

        void MiAbout_Click(object sender, EventArgs e) { ShowAbout(); }

        void ClearScanHistory_Click(object sender, EventArgs e)
        {
            ClearHistory(_cboRoot, KeyScan);
        }

        void ClearPreferredHistory_Click(object sender, EventArgs e)
        {
            ClearHistory(_cboPreferred, KeyPreferred);
        }

        void ClearDestinationHistory_Click(object sender, EventArgs e)
        {
            ClearHistory(_cboQuarantine, KeyDestination);
        }

        void ChkTrash_CheckedChanged(object sender, EventArgs e) { TrashModeChanged(); }

        void ChkRecursive_CheckedChanged(object sender, EventArgs e) { ScanOptionChanged(); }

        void CboMatch_SelectedIndexChanged(object sender, EventArgs e) { MatchModeChanged(); }

        void CboSensitivity_SelectedIndexChanged(object sender, EventArgs e) { ScanOptionChanged(); }

        void CboPreferred_TextChanged(object sender, EventArgs e) { PreferredFolderChanged(); }

        void CboPreferred_Leave(object sender, EventArgs e)
        {
            if (_prefTimer != null) _prefTimer.Stop();
            PreferredFolderCommitted();
        }

        void CboPreferred_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_prefTimer != null) _prefTimer.Stop();
            PreferredFolderCommitted();
        }

        void PrefTimer_Tick(object sender, EventArgs e)
        {
            _prefTimer.Stop();
            PreferredFolderCommitted();
        }

        void ChkKeepPreferred_CheckedChanged(object sender, EventArgs e) { RuleChanged(null); }

        void ChkKeepOldest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepOldest); }

        void ChkKeepNewest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepNewest); }

        void ChkKeepShortest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepShortest); }

        void ChkKeepBest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepBest); }

        void ChkKeepLargest_CheckedChanged(object sender, EventArgs e) { RuleChanged(_chkKeepLargest); }

        void MiKeepPreferred_Click(object sender, EventArgs e)
        {
            if (_chkKeepPreferred.Enabled)
                _chkKeepPreferred.Checked = !_chkKeepPreferred.Checked;
        }

        void MiKeepOldest_Click(object sender, EventArgs e) { _chkKeepOldest.Checked = true; }

        void MiKeepNewest_Click(object sender, EventArgs e) { _chkKeepNewest.Checked = true; }

        void MiKeepShortest_Click(object sender, EventArgs e) { _chkKeepShortest.Checked = true; }

        void MiKeepBest_Click(object sender, EventArgs e) { _chkKeepBest.Checked = true; }

        void MiKeepLargest_Click(object sender, EventArgs e) { _chkKeepLargest.Checked = true; }

        /// <summary>A click on a header sorts by that column, a second click reverses it.</summary>
        void Lv_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sortColumn)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = e.Column;
                // text columns read best A to Z, numbers largest first
                _sortAscending = (e.Column == 0 || e.Column == 1 || e.Column == 5);
            }
            ApplySort();
        }

        void Lv_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_lv.SelectedItems.Count == 0) { _current = null; ClearCards(); return; }
            _current = _lv.SelectedItems[0].Tag as DupGroup;
            ShowGroup(_current);
        }
    }
}
