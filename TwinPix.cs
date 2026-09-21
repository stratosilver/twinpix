// =====================================================================
//  TwinPix - Find and manage duplicate images
//  Win32 (WinForms) application in C#, buildable with nothing but the
//  compiler shipped with Windows:
//    %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
//  See build.bat
//
//  Duplicate criteria: size in bytes + extension (case-insensitive)
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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
            AppDomain.CurrentDomain.UnhandledException +=
                delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Report(e.ExceptionObject as Exception, "TwinPix - unhandled error");
                };
            Application.ThreadException +=
                delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    Report(e.Exception, "TwinPix - unexpected error");
                };

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Report(ex, "TwinPix could not start");
            }
        }

        /// <summary>
        /// Writes to the first location that accepts it: the settings folder,
        /// then the folder of the executable, then the temporary folder.
        /// Returns the file written, or null when none worked.
        /// </summary>
        static string Append(string fileName, string text)
        {
            string[] candidates = new string[3];
            try
            {
                candidates[0] = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "TwinPix");
            }
            catch { }
            try { candidates[1] = Path.GetDirectoryName(Application.ExecutablePath); }
            catch { }
            try { candidates[2] = Path.GetTempPath(); }
            catch { }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.IsNullOrEmpty(candidates[i])) continue;
                try
                {
                    if (!Directory.Exists(candidates[i])) Directory.CreateDirectory(candidates[i]);
                    string path = Path.Combine(candidates[i], fileName);
                    File.AppendAllText(path, text);
                    return path;
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Shows a failure and appends it to %APPDATA%\TwinPix\startup-error.txt,
        /// so a window that never appears still leaves a trace. Deliberately a
        /// plain message box: it must work even when the richer dialog cannot.
        /// </summary>
        static void Report(Exception ex, string title)
        {
            string text = ex == null ? "Unknown error." : ex.ToString();
            string written = Append("startup-error.txt",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine
                + text + Environment.NewLine + Environment.NewLine);

            string shown = text + Environment.NewLine + Environment.NewLine
                         + (written == null ? "(this report could not be saved to disk)"
                                            : "Saved to: " + written);
            try { MessageBox.Show(shown, title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
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

        /// <summary>
        /// A group is identified by its size and extension, so the files in it
        /// can all be named differently. The one to keep represents the group
        /// in the list; failing that, the first file does.
        /// </summary>
        public string DisplayName
        {
            get
            {
                FileEntry k = Kept;
                if (k != null) return k.FileName;
                return Files.Count > 0 ? Files[0].FileName : "";
            }
        }
    }

    public class ScanOptions
    {
        public string Root = "";
        public string Preferred = "";
        public bool Recursive = true;
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

            // Grouping: extension (lower-cased, so .JPG and .jpg match) | size
            var map = new Dictionary<string, DupGroup>(StringComparer.Ordinal);

            for (int i = 0; i < paths.Count; i++)
            {
                if (bw != null && bw.CancellationPending) return res;
                string p = paths[i];
                FileInfo fi;
                try { fi = new FileInfo(p); if (!fi.Exists) continue; }
                catch { res.Errors++; continue; }

                string ext = (fi.Extension ?? "").ToLowerInvariant();
                string key = ext + "|" + fi.Length.ToString(CultureInfo.InvariantCulture);

                DupGroup g;
                if (!map.TryGetValue(key, out g))
                {
                    g = new DupGroup();
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
                AutoSelect(g, DefaultRule, o.Preferred.Length > 0);
            }

            groups.Sort(delegate(DupGroup a, DupGroup b)
            {
                int c = b.Wasted.CompareTo(a.Wasted);
                if (c != 0) return c;
                c = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return a.Size.CompareTo(b.Size);
            });

            res.Groups = groups;
            return res;
        }

        public enum KeepRule { Oldest, Newest, ShortestPath }

        /// <summary>The rule the window starts with, and the one Scan() applies.</summary>
        public const KeepRule DefaultRule = KeepRule.ShortestPath;

        /// <summary>
        /// Marks the one file of the group to keep. <paramref name="preferFolder"/>
        /// puts every file of the preferred folder ahead of the rule; the rule
        /// then decides between the remaining candidates.
        /// </summary>
        public static void AutoSelect(DupGroup g, KeepRule rule, bool preferFolder)
        {
            FileEntry best = null;
            foreach (var f in g.Files)
                if (best == null || IsBetter(f, best, rule, preferFolder)) best = f;
            foreach (var f in g.Files) f.Keep = ReferenceEquals(f, best);
        }

        static int Depth(string p)
        {
            int n = 0;
            for (int i = 0; i < p.Length; i++)
                if (p[i] == Path.DirectorySeparatorChar || p[i] == '/') n++;
            return n;
        }

        static bool IsBetter(FileEntry a, FileEntry b, KeepRule rule, bool preferFolder)
        {
            if (preferFolder && a.InPreferred != b.InPreferred) return a.InPreferred;

            switch (rule)
            {
                case KeepRule.Oldest:
                    if (a.Modified != b.Modified) return a.Modified < b.Modified;
                    break;
                case KeepRule.Newest:
                    if (a.Modified != b.Modified) return a.Modified > b.Modified;
                    break;
                default: // ShortestPath: fewest folders deep, then shortest path
                    int da = Depth(a.FullPath), db = Depth(b.FullPath);
                    if (da != db) return da < db;
                    if (a.FullPath.Length != b.FullPath.Length)
                        return a.FullPath.Length < b.FullPath.Length;
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

        /// <summary>
        /// The application icon. It is embedded in the executable by build.bat
        /// (/resource:assets\twinpix.ico); if that is missing, the icon Windows
        /// associates with the executable is used instead.
        /// </summary>
        public static Icon AppIcon()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream st = asm.GetManifestResourceStream("TwinPix.twinpix.ico"))
                    if (st != null) return new Icon(st);
            }
            catch { }
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            return null;           // no icon available: the default one stays
        }

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
            catch (Exception ex)
            {
                Native.Show(null, "TwinPix", "The file could not be opened", ex.Message,
                            MessageBoxButtons.OK, Native.DialogIcon.Error);
            }
        }

        public static void ShowInExplorer(string path)
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex)
            {
                Native.Show(null, "TwinPix", "Explorer could not be opened", ex.Message,
                            MessageBoxButtons.OK, Native.DialogIcon.Error);
            }
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
    //  Thin wrappers over the Win32 controls that WinForms does not
    //  expose. Every helper is optional: off Windows, or where the
    //  feature is missing, it quietly does nothing and the application
    //  keeps the framework's default appearance.
    // -----------------------------------------------------------------
    public static class Native
    {
        public static bool IsWindows
        {
            get
            {
                PlatformID p = Environment.OSVersion.Platform;
                return p == PlatformID.Win32NT || p == PlatformID.Win32Windows;
            }
        }

        // ---- list view -------------------------------------------------
        const int LVM_FIRST = 0x1000;
        const int LVM_GETHEADER = LVM_FIRST + 31;
        const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
        const int LVS_EX_DOUBLEBUFFER = 0x00010000;

        const int HDM_FIRST = 0x1200;
        const int HDM_GETITEM = HDM_FIRST + 11;
        const int HDM_SETITEM = HDM_FIRST + 12;
        const int HDI_FORMAT = 0x0004;
        const int HDF_SORTUP = 0x0400;
        const int HDF_SORTDOWN = 0x0200;

        // ---- edit / combo ----------------------------------------------
        const int EM_SETCUEBANNER = 0x1501;
        const int CB_SETCUEBANNER = 0x1703;

        // ---- shell ------------------------------------------------------
        const uint SHGFI_ICON = 0x000000100;
        const uint SHGFI_SMALLICON = 0x000000001;
        const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        // ---- recycle bin -------------------------------------------------
        const uint FO_DELETE = 0x0003;
        const ushort FOF_NOCONFIRMATION = 0x0010;   // no "are you sure" per file
        const ushort FOF_ALLOWUNDO = 0x0040;        // recycle instead of destroy
        const ushort FOF_NOERRORUI = 0x0400;        // errors come back as a code
        const ushort FOF_WANTNUKEWARNING = 0x4000;  // still ask before destroying for good

        [StructLayout(LayoutKind.Sequential)]
        struct HDITEM
        {
            public int mask;
            public int cxy;
            public IntPtr pszText;
            public IntPtr hbm;
            public int cchTextMax;
            public int fmt;
            public IntPtr lParam;
            public int iImage;
            public int iOrder;
            public int type;
            public IntPtr pvFilter;
            public int state;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref HDITEM lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref SHFILEINFO psfi,
                                           uint cbFileInfo, uint flags);

        // shellapi.h packs SHFILEOPSTRUCT to 1 byte in 32-bit builds only, so the
        // structure is declared twice and the matching one is used at run time.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        struct SHFILEOPSTRUCT32
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEOPSTRUCT64
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperation")]
        static extern int SHFileOperation32(ref SHFILEOPSTRUCT32 op);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperation")]
        static extern int SHFileOperation64(ref SHFILEOPSTRUCT64 op);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("comctl32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int TaskDialog(IntPtr hwndParent, IntPtr hInstance, string title,
                                     string mainInstruction, string content,
                                     int commonButtons, IntPtr icon, out int pressedButton);

        /// <summary>Gives a control the Explorer look: subtle hover, themed header.</summary>
        public static void UseExplorerTheme(Control c)
        {
            if (!IsWindows || c == null || !c.IsHandleCreated) return;
            try { SetWindowTheme(c.Handle, "Explorer", null); }
            catch { }
        }

        /// <summary>Native double buffering: no flicker while scrolling.</summary>
        public static void EnableDoubleBuffer(ListView lv)
        {
            if (!IsWindows || lv == null || !lv.IsHandleCreated) return;
            try
            {
                SendMessage(lv.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE,
                            (IntPtr)LVS_EX_DOUBLEBUFFER, (IntPtr)LVS_EX_DOUBLEBUFFER);
            }
            catch { }
        }

        /// <summary>Draws the sort arrow in the column header itself.</summary>
        public static void SetSortArrow(ListView lv, int column, bool ascending)
        {
            if (!IsWindows || lv == null || !lv.IsHandleCreated) return;
            try
            {
                IntPtr header = SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (header == IntPtr.Zero) return;
                for (int i = 0; i < lv.Columns.Count; i++)
                {
                    var item = new HDITEM();
                    item.mask = HDI_FORMAT;
                    SendMessage(header, HDM_GETITEM, (IntPtr)i, ref item);
                    item.fmt &= ~(HDF_SORTUP | HDF_SORTDOWN);
                    if (i == column) item.fmt |= ascending ? HDF_SORTUP : HDF_SORTDOWN;
                    SendMessage(header, HDM_SETITEM, (IntPtr)i, ref item);
                }
            }
            catch { }
        }

        /// <summary>Grey prompt shown inside an empty field.</summary>
        public static void SetCueBanner(Control c, string text)
        {
            if (!IsWindows || c == null || !c.IsHandleCreated) return;
            try
            {
                if (c is ComboBox) SendMessage(c.Handle, CB_SETCUEBANNER, IntPtr.Zero, text);
                else if (c is TextBox) SendMessage(c.Handle, EM_SETCUEBANNER, IntPtr.Zero, text);
            }
            catch { }
        }

        /// <summary>The icon the shell associates with a file extension.</summary>
        public static Icon FileTypeIcon(string extension)
        {
            if (!IsWindows || string.IsNullOrEmpty(extension)) return null;
            try
            {
                var info = new SHFILEINFO();
                IntPtr r = SHGetFileInfo("file" + extension, FILE_ATTRIBUTE_NORMAL, ref info,
                                         (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                                         SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                if (r == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
                Icon copy = (Icon)Icon.FromHandle(info.hIcon).Clone();
                DestroyIcon(info.hIcon);
                return copy;
            }
            catch { return null; }
        }

        /// <summary>
        /// Sends files to the Recycle Bin in one shell operation, so the whole
        /// batch is a single "Undo move" in Explorer rather than one entry per
        /// file. The shell shows its own progress dialog and, when a file is too
        /// large for the bin, still asks before destroying it for good.
        /// Returns null when the shell reported success, otherwise a message.
        /// Whether a given file actually went is checked by the caller.
        /// </summary>
        public static string SendToRecycleBin(IWin32Window owner, IList<string> paths)
        {
            if (!IsWindows) return "The Recycle Bin is only available on Windows.";
            if (paths == null || paths.Count == 0) return null;

            // A double-null-terminated list: the marshaller adds the last null.
            var sb = new StringBuilder();
            for (int i = 0; i < paths.Count; i++) { sb.Append(paths[i]); sb.Append('\0'); }
            string from = sb.ToString();
            ushort flags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION
                                    | FOF_WANTNUKEWARNING | FOF_NOERRORUI);
            IntPtr parent = owner == null ? IntPtr.Zero : owner.Handle;

            try
            {
                int result;
                bool aborted;
                if (IntPtr.Size == 8)
                {
                    var op = new SHFILEOPSTRUCT64();
                    op.hwnd = parent;
                    op.wFunc = FO_DELETE;
                    op.pFrom = from;
                    op.fFlags = flags;
                    result = SHFileOperation64(ref op);
                    aborted = op.fAnyOperationsAborted;
                }
                else
                {
                    var op = new SHFILEOPSTRUCT32();
                    op.hwnd = parent;
                    op.wFunc = FO_DELETE;
                    op.pFrom = from;
                    op.fFlags = flags;
                    result = SHFileOperation32(ref op);
                    aborted = op.fAnyOperationsAborted;
                }

                if (result != 0) return "The Recycle Bin refused the operation (code " + result + ").";
                if (aborted) return "The operation was cancelled before every file was moved.";
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public enum DialogIcon { None, Information, Warning, Error }

        /// <summary>
        /// Vista task dialog: large main instruction, explanatory body, standard
        /// buttons. Falls back to a message box where comctl32 v6 is missing.
        /// </summary>
        public static DialogResult Show(IWin32Window owner, string title, string instruction,
                                        string content, MessageBoxButtons buttons, DialogIcon icon)
        {
            if (IsWindows)
            {
                try
                {
                    int common;
                    switch (buttons)
                    {
                        case MessageBoxButtons.OKCancel: common = 0x0001 | 0x0008; break;
                        case MessageBoxButtons.YesNo: common = 0x0002 | 0x0004; break;
                        case MessageBoxButtons.YesNoCancel: common = 0x0002 | 0x0004 | 0x0008; break;
                        default: common = 0x0001; break;
                    }
                    IntPtr hIcon = IntPtr.Zero;
                    if (icon == DialogIcon.Warning) hIcon = new IntPtr(65535);
                    else if (icon == DialogIcon.Error) hIcon = new IntPtr(65534);
                    else if (icon == DialogIcon.Information) hIcon = new IntPtr(65533);

                    int pressed;
                    IntPtr parent = owner == null ? IntPtr.Zero : owner.Handle;
                    int hr = TaskDialog(parent, IntPtr.Zero, title, instruction, content,
                                        common, hIcon, out pressed);
                    if (hr == 0)
                    {
                        switch (pressed)
                        {
                            case 1: return DialogResult.OK;
                            case 2: return DialogResult.Cancel;
                            case 6: return DialogResult.Yes;
                            case 7: return DialogResult.No;
                            default: return DialogResult.OK;
                        }
                    }
                }
                catch { }      // older comctl32: fall through to the message box
            }

            string body = string.IsNullOrEmpty(content) ? instruction
                                                        : instruction + "\r\n\r\n" + content;
            MessageBoxIcon mbi = MessageBoxIcon.None;
            if (icon == DialogIcon.Warning) mbi = MessageBoxIcon.Warning;
            else if (icon == DialogIcon.Error) mbi = MessageBoxIcon.Error;
            else if (icon == DialogIcon.Information) mbi = MessageBoxIcon.Information;
            return MessageBox.Show(owner, body, title, buttons, mbi);
        }
    }

    // -----------------------------------------------------------------
    //  Folder history, shared by every folder field and kept between runs
    //  in %APPDATA%\TwinPix\folders.txt
    // -----------------------------------------------------------------
    public class FolderHistory
    {
        public const int MaxEntries = 12;

        readonly Dictionary<string, List<string>> _map =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TwinPix");
                return Path.Combine(dir, "folders.txt");
            }
        }

        public List<string> Get(string key)
        {
            List<string> list;
            if (!_map.TryGetValue(key, out list))
            {
                list = new List<string>();
                _map[key] = list;
            }
            return list;
        }

        /// <summary>Puts a folder at the top of its list, without duplicates.</summary>
        public void Add(string key, string path)
        {
            if (path == null) return;
            path = path.Trim().TrimEnd('\\', '/');   // "C:\\Photos\\" and "C:/Photos" are one entry
            if (path.Length == 0) return;
            // a drive root keeps its separator: "C:" alone is not "C:\"
            if (path.Length == 2 && path[1] == ':') path += Path.DirectorySeparatorChar;

            List<string> list = Get(key);
            for (int i = list.Count - 1; i >= 0; i--)
                if (string.Equals(list[i], path, StringComparison.OrdinalIgnoreCase))
                    list.RemoveAt(i);

            list.Insert(0, path);
            while (list.Count > MaxEntries) list.RemoveAt(list.Count - 1);
        }

        public void Clear(string key) { Get(key).Clear(); }

        /// <summary>
        /// Single-value entries stored in the same file, verbatim: the window
        /// placement uses one. Unlike Add(), nothing is normalized here.
        /// </summary>
        public void SetValue(string key, string value)
        {
            List<string> list = Get(key);
            list.Clear();
            if (!string.IsNullOrEmpty(value)) list.Add(value);
        }

        public string GetValue(string key)
        {
            List<string> list = Get(key);
            return list.Count > 0 ? list[0] : null;
        }

        public void Load()
        {
            _map.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                string[] lines = File.ReadAllLines(FilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    int sep = lines[i].IndexOf('|');
                    if (sep <= 0) continue;
                    string key = lines[i].Substring(0, sep);
                    string path = lines[i].Substring(sep + 1).Trim();
                    if (path.Length == 0) continue;
                    List<string> list = Get(key);
                    if (list.Count < MaxEntries) list.Add(path);
                }
            }
            catch { }          // a missing or unreadable history is not an error
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                foreach (var kv in _map)
                    for (int i = 0; i < kv.Value.Count; i++)
                        sb.AppendLine(kv.Key + "|" + kv.Value[i]);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    // -----------------------------------------------------------------
    //  Sorts the duplicate-group list on the clicked column
    // -----------------------------------------------------------------
    public class GroupComparer : System.Collections.IComparer
    {
        readonly int _column;
        readonly bool _ascending;

        public GroupComparer(int column, bool ascending)
        {
            _column = column;
            _ascending = ascending;
        }

        public int Compare(object x, object y)
        {
            DupGroup a = ((ListViewItem)x).Tag as DupGroup;
            DupGroup b = ((ListViewItem)y).Tag as DupGroup;
            if (a == null || b == null) return 0;

            int r;
            switch (_column)
            {
                case 1: r = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase); break;
                case 2: r = a.Size.CompareTo(b.Size); break;
                case 3: r = a.Files.Count.CompareTo(b.Files.Count); break;
                case 4: r = a.Wasted.CompareTo(b.Wasted); break;
                case 5: r = string.Compare(KeptDir(a), KeptDir(b), StringComparison.OrdinalIgnoreCase); break;
                default: r = string.Compare(a.DisplayName, b.DisplayName,
                                            StringComparison.OrdinalIgnoreCase); break;
            }
            // stable, predictable order for equal values
            if (r == 0) r = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase);
            if (r == 0) r = a.Size.CompareTo(b.Size);
            return _ascending ? r : -r;
        }

        static string KeptDir(DupGroup g)
        {
            FileEntry k = g.Kept;
            return k == null ? "" : k.DirectoryPath;
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
        const string DefaultExtensions =
            ".jpg;.jpeg;.jpe;.jfif;.png;.gif;.bmp;.tif;.tiff;.webp;.heic;.heif;.ico;.psd;.svg;.raw;.cr2;.nef;.arw;.dng;.orf;.rw2";

        ComboBox _cboRoot, _cboPreferred, _cboQuarantine;
        TextBox _txtExt;
        Button _btnRoot, _btnPreferred, _btnQuarantine, _btnScan;
        CheckBox _chkRecursive, _chkContent, _chkPreserveTree, _chkTrash;
        Label _lblQuarantine;
        ListView _lv;
        ImageList _fileIcons;
        FlowLayoutPanel _cards;
        Label _lblGroupTitle;
        SplitContainer _split;
        GroupBox _sourceBox, _destBox;
        TableLayoutPanel _sourceGrid, _destGrid;

        MenuStrip _menu;
        ToolStripMenuItem _miScan, _miExport, _miMoveAll,
                          _miKeepPreferred, _miKeepOldest, _miKeepNewest, _miKeepShortest;
        ToolStrip _toolbar;
        FlowLayoutPanel _keepBar;
        ToolStripButton _tsScan, _tsMoveAll, _tsExport;
        CheckBox _chkKeepPreferred, _chkKeepOldest, _chkKeepNewest, _chkKeepShortest;
        bool _suspendRules;              // guards the check boxes against echoing
        string _appliedPreferred = "";   // preferred folder the current selection used
        Timer _prefTimer;                // waits for a pause before redoing the selection
        bool _hasScanned;                // a scan has already filled the list at least once

        StatusStrip _status;
        ToolStripStatusLabel _statusLabel, _paneGroups, _paneDuplicates, _paneReclaimable;
        ToolStripProgressBar _progress;
        ToolTip _tip = new ToolTip();

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
        const double ListWidthRatio = 0.75;

        // Sort state of the duplicate-group list.
        int _sortColumn = 4;              // Reclaimable
        bool _sortAscending;              // largest first

        public MainForm()
        {
            Text = "TwinPix - duplicate images";
            Font = Util.UiFont;
            Icon appIcon = Util.AppIcon();
            if (appIcon != null) Icon = appIcon;
            Width = 1240;
            Height = 840;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(960, 640);
            _history.Load();
            BuildUi();
            FillCombo(_cboRoot, KeyScan);
            FillCombo(_cboPreferred, KeyPreferred);
            FillCombo(_cboQuarantine, KeyDestination);
            RestorePlacement();
        }

        // ---------------------- User interface ------------------------
        void BuildUi()
        {
            ToolStripManager.RenderMode = ToolStripManagerRenderMode.System;

            // ----- centre area (added first: it gets the remaining space)
            var split = new SplitContainer();
            _split = split;
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterWidth = 6;

            // --- left half: the list of duplicate groups
            var leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Fill;

            _lv = new ListView();
            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.MultiSelect = false;
            _lv.HideSelection = false;
            _lv.GridLines = false;           // the Explorer theme draws its own rows
            _lv.Columns.Add("Kept file", 230);
            _lv.Columns.Add("Ext", 70);
            _lv.Columns.Add("Size", 100, HorizontalAlignment.Right);
            _lv.Columns.Add("Copies", 80, HorizontalAlignment.Right);
            _lv.Columns.Add("Reclaimable", 120, HorizontalAlignment.Right);
            _lv.Columns.Add("Kept in", 260);
            _lv.SelectedIndexChanged += LvSelectionChanged;
            _lv.ColumnClick += LvColumnClick;
            if (Native.IsWindows)
            {
                _fileIcons = new ImageList();
                _fileIcons.ColorDepth = ColorDepth.Depth32Bit;
                _fileIcons.ImageSize = new Size(16, 16);
                _lv.SmallImageList = _fileIcons;
            }
            leftPanel.Controls.Add(_lv);

            // Selection rules sit right above the list they act on.
            _keepBar = new FlowLayoutPanel();
            _keepBar.Dock = DockStyle.Top;
            _keepBar.AutoSize = true;
            _keepBar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _keepBar.WrapContents = true;

            var lblKeep = new Label();
            lblKeep.Text = "Keep:";
            lblKeep.AutoSize = true;
            lblKeep.MinimumSize = new Size(0, Util.RowHeight);
            lblKeep.TextAlign = ContentAlignment.MiddleLeft;
            lblKeep.Margin = new Padding(2, 2, 8, 2);
            _keepBar.Controls.Add(lblKeep);

            // Driven by the Preferred folder field, and independent of the three
            // rules below: ticked, it puts that folder ahead of whatever they say.
            _chkKeepPreferred = MakeCheck("Preferred folder", false);
            _chkKeepPreferred.Enabled = false;
            _tip.SetToolTip(_chkKeepPreferred,
                "Keep the copy that sits in the preferred folder, whatever the rule says."
                + "\r\nFollows the Preferred folder field above.");
            _keepBar.Controls.Add(_chkKeepPreferred);

            // Exactly one of the three is always ticked.
            _chkKeepOldest = MakeCheck("Oldest", false);
            _tip.SetToolTip(_chkKeepOldest,
                "Of the remaining copies, keep the one with the oldest date.");
            _chkKeepNewest = MakeCheck("Newest", false);
            _tip.SetToolTip(_chkKeepNewest,
                "Of the remaining copies, keep the one with the newest date.");
            _chkKeepShortest = MakeCheck("Shortest path",
                Scanner.DefaultRule == Scanner.KeepRule.ShortestPath);
            _tip.SetToolTip(_chkKeepShortest,
                "Of the remaining copies, keep the one closest to the scanned folder.");
            _keepBar.Controls.Add(_chkKeepOldest);
            _keepBar.Controls.Add(_chkKeepNewest);
            _keepBar.Controls.Add(_chkKeepShortest);

            _chkKeepPreferred.CheckedChanged += delegate { RuleChanged(null); };
            _chkKeepOldest.CheckedChanged += delegate { RuleChanged(_chkKeepOldest); };
            _chkKeepNewest.CheckedChanged += delegate { RuleChanged(_chkKeepNewest); };
            _chkKeepShortest.CheckedChanged += delegate { RuleChanged(_chkKeepShortest); };

            leftPanel.Controls.Add(_keepBar);

            var lblLeft = new Label();
            lblLeft.Dock = DockStyle.Top;
            lblLeft.Height = 26;
            lblLeft.Padding = new Padding(0, 6, 0, 0);
            lblLeft.Text = "&Duplicate groups";
            lblLeft.Font = Util.UiFontBold;
            leftPanel.Controls.Add(lblLeft);      // added last: docks above the toolbar
            split.Panel1.Controls.Add(leftPanel);

            // --- right half: the files of the selected group
            var rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;

            _cards = new FlowLayoutPanel();
            _cards.Dock = DockStyle.Fill;
            _cards.AutoScroll = true;
            _cards.BackColor = Color.FromArgb(250, 250, 250);
            _cards.Padding = new Padding(6);
            rightPanel.Controls.Add(_cards);

            _lblGroupTitle = new Label();
            _lblGroupTitle.Dock = DockStyle.Top;
            _lblGroupTitle.Height = 26;
            _lblGroupTitle.Padding = new Padding(0, 6, 0, 0);
            _lblGroupTitle.Font = Util.UiFontBold;
            _lblGroupTitle.AutoEllipsis = true;
            _lblGroupTitle.Text = "No group selected";
            rightPanel.Controls.Add(_lblGroupTitle);

            split.Panel2.Controls.Add(rightPanel);
            // Panel1MinSize / Panel2MinSize are NOT set here: the container is
            // still at its default 150 px, and Windows validates them against
            // the current width and splitter position. They are applied in
            // ApplySplitLayout(), once the window has its real size.

            // The centre area sits in a padded host so the two panels keep the
            // same margin from the window edge as the bands above and below.
            var centre = new Panel();
            centre.Dock = DockStyle.Fill;
            centre.Padding = new Padding(Util.SideMargin, 0, Util.SideMargin, 2);
            centre.Controls.Add(split);
            Controls.Add(centre);

            // ----- destination band (docked bottom, added before the top ones)
            var destination = new GroupBox();
            _destBox = destination;
            destination.Text = "Destination";
            destination.Dock = DockStyle.Bottom;
            destination.Padding = new Padding(8, 2, 8, 6);
            destination.Height = 80;            // replaced in OnLoad by the real content height

            var bottom = NewFieldGrid();
            _destGrid = bottom;
            _lblQuarantine = MakeLabel("&Move duplicates to:");
            bottom.Controls.Add(_lblQuarantine, 0, 0);
            _cboQuarantine = MakeCombo(KeyDestination);
            _tip.SetToolTip(_cboQuarantine, "Recently used folders are kept in the drop-down list.");
            bottom.Controls.Add(_cboQuarantine, 1, 0);
            _btnQuarantine = MakeButton("Bro&wse...", 88,
                delegate { Browse(_cboQuarantine, "Destination folder for duplicates", KeyDestination); });
            bottom.Controls.Add(_btnQuarantine, 2, 0);

            // Ticked, it replaces the destination folder: the duplicates go to the
            // Windows Recycle Bin, from where they can be put back.
            _chkTrash = MakeCheck("Move to &trash", false);
            _tip.SetToolTip(_chkTrash,
                "Send the duplicates to the Windows Recycle Bin instead of a folder."
                + "\r\nThey can be restored from there; no destination folder is needed.");
            _chkTrash.Enabled = Native.IsWindows;
            _chkTrash.CheckedChanged += delegate { TrashModeChanged(); };
            bottom.Controls.Add(_chkTrash, 3, 0);

            _chkPreserveTree = MakeCheck("&Keep folder structure", true);
            _tip.SetToolTip(_chkPreserveTree,
                "Recreate the original subfolders inside the destination.");
            bottom.Controls.Add(_chkPreserveTree, 1, 1);
            bottom.SetColumnSpan(_chkPreserveTree, 3);

            destination.Controls.Add(bottom);
            Controls.Add(destination);

            // ----- source band
            var source = new GroupBox();
            _sourceBox = source;
            source.Text = "Source";
            source.Dock = DockStyle.Top;
            source.Padding = new Padding(8, 2, 8, 6);
            source.Height = 200;                // replaced in OnLoad by the real content height

            var top = NewFieldGrid();
            _sourceGrid = top;
            top.Controls.Add(MakeLabel("&Folder to scan:"), 0, 0);
            _cboRoot = MakeCombo(KeyScan);
            _tip.SetToolTip(_cboRoot, "Recently used folders are kept in the drop-down list.");
            top.Controls.Add(_cboRoot, 1, 0);
            _btnRoot = MakeButton("&Browse...", 88,
                delegate { Browse(_cboRoot, "Folder to scan", KeyScan); });
            top.Controls.Add(_btnRoot, 2, 0);
            _btnScan = MakeButton("&SCAN", 150, delegate { StartScan(); });
            EmphasizeButton(_btnScan);
            AcceptButton = _btnScan;         // Windows outlines the default button
            top.Controls.Add(_btnScan, 3, 0);

            top.Controls.Add(MakeLabel("&Preferred folder:"), 0, 1);
            _cboPreferred = MakeCombo(KeyPreferred);
            // Typing fires TextChanged per keystroke, so the list is rebuilt once
            // typing pauses; leaving the box or picking a folder does it at once.
            _prefTimer = new Timer();
            _prefTimer.Interval = 400;
            _prefTimer.Tick += delegate { _prefTimer.Stop(); PreferredFolderCommitted(); };
            _cboPreferred.TextChanged += delegate { PreferredFolderChanged(); };
            _cboPreferred.Leave += delegate
            {
                if (_prefTimer != null) _prefTimer.Stop();
                PreferredFolderCommitted();
            };
            _cboPreferred.SelectedIndexChanged += delegate
            {
                if (_prefTimer != null) _prefTimer.Stop();
                PreferredFolderCommitted();
            };
            _tip.SetToolTip(_cboPreferred,
                "Images inside this folder (and its subfolders) are kept by default.");
            top.Controls.Add(_cboPreferred, 1, 1);
            _btnPreferred = MakeButton("B&rowse...", 88,
                delegate { Browse(_cboPreferred, "Preferred folder", KeyPreferred); });
            top.Controls.Add(_btnPreferred, 2, 1);
            var btnClearPref = MakeButton("C&lear", 142, delegate { _cboPreferred.Text = ""; });
            top.Controls.Add(btnClearPref, 3, 1);

            var opts = new FlowLayoutPanel();
            opts.AutoSize = true;
            opts.WrapContents = true;
            opts.Dock = DockStyle.Fill;
            _chkRecursive = MakeCheck("&Include subfolders", true);
            _tip.SetToolTip(_chkRecursive,
                "Also look inside the subfolders."
                + "\r\nChanging this searches the folder again.");
            _chkContent = MakeCheck("&Check content (MD5)", false);
            _tip.SetToolTip(_chkContent,
                "Slower: confirms the files really are identical."
                + "\r\nChanging this searches the folder again.");
            // Both change what a scan finds, so the list is rebuilt on the spot.
            _chkRecursive.CheckedChanged += delegate { ScanOptionChanged(); };
            _chkContent.CheckedChanged += delegate { ScanOptionChanged(); };
            opts.Controls.Add(_chkRecursive);
            opts.Controls.Add(_chkContent);
            top.Controls.Add(opts, 0, 2);
            top.SetColumnSpan(opts, 4);

            top.Controls.Add(MakeLabel("Scanned e&xtensions:"), 0, 3);
            _txtExt = MakeText();
            _txtExt.Text = DefaultExtensions;
            top.Controls.Add(_txtExt, 1, 3);
            top.SetColumnSpan(_txtExt, 3);

            source.Controls.Add(top);
            Controls.Add(source);

            // ----- toolbar
            _toolbar = new ToolStrip();
            _toolbar.Dock = DockStyle.Top;
            _toolbar.GripStyle = ToolStripGripStyle.Hidden;
            _tsScan = AddBarButton(_toolbar, "Scan", "Search the folder for duplicates (F5)",
                                   delegate { StartScan(); });
            Image scanImage = SmallAppImage();
            if (scanImage != null)
            {
                _tsScan.Image = scanImage;
                _tsScan.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            }
            _toolbar.Items.Add(new ToolStripSeparator());
            _tsMoveAll = AddBarButton(_toolbar, "Move all",
                "Move the duplicates of every group (Ctrl+Shift+M)",
                delegate { MoveAllDuplicates(); });
            _toolbar.Items.Add(new ToolStripSeparator());
            _tsExport = AddBarButton(_toolbar, "Export CSV",
                "Write the full inventory to a CSV file (Ctrl+E)", delegate { ExportCsv(); });
            Controls.Add(_toolbar);

            // ----- menu bar
            _menu = new MenuStrip();
            _menu.Dock = DockStyle.Top;

            var mFile = new ToolStripMenuItem("&File");
            _miScan = NewMenuItem("&Scan", Keys.F5, delegate { StartScan(); });
            _miMoveAll = NewMenuItem("Move &all duplicates",
                                     Keys.Control | Keys.Shift | Keys.M,
                                     delegate { MoveAllDuplicates(); });
            _miExport = NewMenuItem("&Export list to CSV...", Keys.Control | Keys.E,
                                    delegate { ExportCsv(); });
            var miExit = NewMenuItem("E&xit", Keys.None, delegate { Close(); });
            mFile.DropDownItems.AddRange(new ToolStripItem[] {
                _miScan, new ToolStripSeparator(),
                _miMoveAll, new ToolStripSeparator(),
                _miExport, new ToolStripSeparator(), miExit });

            var mEdit = new ToolStripMenuItem("&Edit");
            _miKeepPreferred = NewMenuItem("Favour the &preferred folder", Keys.None,
                delegate
                {
                    if (_chkKeepPreferred.Enabled)
                        _chkKeepPreferred.Checked = !_chkKeepPreferred.Checked;
                });
            _miKeepOldest = NewMenuItem("Then keep the &oldest copy",
                Keys.None, delegate { _chkKeepOldest.Checked = true; });
            _miKeepNewest = NewMenuItem("Then keep the &newest copy",
                Keys.None, delegate { _chkKeepNewest.Checked = true; });
            _miKeepShortest = NewMenuItem("Then keep the &shortest path",
                Keys.None, delegate { _chkKeepShortest.Checked = true; });
            mEdit.DropDownItems.AddRange(new ToolStripItem[] {
                _miKeepPreferred, new ToolStripSeparator(),
                _miKeepOldest, _miKeepNewest, _miKeepShortest });

            var mHelp = new ToolStripMenuItem("&Help");
            mHelp.DropDownItems.Add(NewMenuItem("&About TwinPix...", Keys.None,
                                                delegate { ShowAbout(); }));

            _menu.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mHelp });
            Controls.Add(_menu);
            MainMenuStrip = _menu;

            // ----- status bar
            _status = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Ready.");
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _paneGroups = NewStatusPane(110);
            _paneDuplicates = NewStatusPane(130);
            _paneReclaimable = NewStatusPane(190);
            _progress = new ToolStripProgressBar();
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.Visible = false;
            _status.Items.AddRange(new ToolStripItem[] {
                _statusLabel, _paneGroups, _paneDuplicates, _paneReclaimable, _progress });
            Controls.Add(_status);

            SyncRuleMenu();
            TrashModeChanged();
            EnableActions(false);
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
        }

        static void FitGroupBox(GroupBox box, Control content)
        {
            if (box == null || content == null) return;
            int inner = Math.Max(content.Height, content.PreferredSize.Height);
            int needed = inner + box.Padding.Vertical + 22;            // 22: the caption band
            if (needed > 0 && needed != box.Height) box.Height = needed;
        }

        /// <summary>The four-column grid both bands use for their fields.</summary>
        TableLayoutPanel NewFieldGrid()
        {
            var grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Top;
            grid.AutoSize = true;
            grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grid.ColumnCount = 4;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            return grid;
        }

        ToolStripButton AddBarButton(ToolStrip bar, string text, string tip, EventHandler onClick)
        {
            var b = new ToolStripButton(text);
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.ToolTipText = tip;
            b.Click += onClick;
            bar.Items.Add(b);
            return b;
        }

        static ToolStripMenuItem NewMenuItem(string text, Keys shortcut, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
            return item;
        }

        static ToolStripStatusLabel NewStatusPane(int width)
        {
            var pane = new ToolStripStatusLabel();
            pane.AutoSize = false;
            pane.Width = width;
            pane.TextAlign = ContentAlignment.MiddleLeft;
            pane.BorderSides = ToolStripStatusLabelBorderSides.Left;
            pane.BorderStyle = Border3DStyle.Etched;
            return pane;
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
                "Finds duplicate images by name, size and extension, then moves the copies "
                + "you do not keep to a folder of your choice.\r\n\r\n"
                + "Built with the C# compiler shipped with Windows.",
                MessageBoxButtons.OK, Native.DialogIcon.Information);
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

        /// <summary>Editable folder field whose drop-down keeps the folders used before.</summary>
        ComboBox MakeCombo(string historyKey)
        {
            var c = new ComboBox();
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(3, 5, 3, 5);
            c.DropDownStyle = ComboBoxStyle.DropDown;
            c.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            c.AutoCompleteSource = AutoCompleteSource.FileSystemDirectories;
            c.MaxDropDownItems = FolderHistory.MaxEntries;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Clear this list", null, delegate
            {
                _history.Clear(historyKey);
                _history.Save();
                FillCombo(c, historyKey);
            });
            c.ContextMenuStrip = menu;
            return c;
        }

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

        /// <summary>
        /// Marks a primary button. A custom BackColor would make Windows drop
        /// the visual-style rendering and draw a square-cornered classic button,
        /// so the button is left untouched apart from its bold label. SCAN is
        /// also the form's default button, which Windows outlines by itself.
        /// </summary>
        static void EmphasizeButton(Button b)
        {
            b.Font = Util.UiFontBold;
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

        void EnableActions(bool on)
        {
            _miMoveAll.Enabled = on;
            _miExport.Enabled = on;
            _tsMoveAll.Enabled = on;
            _tsExport.Enabled = on;
            // the keep rules stay available: they are settings, not actions
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
                _statusLabel.Text = res.FilesScanned + " image(s) scanned - "
                                  + _groups.Count + " duplicate group(s)"
                                  + (res.Errors > 0 ? " - " + res.Errors + " unreadable item(s)" : "");
                EnableActions(_groups.Count > 0);
                if (_groups.Count == 0)
                    Native.Show(this, "TwinPix", "No duplicates found",
                                "No two images share the same name, size and extension "
                                + "in this folder.",
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

        /// <summary>A click on a header sorts by that column, a second click reverses it.</summary>
        void LvColumnClick(object sender, ColumnClickEventArgs e)
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

        void ApplySort()
        {
            _lv.ListViewItemSorter = new GroupComparer(_sortColumn, _sortAscending);
            _lv.Sort();
            Native.SetSortArrow(_lv, _sortColumn, _sortAscending);
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
            // the panel is narrow by default, so the title stays short
            _lblGroupTitle.Text = Util.FormatSize(g.Size) + " " + g.Extension
                                + "  -  " + g.Files.Count + " files";
            _tip.SetToolTip(_lblGroupTitle, g.Files.Count + " files of exactly "
                            + Util.FormatSize(g.Size) + " with the " + g.Extension + " extension"
                            + "\r\nClick an image to mark it as the one to keep.");
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

        /// <summary>The rule ticked in the bar, one of the three being always on.</summary>
        Scanner.KeepRule CurrentRule
        {
            get
            {
                if (_chkKeepOldest.Checked) return Scanner.KeepRule.Oldest;
                if (_chkKeepNewest.Checked) return Scanner.KeepRule.Newest;
                return Scanner.KeepRule.ShortestPath;
            }
        }

        /// <summary>
        /// Keeps the three rule boxes mutually exclusive - and never all three
        /// off - then re-applies the selection to every group.
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
                        if (source != _chkKeepOldest) _chkKeepOldest.Checked = false;
                        if (source != _chkKeepNewest) _chkKeepNewest.Checked = false;
                        if (source != _chkKeepShortest) _chkKeepShortest.Checked = false;
                    }
                }
                SyncRuleMenu();
            }
            finally { _suspendRules = false; }

            ApplyRule();
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
        /// "Include subfolders" and "Check content" change what a scan finds, so
        /// the folder is searched again the moment one of them is ticked. Before
        /// the first scan there is nothing to refresh, and while a scan is running
        /// the new setting simply applies to the next one.
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
                if (g.Files.Count > 1) remaining.Add(g);
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

        /// <summary>The group list gets three quarters of the window by default.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // These need a live window handle, so they happen here, not in BuildUi.
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
            if (_prefTimer != null) { _prefTimer.Stop(); _prefTimer.Dispose(); _prefTimer = null; }
            if (_worker != null && _worker.IsBusy) _worker.CancelAsync();
            SavePlacement();
            _history.Add(KeyScan, _cboRoot.Text);
            _history.Add(KeyPreferred, _cboPreferred.Text);
            _history.Add(KeyDestination, _cboQuarantine.Text);
            _history.Save();
            base.OnFormClosing(e);
        }
    }
}
