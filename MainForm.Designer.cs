namespace TwinPix
{
    partial class MainForm
    {
        /// <summary>Components owned by the form: tool tip, timer, menus, icons.</summary>
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this._tip = new System.Windows.Forms.ToolTip(this.components);
            this._chkKeepPreferred = new System.Windows.Forms.CheckBox();
            this._chkKeepOldest = new System.Windows.Forms.CheckBox();
            this._chkKeepNewest = new System.Windows.Forms.CheckBox();
            this._chkKeepShortest = new System.Windows.Forms.CheckBox();
            this._chkKeepBest = new System.Windows.Forms.CheckBox();
            this._chkKeepLargest = new System.Windows.Forms.CheckBox();
            this._cboQuarantine = new System.Windows.Forms.ComboBox();
            this._menuHistoryDestination = new System.Windows.Forms.ContextMenuStrip(this.components);
            this._miClearDestinationHistory = new System.Windows.Forms.ToolStripMenuItem();
            this._chkTrash = new System.Windows.Forms.CheckBox();
            this._chkPreserveTree = new System.Windows.Forms.CheckBox();
            this._cboRoot = new System.Windows.Forms.ComboBox();
            this._menuHistoryScan = new System.Windows.Forms.ContextMenuStrip(this.components);
            this._miClearScanHistory = new System.Windows.Forms.ToolStripMenuItem();
            this._chkRecursive = new System.Windows.Forms.CheckBox();
            this._cboMatch = new System.Windows.Forms.ComboBox();
            this._cboSensitivity = new System.Windows.Forms.ComboBox();
            this._fileIcons = new System.Windows.Forms.ImageList(this.components);
            this._centre = new System.Windows.Forms.Panel();
            this._split = new System.Windows.Forms.SplitContainer();
            this._splitLists = new System.Windows.Forms.SplitContainer();
            this._lv = new System.Windows.Forms.ListView();
            this._colKept = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colExt = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colCopies = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colWasted = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colKeptIn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._keepBar = new System.Windows.Forms.FlowLayoutPanel();
            this._lblKeep = new System.Windows.Forms.Label();
            this._lblGroups = new System.Windows.Forms.Label();
            this._lvFolders = new System.Windows.Forms.ListView();
            this._colFolder = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colFolderImages = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colFolderDups = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._lblFolders = new System.Windows.Forms.Label();
            this._cards = new System.Windows.Forms.FlowLayoutPanel();
            this._lblGroupTitle = new System.Windows.Forms.Label();
            this._destBox = new System.Windows.Forms.GroupBox();
            this._destGrid = new System.Windows.Forms.TableLayoutPanel();
            this._lblQuarantine = new System.Windows.Forms.Label();
            this._btnQuarantine = new System.Windows.Forms.Button();
            this._btnMoveAll = new System.Windows.Forms.Button();
            this._sourceBox = new System.Windows.Forms.GroupBox();
            this._sourceGrid = new System.Windows.Forms.TableLayoutPanel();
            this._lblRoot = new System.Windows.Forms.Label();
            this._btnRoot = new System.Windows.Forms.Button();
            this._btnScan = new System.Windows.Forms.Button();
            this._opts = new System.Windows.Forms.FlowLayoutPanel();
            this._lblMatch = new System.Windows.Forms.Label();
            this._lblSensitivity = new System.Windows.Forms.Label();
            this._lblExtensions = new System.Windows.Forms.Label();
            this._txtExt = new System.Windows.Forms.TextBox();
            this._menu = new System.Windows.Forms.MenuStrip();
            this._mFile = new System.Windows.Forms.ToolStripMenuItem();
            this._miScan = new System.Windows.Forms.ToolStripMenuItem();
            this._miFileSep1 = new System.Windows.Forms.ToolStripSeparator();
            this._miMoveAll = new System.Windows.Forms.ToolStripMenuItem();
            this._miFileSep2 = new System.Windows.Forms.ToolStripSeparator();
            this._miExport = new System.Windows.Forms.ToolStripMenuItem();
            this._miFileSep3 = new System.Windows.Forms.ToolStripSeparator();
            this._miExit = new System.Windows.Forms.ToolStripMenuItem();
            this._mEdit = new System.Windows.Forms.ToolStripMenuItem();
            this._miKeepPreferred = new System.Windows.Forms.ToolStripMenuItem();
            this._miEditSep1 = new System.Windows.Forms.ToolStripSeparator();
            this._miKeepOldest = new System.Windows.Forms.ToolStripMenuItem();
            this._miKeepNewest = new System.Windows.Forms.ToolStripMenuItem();
            this._miKeepShortest = new System.Windows.Forms.ToolStripMenuItem();
            this._miKeepBest = new System.Windows.Forms.ToolStripMenuItem();
            this._miKeepLargest = new System.Windows.Forms.ToolStripMenuItem();
            this._mHelp = new System.Windows.Forms.ToolStripMenuItem();
            this._miAbout = new System.Windows.Forms.ToolStripMenuItem();
            this._status = new System.Windows.Forms.StatusStrip();
            this._statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this._paneGroups = new System.Windows.Forms.ToolStripStatusLabel();
            this._paneDuplicates = new System.Windows.Forms.ToolStripStatusLabel();
            this._paneReclaimable = new System.Windows.Forms.ToolStripStatusLabel();
            this._progress = new System.Windows.Forms.ToolStripProgressBar();
            this._menuHistoryDestination.SuspendLayout();
            this._menuHistoryScan.SuspendLayout();
            this._centre.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
            this._split.Panel1.SuspendLayout();
            this._split.Panel2.SuspendLayout();
            this._split.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitLists)).BeginInit();
            this._splitLists.Panel1.SuspendLayout();
            this._splitLists.Panel2.SuspendLayout();
            this._splitLists.SuspendLayout();
            this._keepBar.SuspendLayout();
            this._destBox.SuspendLayout();
            this._destGrid.SuspendLayout();
            this._sourceBox.SuspendLayout();
            this._sourceGrid.SuspendLayout();
            this._opts.SuspendLayout();
            this._menu.SuspendLayout();
            this._status.SuspendLayout();
            this.SuspendLayout();
            // 
            // _chkKeepPreferred
            // 
            this._chkKeepPreferred.AutoSize = true;
            this._chkKeepPreferred.Enabled = false;
            this._chkKeepPreferred.Location = new System.Drawing.Point(56, 2);
            this._chkKeepPreferred.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkKeepPreferred.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkKeepPreferred.Name = "_chkKeepPreferred";
            this._chkKeepPreferred.Size = new System.Drawing.Size(131, 32);
            this._chkKeepPreferred.TabIndex = 1;
            this._chkKeepPreferred.Text = "Preferred folders";
            this._tip.SetToolTip(this._chkKeepPreferred, "Keep the copy that sits in a ticked preferred folder, whatever the rule says.\r\nF" +
        "ollows the Preferred folders list below.");
            this._chkKeepPreferred.CheckedChanged += new System.EventHandler(this.ChkKeepPreferred_CheckedChanged);
            // 
            // _chkKeepOldest
            // 
            this._chkKeepOldest.AutoSize = true;
            this._chkKeepOldest.Location = new System.Drawing.Point(195, 2);
            this._chkKeepOldest.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkKeepOldest.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkKeepOldest.Name = "_chkKeepOldest";
            this._chkKeepOldest.Size = new System.Drawing.Size(68, 32);
            this._chkKeepOldest.TabIndex = 2;
            this._chkKeepOldest.Text = "Oldest";
            this._tip.SetToolTip(this._chkKeepOldest, "Of the remaining copies, keep the one with the oldest date.");
            this._chkKeepOldest.CheckedChanged += new System.EventHandler(this.ChkKeepOldest_CheckedChanged);
            // 
            // _chkKeepNewest
            // 
            this._chkKeepNewest.AutoSize = true;
            this._chkKeepNewest.Location = new System.Drawing.Point(279, 2);
            this._chkKeepNewest.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkKeepNewest.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkKeepNewest.Name = "_chkKeepNewest";
            this._chkKeepNewest.Size = new System.Drawing.Size(73, 32);
            this._chkKeepNewest.TabIndex = 3;
            this._chkKeepNewest.Text = "Newest";
            this._tip.SetToolTip(this._chkKeepNewest, "Of the remaining copies, keep the one with the newest date.");
            this._chkKeepNewest.CheckedChanged += new System.EventHandler(this.ChkKeepNewest_CheckedChanged);
            // 
            // _chkKeepShortest
            // 
            this._chkKeepShortest.AutoSize = true;
            this._chkKeepShortest.Checked = true;
            this._chkKeepShortest.CheckState = System.Windows.Forms.CheckState.Checked;
            this._chkKeepShortest.Location = new System.Drawing.Point(368, 2);
            this._chkKeepShortest.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkKeepShortest.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkKeepShortest.Name = "_chkKeepShortest";
            this._chkKeepShortest.Size = new System.Drawing.Size(111, 32);
            this._chkKeepShortest.TabIndex = 4;
            this._chkKeepShortest.Text = "Shortest path";
            this._tip.SetToolTip(this._chkKeepShortest, "Of the remaining copies, keep the one closest to the scanned folder.");
            this._chkKeepShortest.CheckedChanged += new System.EventHandler(this.ChkKeepShortest_CheckedChanged);
            // 
            // _chkKeepBest
            // 
            this._chkKeepBest.AutoSize = true;
            this._chkKeepBest.Location = new System.Drawing.Point(495, 2);
            this._chkKeepBest.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkKeepBest.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkKeepBest.Name = "_chkKeepBest";
            this._chkKeepBest.Size = new System.Drawing.Size(119, 32);
            this._chkKeepBest.TabIndex = 5;
            this._chkKeepBest.Text = "Best resolution";
            this._tip.SetToolTip(this._chkKeepBest, "Of the remaining copies, keep the one with the most pixels,\r\nthen the heaviest. T" +
        "he closest thing to the original.");
            this._chkKeepBest.CheckedChanged += new System.EventHandler(this.ChkKeepBest_CheckedChanged);
            // 
            // _chkKeepLargest
            // 
            this._chkKeepLargest.AutoSize = true;
            this._chkKeepLargest.Location = new System.Drawing.Point(630, 2);
            this._chkKeepLargest.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkKeepLargest.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkKeepLargest.Name = "_chkKeepLargest";
            this._chkKeepLargest.Size = new System.Drawing.Size(94, 32);
            this._chkKeepLargest.TabIndex = 6;
            this._chkKeepLargest.Text = "Largest file";
            this._tip.SetToolTip(this._chkKeepLargest, "Of the remaining copies, keep the heaviest file.");
            this._chkKeepLargest.CheckedChanged += new System.EventHandler(this.ChkKeepLargest_CheckedChanged);
            // 
            // _cboQuarantine
            // 
            this._cboQuarantine.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this._cboQuarantine.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this._cboQuarantine.ContextMenuStrip = this._menuHistoryDestination;
            this._cboQuarantine.Dock = System.Windows.Forms.DockStyle.Fill;
            this._cboQuarantine.Location = new System.Drawing.Point(233, 5);
            this._cboQuarantine.Margin = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._cboQuarantine.MaxDropDownItems = 12;
            this._cboQuarantine.Name = "_cboQuarantine";
            this._cboQuarantine.Size = new System.Drawing.Size(928, 27);
            this._cboQuarantine.TabIndex = 1;
            this._tip.SetToolTip(this._cboQuarantine, "Recently used folders are kept in the drop-down list.");
            // 
            // _menuHistoryDestination
            // 
            this._menuHistoryDestination.ImageScalingSize = new System.Drawing.Size(24, 24);
            this._menuHistoryDestination.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._miClearDestinationHistory});
            this._menuHistoryDestination.Name = "_menuHistoryDestination";
            this._menuHistoryDestination.Size = new System.Drawing.Size(142, 26);
            // 
            // _miClearDestinationHistory
            // 
            this._miClearDestinationHistory.Name = "_miClearDestinationHistory";
            this._miClearDestinationHistory.Size = new System.Drawing.Size(141, 22);
            this._miClearDestinationHistory.Text = "Clear this list";
            this._miClearDestinationHistory.Click += new System.EventHandler(this.ClearDestinationHistory_Click);
            // 
            // _chkTrash
            // 
            this._chkTrash.AutoSize = true;
            this._chkTrash.Location = new System.Drawing.Point(1288, 2);
            this._chkTrash.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkTrash.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkTrash.Name = "_chkTrash";
            this._chkTrash.Size = new System.Drawing.Size(115, 32);
            this._chkTrash.TabIndex = 3;
            this._chkTrash.Text = "Move to &trash";
            this._tip.SetToolTip(this._chkTrash, "Send the duplicates to the Windows Recycle Bin instead of a folder.\r\nThey can be " +
        "restored from there; no destination folder is needed.");
            this._chkTrash.CheckedChanged += new System.EventHandler(this.ChkTrash_CheckedChanged);
            // 
            // _chkPreserveTree
            // 
            this._chkPreserveTree.AutoSize = true;
            this._chkPreserveTree.Checked = true;
            this._chkPreserveTree.CheckState = System.Windows.Forms.CheckState.Checked;
            this._destGrid.SetColumnSpan(this._chkPreserveTree, 2);
            this._chkPreserveTree.Location = new System.Drawing.Point(234, 50);
            this._chkPreserveTree.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkPreserveTree.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkPreserveTree.Name = "_chkPreserveTree";
            this._chkPreserveTree.Size = new System.Drawing.Size(156, 32);
            this._chkPreserveTree.TabIndex = 4;
            this._chkPreserveTree.Text = "&Keep folder structure";
            this._tip.SetToolTip(this._chkPreserveTree, "Recreate the original subfolders inside the destination.");
            // 
            // _cboRoot
            // 
            this._cboRoot.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this._cboRoot.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this._cboRoot.ContextMenuStrip = this._menuHistoryScan;
            this._cboRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this._cboRoot.Location = new System.Drawing.Point(233, 5);
            this._cboRoot.Margin = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._cboRoot.MaxDropDownItems = 12;
            this._cboRoot.Name = "_cboRoot";
            this._cboRoot.Size = new System.Drawing.Size(928, 27);
            this._cboRoot.TabIndex = 1;
            this._tip.SetToolTip(this._cboRoot, "Recently used folders are kept in the drop-down list.");
            // 
            // _menuHistoryScan
            // 
            this._menuHistoryScan.ImageScalingSize = new System.Drawing.Size(24, 24);
            this._menuHistoryScan.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._miClearScanHistory});
            this._menuHistoryScan.Name = "_menuHistoryScan";
            this._menuHistoryScan.Size = new System.Drawing.Size(142, 26);
            // 
            // _miClearScanHistory
            // 
            this._miClearScanHistory.Name = "_miClearScanHistory";
            this._miClearScanHistory.Size = new System.Drawing.Size(141, 22);
            this._miClearScanHistory.Text = "Clear this list";
            this._miClearScanHistory.Click += new System.EventHandler(this.ClearScanHistory_Click);
            // 
            // _chkRecursive
            // 
            this._chkRecursive.AutoSize = true;
            this._chkRecursive.Checked = true;
            this._chkRecursive.CheckState = System.Windows.Forms.CheckState.Checked;
            this._chkRecursive.Location = new System.Drawing.Point(4, 2);
            this._chkRecursive.Margin = new System.Windows.Forms.Padding(4, 2, 12, 2);
            this._chkRecursive.MinimumSize = new System.Drawing.Size(0, 32);
            this._chkRecursive.Name = "_chkRecursive";
            this._chkRecursive.Size = new System.Drawing.Size(139, 32);
            this._chkRecursive.TabIndex = 0;
            this._chkRecursive.Text = "&Include subfolders";
            this._tip.SetToolTip(this._chkRecursive, "Also look inside the subfolders.\r\nChanging this searches the folder again.");
            this._chkRecursive.CheckedChanged += new System.EventHandler(this.ChkRecursive_CheckedChanged);
            // 
            // _cboMatch
            // 
            this._cboMatch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cboMatch.Items.AddRange(new object[] {
            "Same bytes (MD5)",
            "Same picture (visual)"});
            this._cboMatch.Location = new System.Drawing.Point(244, 4);
            this._cboMatch.Margin = new System.Windows.Forms.Padding(3, 2, 6, 2);
            this._cboMatch.Name = "_cboMatch";
            this._cboMatch.Size = new System.Drawing.Size(250, 27);
            this._cboMatch.TabIndex = 2;
            this._tip.SetToolTip(this._cboMatch, resources.GetString("_cboMatch.ToolTip"));
            this._cboMatch.SelectedIndexChanged += new System.EventHandler(this.CboMatch_SelectedIndexChanged);
            // 
            // _cboSensitivity
            // 
            this._cboSensitivity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cboSensitivity.Items.AddRange(new object[] {
            "Strict - re-saved copies",
            "Normal",
            "Loose - lightly edited"});
            this._cboSensitivity.Location = new System.Drawing.Point(592, 4);
            this._cboSensitivity.Margin = new System.Windows.Forms.Padding(3, 2, 6, 2);
            this._cboSensitivity.Name = "_cboSensitivity";
            this._cboSensitivity.Size = new System.Drawing.Size(210, 27);
            this._cboSensitivity.TabIndex = 4;
            this._tip.SetToolTip(this._cboSensitivity, "How far apart two pictures may look and still count as the same.\r\nLoose finds mor" +
        "e, and is likelier to put two different\r\nphotographs of the same scene in one gr" +
        "oup.\r\n\r\nVisual matching only.");
            this._cboSensitivity.SelectedIndexChanged += new System.EventHandler(this.CboSensitivity_SelectedIndexChanged);
            // 
            // _fileIcons
            // 
            this._fileIcons.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            this._fileIcons.ImageSize = new System.Drawing.Size(16, 16);
            this._fileIcons.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // _centre
            // 
            this._centre.Controls.Add(this._split);
            this._centre.Dock = System.Windows.Forms.DockStyle.Fill;
            this._centre.Location = new System.Drawing.Point(0, 194);
            this._centre.Name = "_centre";
            this._centre.Padding = new System.Windows.Forms.Padding(9, 0, 9, 2);
            this._centre.Size = new System.Drawing.Size(1540, 519);
            this._centre.TabIndex = 4;
            // 
            // _split
            // 
            this._split.Dock = System.Windows.Forms.DockStyle.Fill;
            this._split.Location = new System.Drawing.Point(9, 0);
            this._split.Name = "_split";
            // 
            // _split.Panel1
            // 
            this._split.Panel1.Controls.Add(this._splitLists);
            this._split.Panel1.Controls.Add(this._keepBar);
            this._split.Panel1.Controls.Add(this._lblGroups);
            // 
            // _split.Panel2
            // 
            this._split.Panel2.Controls.Add(this._cards);
            this._split.Panel2.Controls.Add(this._lblGroupTitle);
            this._split.Size = new System.Drawing.Size(1522, 482);
            this._split.SplitterDistance = 1134;
            this._split.SplitterWidth = 6;
            this._split.TabIndex = 0;
            // 
            // _splitLists
            // 
            this._splitLists.Dock = System.Windows.Forms.DockStyle.Fill;
            this._splitLists.Location = new System.Drawing.Point(0, 62);
            this._splitLists.Name = "_splitLists";
            this._splitLists.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // _splitLists.Panel1
            // 
            this._splitLists.Panel1.Controls.Add(this._lv);
            // 
            // _splitLists.Panel2
            // 
            this._splitLists.Panel2.Controls.Add(this._lvFolders);
            this._splitLists.Panel2.Controls.Add(this._lblFolders);
            this._splitLists.Size = new System.Drawing.Size(1134, 420);
            this._splitLists.SplitterDistance = 240;
            this._splitLists.SplitterWidth = 6;
            this._splitLists.TabIndex = 2;
            // 
            // _lv
            // 
            this._lv.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colKept,
            this._colExt,
            this._colSize,
            this._colCopies,
            this._colWasted,
            this._colKeptIn});
            this._lv.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lv.FullRowSelect = true;
            this._lv.HideSelection = false;
            this._lv.Location = new System.Drawing.Point(0, 0);
            this._lv.MultiSelect = false;
            this._lv.Name = "_lv";
            this._lv.Size = new System.Drawing.Size(1134, 240);
            this._lv.SmallImageList = this._fileIcons;
            this._lv.TabIndex = 0;
            this._lv.UseCompatibleStateImageBehavior = false;
            this._lv.View = System.Windows.Forms.View.Details;
            this._lv.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.Lv_ColumnClick);
            this._lv.SelectedIndexChanged += new System.EventHandler(this.Lv_SelectedIndexChanged);
            // 
            // _colKept
            // 
            this._colKept.Text = "Kept file";
            this._colKept.Width = 230;
            // 
            // _colExt
            // 
            this._colExt.Text = "Ext";
            this._colExt.Width = 70;
            // 
            // _colSize
            // 
            this._colSize.Text = "Size";
            this._colSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this._colSize.Width = 100;
            // 
            // _colCopies
            // 
            this._colCopies.Text = "Copies";
            this._colCopies.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this._colCopies.Width = 80;
            // 
            // _colWasted
            // 
            this._colWasted.Text = "Reclaimable";
            this._colWasted.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this._colWasted.Width = 120;
            // 
            // _colKeptIn
            // 
            this._colKeptIn.Text = "Kept in";
            this._colKeptIn.Width = 260;
            // 
            // _keepBar
            // 
            this._keepBar.AutoSize = true;
            this._keepBar.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._keepBar.Controls.Add(this._lblKeep);
            this._keepBar.Controls.Add(this._chkKeepPreferred);
            this._keepBar.Controls.Add(this._chkKeepOldest);
            this._keepBar.Controls.Add(this._chkKeepNewest);
            this._keepBar.Controls.Add(this._chkKeepShortest);
            this._keepBar.Controls.Add(this._chkKeepBest);
            this._keepBar.Controls.Add(this._chkKeepLargest);
            this._keepBar.Dock = System.Windows.Forms.DockStyle.Top;
            this._keepBar.Location = new System.Drawing.Point(0, 26);
            this._keepBar.Name = "_keepBar";
            this._keepBar.Size = new System.Drawing.Size(1134, 36);
            this._keepBar.TabIndex = 1;
            // 
            // _lblKeep
            // 
            this._lblKeep.AutoSize = true;
            this._lblKeep.Location = new System.Drawing.Point(2, 2);
            this._lblKeep.Margin = new System.Windows.Forms.Padding(2, 2, 8, 2);
            this._lblKeep.MinimumSize = new System.Drawing.Size(0, 32);
            this._lblKeep.Name = "_lblKeep";
            this._lblKeep.Size = new System.Drawing.Size(42, 32);
            this._lblKeep.TabIndex = 0;
            this._lblKeep.Text = "Keep:";
            this._lblKeep.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _lblGroups
            // 
            this._lblGroups.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblGroups.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblGroups.Location = new System.Drawing.Point(0, 0);
            this._lblGroups.Name = "_lblGroups";
            this._lblGroups.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._lblGroups.Size = new System.Drawing.Size(1134, 26);
            this._lblGroups.TabIndex = 0;
            this._lblGroups.Text = "&Duplicate groups";
            // 
            // _lvFolders
            // 
            this._lvFolders.CheckBoxes = true;
            this._lvFolders.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colFolder,
            this._colFolderImages,
            this._colFolderDups});
            this._lvFolders.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lvFolders.FullRowSelect = true;
            this._lvFolders.HideSelection = false;
            this._lvFolders.Location = new System.Drawing.Point(0, 26);
            this._lvFolders.MultiSelect = false;
            this._lvFolders.Name = "_lvFolders";
            this._lvFolders.Size = new System.Drawing.Size(1134, 148);
            this._lvFolders.TabIndex = 1;
            this._tip.SetToolTip(this._lvFolders, "Tick a folder to keep the copies it holds, whatever the rule says.\r\nOnly the fi" +
        "les directly inside a ticked folder count, not those of its subfolders.");
            this._lvFolders.UseCompatibleStateImageBehavior = false;
            this._lvFolders.View = System.Windows.Forms.View.Details;
            this._lvFolders.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.LvFolders_ItemChecked);
            // 
            // _colFolder
            // 
            this._colFolder.Text = "Folder";
            this._colFolder.Width = 520;
            // 
            // _colFolderImages
            // 
            this._colFolderImages.Text = "Images";
            this._colFolderImages.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this._colFolderImages.Width = 90;
            // 
            // _colFolderDups
            // 
            this._colFolderDups.Text = "In groups";
            this._colFolderDups.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this._colFolderDups.Width = 100;
            // 
            // _lblFolders
            // 
            this._lblFolders.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblFolders.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblFolders.Location = new System.Drawing.Point(0, 0);
            this._lblFolders.Name = "_lblFolders";
            this._lblFolders.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._lblFolders.Size = new System.Drawing.Size(1134, 26);
            this._lblFolders.TabIndex = 0;
            this._lblFolders.Text = "&Preferred folders";
            // 
            // _cards
            // 
            this._cards.AutoScroll = true;
            this._cards.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this._cards.Dock = System.Windows.Forms.DockStyle.Fill;
            this._cards.Location = new System.Drawing.Point(0, 60);
            this._cards.Name = "_cards";
            this._cards.Padding = new System.Windows.Forms.Padding(6);
            this._cards.Size = new System.Drawing.Size(382, 422);
            this._cards.TabIndex = 1;
            // 
            // _lblGroupTitle
            // 
            this._lblGroupTitle.AutoEllipsis = true;
            this._lblGroupTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblGroupTitle.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblGroupTitle.Location = new System.Drawing.Point(0, 0);
            this._lblGroupTitle.Name = "_lblGroupTitle";
            this._lblGroupTitle.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this._lblGroupTitle.Size = new System.Drawing.Size(382, 60);
            this._lblGroupTitle.TabIndex = 0;
            this._lblGroupTitle.Text = "No group selected";
            // 
            // _destBox
            // 
            this._destBox.Controls.Add(this._destGrid);
            this._destBox.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._destBox.Location = new System.Drawing.Point(0, 713);
            this._destBox.Name = "_destBox";
            this._destBox.Padding = new System.Windows.Forms.Padding(8, 2, 8, 6);
            this._destBox.Size = new System.Drawing.Size(1540, 100);
            this._destBox.TabIndex = 5;
            this._destBox.TabStop = false;
            this._destBox.Text = "Destination";
            // 
            // _destGrid
            // 
            this._destGrid.AutoSize = true;
            this._destGrid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._destGrid.ColumnCount = 4;
            this._destGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 230F));
            this._destGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._destGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this._destGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 240F));
            this._destGrid.Controls.Add(this._lblQuarantine, 0, 0);
            this._destGrid.Controls.Add(this._cboQuarantine, 1, 0);
            this._destGrid.Controls.Add(this._btnQuarantine, 2, 0);
            this._destGrid.Controls.Add(this._chkTrash, 3, 0);
            this._destGrid.Controls.Add(this._chkPreserveTree, 1, 1);
            this._destGrid.Controls.Add(this._btnMoveAll, 3, 1);
            this._destGrid.Dock = System.Windows.Forms.DockStyle.Top;
            this._destGrid.Location = new System.Drawing.Point(8, 21);
            this._destGrid.Name = "_destGrid";
            this._destGrid.RowCount = 2;
            this._destGrid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._destGrid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._destGrid.Size = new System.Drawing.Size(1524, 84);
            this._destGrid.TabIndex = 0;
            // 
            // _lblQuarantine
            // 
            this._lblQuarantine.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblQuarantine.Location = new System.Drawing.Point(3, 0);
            this._lblQuarantine.Name = "_lblQuarantine";
            this._lblQuarantine.Size = new System.Drawing.Size(224, 48);
            this._lblQuarantine.TabIndex = 0;
            this._lblQuarantine.Text = "&Move duplicates to:";
            this._lblQuarantine.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _btnQuarantine
            // 
            this._btnQuarantine.AutoSize = true;
            this._btnQuarantine.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._btnQuarantine.Location = new System.Drawing.Point(1167, 4);
            this._btnQuarantine.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this._btnQuarantine.MinimumSize = new System.Drawing.Size(88, 38);
            this._btnQuarantine.Name = "_btnQuarantine";
            this._btnQuarantine.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this._btnQuarantine.Size = new System.Drawing.Size(92, 38);
            this._btnQuarantine.TabIndex = 2;
            this._btnQuarantine.Text = "Bro&wse...";
            this._btnQuarantine.UseVisualStyleBackColor = true;
            this._btnQuarantine.Click += new System.EventHandler(this.BtnQuarantine_Click);
            // 
            // _btnMoveAll
            // 
            this._btnMoveAll.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._btnMoveAll.AutoSize = true;
            this._btnMoveAll.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._btnMoveAll.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._btnMoveAll.Location = new System.Drawing.Point(1287, 50);
            this._btnMoveAll.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this._btnMoveAll.MinimumSize = new System.Drawing.Size(150, 38);
            this._btnMoveAll.Name = "_btnMoveAll";
            this._btnMoveAll.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this._btnMoveAll.Size = new System.Drawing.Size(234, 38);
            this._btnMoveAll.TabIndex = 5;
            this._btnMoveAll.Text = "MOVE &ALL";
            this._tip.SetToolTip(this._btnMoveAll, "Move the duplicates of every group, keeping the copy marked in each (Ctrl+Shift+M)");
            this._btnMoveAll.UseVisualStyleBackColor = true;
            this._btnMoveAll.Click += new System.EventHandler(this.MoveAll_Click);
            // 
            // _sourceBox
            // 
            this._sourceBox.Controls.Add(this._sourceGrid);
            this._sourceBox.Dock = System.Windows.Forms.DockStyle.Top;
            this._sourceBox.Location = new System.Drawing.Point(0, 24);
            this._sourceBox.Name = "_sourceBox";
            this._sourceBox.Padding = new System.Windows.Forms.Padding(8, 2, 8, 6);
            this._sourceBox.Size = new System.Drawing.Size(1540, 170);
            this._sourceBox.TabIndex = 3;
            this._sourceBox.TabStop = false;
            this._sourceBox.Text = "Source";
            // 
            // _sourceGrid
            // 
            this._sourceGrid.AutoSize = true;
            this._sourceGrid.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._sourceGrid.ColumnCount = 4;
            this._sourceGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 230F));
            this._sourceGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._sourceGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this._sourceGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 240F));
            this._sourceGrid.Controls.Add(this._lblRoot, 0, 0);
            this._sourceGrid.Controls.Add(this._cboRoot, 1, 0);
            this._sourceGrid.Controls.Add(this._btnRoot, 2, 0);
            this._sourceGrid.Controls.Add(this._btnScan, 3, 0);
            this._sourceGrid.Controls.Add(this._opts, 0, 1);
            this._sourceGrid.Controls.Add(this._lblExtensions, 0, 2);
            this._sourceGrid.Controls.Add(this._txtExt, 1, 2);
            this._sourceGrid.Dock = System.Windows.Forms.DockStyle.Top;
            this._sourceGrid.Location = new System.Drawing.Point(8, 21);
            this._sourceGrid.Name = "_sourceGrid";
            this._sourceGrid.RowCount = 3;
            this._sourceGrid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._sourceGrid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._sourceGrid.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._sourceGrid.Size = new System.Drawing.Size(1524, 183);
            this._sourceGrid.TabIndex = 0;
            // 
            // _lblRoot
            // 
            this._lblRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblRoot.Location = new System.Drawing.Point(3, 0);
            this._lblRoot.Name = "_lblRoot";
            this._lblRoot.Size = new System.Drawing.Size(224, 48);
            this._lblRoot.TabIndex = 0;
            this._lblRoot.Text = "&Folder to scan:";
            this._lblRoot.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _btnRoot
            // 
            this._btnRoot.AutoSize = true;
            this._btnRoot.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._btnRoot.Location = new System.Drawing.Point(1167, 4);
            this._btnRoot.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this._btnRoot.MinimumSize = new System.Drawing.Size(88, 38);
            this._btnRoot.Name = "_btnRoot";
            this._btnRoot.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this._btnRoot.Size = new System.Drawing.Size(92, 38);
            this._btnRoot.TabIndex = 2;
            this._btnRoot.Text = "&Browse...";
            this._btnRoot.UseVisualStyleBackColor = true;
            this._btnRoot.Click += new System.EventHandler(this.BtnRoot_Click);
            // 
            // _btnScan
            // 
            this._btnScan.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._btnScan.AutoSize = true;
            this._btnScan.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._btnScan.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._btnScan.Location = new System.Drawing.Point(1287, 4);
            this._btnScan.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this._btnScan.MinimumSize = new System.Drawing.Size(150, 38);
            this._btnScan.Name = "_btnScan";
            this._btnScan.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this._btnScan.Size = new System.Drawing.Size(234, 38);
            this._btnScan.TabIndex = 3;
            this._btnScan.Text = "&SCAN";
            this._btnScan.UseVisualStyleBackColor = true;
            this._btnScan.Click += new System.EventHandler(this.BtnScan_Click);
            // 
            // _opts
            // 
            this._opts.AutoSize = true;
            this._sourceGrid.SetColumnSpan(this._opts, 4);
            this._opts.Controls.Add(this._chkRecursive);
            this._opts.Controls.Add(this._lblMatch);
            this._opts.Controls.Add(this._cboMatch);
            this._opts.Controls.Add(this._lblSensitivity);
            this._opts.Controls.Add(this._cboSensitivity);
            this._opts.Dock = System.Windows.Forms.DockStyle.Fill;
            this._opts.Location = new System.Drawing.Point(3, 51);
            this._opts.Name = "_opts";
            this._opts.MinimumSize = new System.Drawing.Size(0, 38);
            this._opts.Size = new System.Drawing.Size(1518, 38);
            this._opts.TabIndex = 8;
            // 
            // _lblMatch
            // 
            this._lblMatch.AutoSize = true;
            this._lblMatch.Location = new System.Drawing.Point(169, 8);
            this._lblMatch.Margin = new System.Windows.Forms.Padding(14, 9, 2, 0);
            this._lblMatch.Name = "_lblMatch";
            this._lblMatch.Size = new System.Drawing.Size(70, 19);
            this._lblMatch.TabIndex = 1;
            this._lblMatch.Text = "Matc&hing:";
            // 
            // _lblSensitivity
            // 
            this._lblSensitivity.AutoSize = true;
            this._lblSensitivity.Location = new System.Drawing.Point(514, 8);
            this._lblSensitivity.Margin = new System.Windows.Forms.Padding(14, 9, 2, 0);
            this._lblSensitivity.Name = "_lblSensitivity";
            this._lblSensitivity.Size = new System.Drawing.Size(73, 19);
            this._lblSensitivity.TabIndex = 3;
            this._lblSensitivity.Text = "Se&nsitivity:";
            // 
            // _lblExtensions
            // 
            this._lblExtensions.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblExtensions.Location = new System.Drawing.Point(3, 93);
            this._lblExtensions.Name = "_lblExtensions";
            this._lblExtensions.Size = new System.Drawing.Size(224, 45);
            this._lblExtensions.TabIndex = 9;
            this._lblExtensions.Text = "Scanned e&xtensions:";
            this._lblExtensions.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _txtExt
            // 
            this._sourceGrid.SetColumnSpan(this._txtExt, 3);
            this._txtExt.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtExt.Location = new System.Drawing.Point(233, 143);
            this._txtExt.Margin = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._txtExt.MinimumSize = new System.Drawing.Size(0, 34);
            this._txtExt.Multiline = true;
            this._txtExt.Name = "_txtExt";
            this._txtExt.Size = new System.Drawing.Size(1288, 34);
            this._txtExt.TabIndex = 10;
            this._txtExt.WordWrap = false;
            this._txtExt.Text = ".jpg;.jpeg;.jpe;.jfif;.png;.gif;.bmp;.tif;.tiff;.webp;.raw;.cr2;.nef;.arw;.dng;.orf;.rw2";
            // 
            // _menu
            // 
            this._menu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this._menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._mFile,
            this._mEdit,
            this._mHelp});
            this._menu.Location = new System.Drawing.Point(0, 0);
            this._menu.Name = "_menu";
            this._menu.Size = new System.Drawing.Size(1540, 24);
            this._menu.TabIndex = 1;
            // 
            // _mFile
            // 
            this._mFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._miScan,
            this._miFileSep1,
            this._miMoveAll,
            this._miFileSep2,
            this._miExport,
            this._miFileSep3,
            this._miExit});
            this._mFile.Name = "_mFile";
            this._mFile.Size = new System.Drawing.Size(37, 20);
            this._mFile.Text = "&File";
            // 
            // _miScan
            // 
            this._miScan.Name = "_miScan";
            this._miScan.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this._miScan.Size = new System.Drawing.Size(253, 22);
            this._miScan.Text = "&Scan";
            this._miScan.Click += new System.EventHandler(this.Scan_Click);
            // 
            // _miFileSep1
            // 
            this._miFileSep1.Name = "_miFileSep1";
            this._miFileSep1.Size = new System.Drawing.Size(250, 6);
            // 
            // _miMoveAll
            // 
            this._miMoveAll.Name = "_miMoveAll";
            this._miMoveAll.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.M)));
            this._miMoveAll.Size = new System.Drawing.Size(253, 22);
            this._miMoveAll.Text = "Move &all duplicates";
            this._miMoveAll.Click += new System.EventHandler(this.MoveAll_Click);
            // 
            // _miFileSep2
            // 
            this._miFileSep2.Name = "_miFileSep2";
            this._miFileSep2.Size = new System.Drawing.Size(250, 6);
            // 
            // _miExport
            // 
            this._miExport.Name = "_miExport";
            this._miExport.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.E)));
            this._miExport.Size = new System.Drawing.Size(253, 22);
            this._miExport.Text = "&Export list to CSV...";
            this._miExport.Click += new System.EventHandler(this.Export_Click);
            // 
            // _miFileSep3
            // 
            this._miFileSep3.Name = "_miFileSep3";
            this._miFileSep3.Size = new System.Drawing.Size(250, 6);
            // 
            // _miExit
            // 
            this._miExit.Name = "_miExit";
            this._miExit.Size = new System.Drawing.Size(253, 22);
            this._miExit.Text = "E&xit";
            this._miExit.Click += new System.EventHandler(this.Exit_Click);
            // 
            // _mEdit
            // 
            this._mEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._miKeepPreferred,
            this._miEditSep1,
            this._miKeepOldest,
            this._miKeepNewest,
            this._miKeepShortest,
            this._miKeepBest,
            this._miKeepLargest});
            this._mEdit.Name = "_mEdit";
            this._mEdit.Size = new System.Drawing.Size(39, 20);
            this._mEdit.Text = "&Edit";
            // 
            // _miKeepPreferred
            // 
            this._miKeepPreferred.Name = "_miKeepPreferred";
            this._miKeepPreferred.Size = new System.Drawing.Size(230, 22);
            this._miKeepPreferred.Text = "Favour the &preferred folders";
            this._miKeepPreferred.Click += new System.EventHandler(this.MiKeepPreferred_Click);
            // 
            // _miEditSep1
            // 
            this._miEditSep1.Name = "_miEditSep1";
            this._miEditSep1.Size = new System.Drawing.Size(227, 6);
            // 
            // _miKeepOldest
            // 
            this._miKeepOldest.Name = "_miKeepOldest";
            this._miKeepOldest.Size = new System.Drawing.Size(230, 22);
            this._miKeepOldest.Text = "Then keep the &oldest copy";
            this._miKeepOldest.Click += new System.EventHandler(this.MiKeepOldest_Click);
            // 
            // _miKeepNewest
            // 
            this._miKeepNewest.Name = "_miKeepNewest";
            this._miKeepNewest.Size = new System.Drawing.Size(230, 22);
            this._miKeepNewest.Text = "Then keep the &newest copy";
            this._miKeepNewest.Click += new System.EventHandler(this.MiKeepNewest_Click);
            // 
            // _miKeepShortest
            // 
            this._miKeepShortest.Name = "_miKeepShortest";
            this._miKeepShortest.Size = new System.Drawing.Size(230, 22);
            this._miKeepShortest.Text = "Then keep the &shortest path";
            this._miKeepShortest.Click += new System.EventHandler(this.MiKeepShortest_Click);
            // 
            // _miKeepBest
            // 
            this._miKeepBest.Name = "_miKeepBest";
            this._miKeepBest.Size = new System.Drawing.Size(230, 22);
            this._miKeepBest.Text = "Then keep the &best resolution";
            this._miKeepBest.Click += new System.EventHandler(this.MiKeepBest_Click);
            // 
            // _miKeepLargest
            // 
            this._miKeepLargest.Name = "_miKeepLargest";
            this._miKeepLargest.Size = new System.Drawing.Size(230, 22);
            this._miKeepLargest.Text = "Then keep the &largest file";
            this._miKeepLargest.Click += new System.EventHandler(this.MiKeepLargest_Click);
            // 
            // _mHelp
            // 
            this._mHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._miAbout});
            this._mHelp.Name = "_mHelp";
            this._mHelp.Size = new System.Drawing.Size(44, 20);
            this._mHelp.Text = "&Help";
            // 
            // _miAbout
            // 
            this._miAbout.Name = "_miAbout";
            this._miAbout.Size = new System.Drawing.Size(159, 22);
            this._miAbout.Text = "&About TwinPix...";
            this._miAbout.Click += new System.EventHandler(this.MiAbout_Click);
            // 
            // _status
            // 
            this._status.ImageScalingSize = new System.Drawing.Size(24, 24);
            this._status.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._statusLabel,
            this._paneGroups,
            this._paneDuplicates,
            this._paneReclaimable,
            this._progress});
            this._status.Location = new System.Drawing.Point(0, 813);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(1540, 32);
            this._status.TabIndex = 6;
            // 
            // _statusLabel
            // 
            this._statusLabel.Name = "_statusLabel";
            this._statusLabel.Size = new System.Drawing.Size(993, 27);
            this._statusLabel.Spring = true;
            this._statusLabel.Text = "Ready.";
            this._statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _paneGroups
            // 
            this._paneGroups.AutoSize = false;
            this._paneGroups.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this._paneGroups.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
            this._paneGroups.Name = "_paneGroups";
            this._paneGroups.Size = new System.Drawing.Size(110, 27);
            this._paneGroups.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _paneDuplicates
            // 
            this._paneDuplicates.AutoSize = false;
            this._paneDuplicates.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this._paneDuplicates.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
            this._paneDuplicates.Name = "_paneDuplicates";
            this._paneDuplicates.Size = new System.Drawing.Size(130, 27);
            this._paneDuplicates.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _paneReclaimable
            // 
            this._paneReclaimable.AutoSize = false;
            this._paneReclaimable.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;
            this._paneReclaimable.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
            this._paneReclaimable.Name = "_paneReclaimable";
            this._paneReclaimable.Size = new System.Drawing.Size(190, 27);
            this._paneReclaimable.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _progress
            // 
            this._progress.Name = "_progress";
            this._progress.Size = new System.Drawing.Size(100, 26);
            this._progress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            // 
            // MainForm
            // 
            this.AcceptButton = this._btnScan;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1540, 845);
            this.Controls.Add(this._centre);
            this.Controls.Add(this._destBox);
            this.Controls.Add(this._sourceBox);
            this.Controls.Add(this._menu);
            this.Controls.Add(this._status);
            this.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MainMenuStrip = this._menu;
            this.MinimumSize = new System.Drawing.Size(960, 640);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TwinPix - duplicate images";
            this._menuHistoryDestination.ResumeLayout(false);
            this._menuHistoryScan.ResumeLayout(false);
            this._centre.ResumeLayout(false);
            this._split.Panel1.ResumeLayout(false);
            this._split.Panel1.PerformLayout();
            this._split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
            this._split.ResumeLayout(false);
            this._splitLists.Panel1.ResumeLayout(false);
            this._splitLists.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._splitLists)).EndInit();
            this._splitLists.ResumeLayout(false);
            this._keepBar.ResumeLayout(false);
            this._keepBar.PerformLayout();
            this._destBox.ResumeLayout(false);
            this._destBox.PerformLayout();
            this._destGrid.ResumeLayout(false);
            this._destGrid.PerformLayout();
            this._sourceBox.ResumeLayout(false);
            this._sourceBox.PerformLayout();
            this._sourceGrid.ResumeLayout(false);
            this._sourceGrid.PerformLayout();
            this._opts.ResumeLayout(false);
            this._opts.PerformLayout();
            this._menu.ResumeLayout(false);
            this._menu.PerformLayout();
            this._status.ResumeLayout(false);
            this._status.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolTip _tip;
        private System.Windows.Forms.ImageList _fileIcons;
        private System.Windows.Forms.ContextMenuStrip _menuHistoryScan;
        private System.Windows.Forms.ToolStripMenuItem _miClearScanHistory;
        private System.Windows.Forms.ContextMenuStrip _menuHistoryDestination;
        private System.Windows.Forms.ToolStripMenuItem _miClearDestinationHistory;
        private System.Windows.Forms.Panel _centre;
        private System.Windows.Forms.SplitContainer _split;
        private System.Windows.Forms.SplitContainer _splitLists;
        private System.Windows.Forms.ListView _lv;
        private System.Windows.Forms.ColumnHeader _colKept;
        private System.Windows.Forms.ColumnHeader _colExt;
        private System.Windows.Forms.ColumnHeader _colSize;
        private System.Windows.Forms.ColumnHeader _colCopies;
        private System.Windows.Forms.ColumnHeader _colWasted;
        private System.Windows.Forms.ColumnHeader _colKeptIn;
        private System.Windows.Forms.FlowLayoutPanel _keepBar;
        private System.Windows.Forms.Label _lblKeep;
        private System.Windows.Forms.CheckBox _chkKeepPreferred;
        private System.Windows.Forms.CheckBox _chkKeepOldest;
        private System.Windows.Forms.CheckBox _chkKeepNewest;
        private System.Windows.Forms.CheckBox _chkKeepShortest;
        private System.Windows.Forms.CheckBox _chkKeepBest;
        private System.Windows.Forms.CheckBox _chkKeepLargest;
        private System.Windows.Forms.Label _lblGroups;
        private System.Windows.Forms.Label _lblFolders;
        private System.Windows.Forms.ListView _lvFolders;
        private System.Windows.Forms.ColumnHeader _colFolder;
        private System.Windows.Forms.ColumnHeader _colFolderImages;
        private System.Windows.Forms.ColumnHeader _colFolderDups;
        private System.Windows.Forms.FlowLayoutPanel _cards;
        private System.Windows.Forms.Label _lblGroupTitle;
        private System.Windows.Forms.GroupBox _destBox;
        private System.Windows.Forms.TableLayoutPanel _destGrid;
        private System.Windows.Forms.Label _lblQuarantine;
        private System.Windows.Forms.ComboBox _cboQuarantine;
        private System.Windows.Forms.Button _btnQuarantine;
        private System.Windows.Forms.Button _btnMoveAll;
        private System.Windows.Forms.CheckBox _chkTrash;
        private System.Windows.Forms.CheckBox _chkPreserveTree;
        private System.Windows.Forms.GroupBox _sourceBox;
        private System.Windows.Forms.TableLayoutPanel _sourceGrid;
        private System.Windows.Forms.Label _lblRoot;
        private System.Windows.Forms.ComboBox _cboRoot;
        private System.Windows.Forms.Button _btnRoot;
        private System.Windows.Forms.Button _btnScan;
        private System.Windows.Forms.FlowLayoutPanel _opts;
        private System.Windows.Forms.CheckBox _chkRecursive;
        private System.Windows.Forms.Label _lblMatch;
        private System.Windows.Forms.ComboBox _cboMatch;
        private System.Windows.Forms.Label _lblSensitivity;
        private System.Windows.Forms.ComboBox _cboSensitivity;
        private System.Windows.Forms.Label _lblExtensions;
        private System.Windows.Forms.TextBox _txtExt;
        private System.Windows.Forms.MenuStrip _menu;
        private System.Windows.Forms.ToolStripMenuItem _mFile;
        private System.Windows.Forms.ToolStripMenuItem _miScan;
        private System.Windows.Forms.ToolStripSeparator _miFileSep1;
        private System.Windows.Forms.ToolStripMenuItem _miMoveAll;
        private System.Windows.Forms.ToolStripSeparator _miFileSep2;
        private System.Windows.Forms.ToolStripMenuItem _miExport;
        private System.Windows.Forms.ToolStripSeparator _miFileSep3;
        private System.Windows.Forms.ToolStripMenuItem _miExit;
        private System.Windows.Forms.ToolStripMenuItem _mEdit;
        private System.Windows.Forms.ToolStripMenuItem _miKeepPreferred;
        private System.Windows.Forms.ToolStripSeparator _miEditSep1;
        private System.Windows.Forms.ToolStripMenuItem _miKeepOldest;
        private System.Windows.Forms.ToolStripMenuItem _miKeepNewest;
        private System.Windows.Forms.ToolStripMenuItem _miKeepShortest;
        private System.Windows.Forms.ToolStripMenuItem _miKeepBest;
        private System.Windows.Forms.ToolStripMenuItem _miKeepLargest;
        private System.Windows.Forms.ToolStripMenuItem _mHelp;
        private System.Windows.Forms.ToolStripMenuItem _miAbout;
        private System.Windows.Forms.StatusStrip _status;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabel;
        private System.Windows.Forms.ToolStripStatusLabel _paneGroups;
        private System.Windows.Forms.ToolStripStatusLabel _paneDuplicates;
        private System.Windows.Forms.ToolStripStatusLabel _paneReclaimable;
        private System.Windows.Forms.ToolStripProgressBar _progress;
    }
}
