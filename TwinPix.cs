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
using System.Drawing.Imaging;
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

        // Visual matching only: the picture's fingerprint, and how far this
        // copy sits from the reference copy of its group, in bits out of 64.
        public Fingerprint Fp;
        public int Distance;

        public int PixelWidth { get { return Fp == null ? 0 : Fp.PixelWidth; } }
        public int PixelHeight { get { return Fp == null ? 0 : Fp.PixelHeight; } }
        public long Pixels { get { return (long)PixelWidth * PixelHeight; } }

        public string Dimensions
        {
            get
            {
                return PixelWidth > 0 && PixelHeight > 0
                     ? PixelWidth + "x" + PixelHeight : "";
            }
        }

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
        public long Size;            // the largest copy: what the list shows and sorts on
        public long SizeMin;
        public long Wasted;
        public bool Visual;          // matched by appearance, so the copies may differ
        public List<FileEntry> Files = new List<FileEntry>();

        /// <summary>
        /// Refreshes what the group says about itself. Needed because the
        /// copies of a visually matched group no longer share one size or even
        /// one extension, and because a move takes files out of the group.
        /// Reclaimable space is counted against the largest copy - the best
        /// case, and the one the "Best resolution" rule aims at.
        /// </summary>
        public void Recompute()
        {
            string ext = null;
            long max = 0, min = long.MaxValue, total = 0;
            foreach (var f in Files)
            {
                if (ext == null) ext = f.Extension;
                else if (!string.Equals(ext, f.Extension, StringComparison.OrdinalIgnoreCase))
                    ext = "mixed";
                if (f.Size > max) max = f.Size;
                if (f.Size < min) min = f.Size;
                total += f.Size;
            }
            Extension = ext ?? "";
            Size = max;
            SizeMin = Files.Count > 0 ? min : 0;
            Wasted = Files.Count > 1 ? total - max : 0;
        }

        public bool SizesDiffer { get { return SizeMin != Size; } }

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

    /// <summary>What counts as a duplicate.</summary>
    public enum MatchMode
    {
        NameSize,        // same size and extension: fast, and never wrong about bytes
        Content,         // the above, confirmed by MD5
        Visual           // the same picture, whatever its size, format or quality
    }

    public class ScanOptions
    {
        public string Root = "";
        public string Preferred = "";
        public bool Recursive = true;
        public MatchMode Mode = MatchMode.NameSize;

        /// <summary>Bits out of 64 two fingerprints may differ by, visual mode only.</summary>
        public int MaxDistance = 6;

        /// <summary>Shared with the window, so it survives from one scan to the next.</summary>
        public FingerprintCache Cache;

        public HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool CompareContent { get { return Mode == MatchMode.Content; } }
    }

    public class ScanResult
    {
        public List<DupGroup> Groups = new List<DupGroup>();
        public int FilesScanned;
        public int Errors;
        public int Fingerprinted;    // images actually decoded this time
        public int FromCache;        // images whose fingerprint was already known
        public int Skipped;          // too small, or too uniform to be told apart
    }

    // -----------------------------------------------------------------
    //  Perceptual fingerprint
    //
    //  What a picture looks like, in 144 bytes, whatever its file size,
    //  its dimensions, its format or its JPEG quality:
    //
    //    dHash  64 bits  - a 9x8 grey grid, one bit per "is this pixel
    //                      brighter than the one on its right". Immune to
    //                      scaling, to re-compression and to a change of
    //                      overall brightness.
    //    pHash  64 bits  - the low frequencies of a 32x32 DCT, thresholded
    //                      at their median. Sturdier, used to confirm.
    //    Grid   64 bytes - an 8x8 grey grid, kept to compare two candidates
    //                      pixel by pixel and throw out the look-alikes the
    //                      two hashes agree on by accident.
    // -----------------------------------------------------------------
    public class Fingerprint
    {
        public ulong DHash;
        public ulong PHash;
        public byte[] Grid;          // 8x8 grey levels
        public int PixelWidth;
        public int PixelHeight;
        public bool Flat;            // too uniform to be told apart from another

        public double AspectRatio
        {
            get { return PixelHeight > 0 ? (double)PixelWidth / PixelHeight : 0; }
        }
    }

    public static class ImageHash
    {
        // Bumped whenever the way a fingerprint is computed changes, so an
        // old cache is dropped instead of being compared with new values.
        public const int Version = 1;

        const int Grid = 32;         // working resolution: 32 x 32 grey levels

        /// <summary>
        /// The fingerprint of one image, or null when it cannot be read.
        /// Nothing here touches the user interface, so it runs on any thread.
        /// </summary>
        public static Fingerprint Compute(string path)
        {
            int w = 0, h = 0;
            byte[] grey = Decode(path, ref w, ref h);
            if (grey == null) return null;

            var fp = new Fingerprint();
            fp.PixelWidth = w;
            fp.PixelHeight = h;
            fp.Grid = Resample(grey, Grid, Grid, 8, 8);
            fp.DHash = DHash(Resample(grey, Grid, Grid, 9, 8));
            fp.PHash = PHash(grey);
            fp.Flat = StdDev(grey) < 6.0;
            return fp;
        }

        // ---------------- decoding ------------------------------------

        /// <summary>
        /// A 32x32 grey thumbnail, and the real pixel size of the image.
        /// WIC (when compiled in) asks the JPEG decoder for a scaled-down
        /// image, which stops at 1/8 resolution in the DCT domain instead of
        /// unpacking millions of pixels. GDI+ has no such thing and decodes
        /// everything, so it is only the fallback.
        /// </summary>
        static byte[] Decode(string path, ref int width, ref int height)
        {
#if WIC
            try
            {
                byte[] viaWic = DecodeWic(path, ref width, ref height);
                if (viaWic != null) return viaWic;
            }
            catch { }
#endif
            try { return DecodeGdi(path, ref width, ref height); }
            catch { return null; }
        }

#if WIC
        /// <summary>Decoded width asked of WIC: a JPEG then stops at 1/8.</summary>
        const int WicWidth = 128;

        /// <summary>
        /// Scaled decoding through the Windows Imaging Component. The header is
        /// read first, for the image's real size - which the keep rules need,
        /// and which the scaled-down bitmap no longer knows - then the pixels
        /// are decoded straight from the same open file. A stream is used
        /// rather than a URI so that a '#' or a '%' in a file name cannot be
        /// mistaken for URI syntax.
        /// </summary>
        static byte[] DecodeWic(string path, ref int width, ref int height)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite, 65536))
            {
                var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(fs,
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation
                    | System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                if (dec.Frames.Count == 0) return null;
                width = dec.Frames[0].PixelWidth;
                height = dec.Frames[0].PixelHeight;
                if (width <= 0 || height <= 0) return null;

                fs.Position = 0;
                var src = new System.Windows.Media.Imaging.BitmapImage();
                src.BeginInit();
                src.StreamSource = fs;
                src.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                src.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile;
                if (width > WicWidth) src.DecodePixelWidth = WicWidth;
                src.EndInit();
                src.Freeze();

                var grey = new System.Windows.Media.Imaging.FormatConvertedBitmap();
                grey.BeginInit();
                grey.Source = src;
                grey.DestinationFormat = System.Windows.Media.PixelFormats.Gray8;
                grey.EndInit();
                grey.Freeze();

                int gw = grey.PixelWidth, gh = grey.PixelHeight;
                if (gw <= 0 || gh <= 0) return null;
                var buffer = new byte[gw * gh];
                grey.CopyPixels(buffer, gw, 0);
                return Resample(buffer, gw, gh, Grid, Grid);
            }
        }
#endif

        /// <summary>Full decoding through GDI+: correct everywhere, slower.</summary>
        static byte[] DecodeGdi(string path, ref int width, ref int height)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite, 65536))
            using (var img = Image.FromStream(fs, false, false))
            {
                width = img.Width;
                height = img.Height;
                if (width <= 0 || height <= 0) return null;

                using (var small = new Bitmap(Grid, Grid, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(img, 0, 0, Grid, Grid);
                    }

                    var data = small.LockBits(new Rectangle(0, 0, Grid, Grid),
                                              ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        var grey = new byte[Grid * Grid];
                        var row = new byte[data.Stride];
                        for (int y = 0; y < Grid; y++)
                        {
                            Marshal.Copy((IntPtr)(data.Scan0.ToInt64() + (long)y * data.Stride),
                                         row, 0, data.Stride);
                            for (int x = 0; x < Grid; x++)
                            {
                                int i = x * 3;
                                // Rec. 601 luma, in integers
                                grey[y * Grid + x] = (byte)((row[i + 2] * 77
                                                           + row[i + 1] * 150
                                                           + row[i] * 29) >> 8);
                            }
                        }
                        return grey;
                    }
                    finally { small.UnlockBits(data); }
                }
            }
        }

        // ---------------- signal ---------------------------------------

        /// <summary>
        /// Box resampling to any size, fractional edges included. Both hashes
        /// and the comparison grid go through it, so every path ends up
        /// comparing grids built exactly the same way.
        /// </summary>
        public static byte[] Resample(byte[] src, int sw, int sh, int dw, int dh)
        {
            var dst = new byte[dw * dh];
            double fx = (double)sw / dw, fy = (double)sh / dh;
            for (int y = 0; y < dh; y++)
            {
                int y0 = (int)(y * fy), y1 = (int)Math.Ceiling((y + 1) * fy);
                if (y1 > sh) y1 = sh;
                if (y1 <= y0) y1 = y0 + 1;
                for (int x = 0; x < dw; x++)
                {
                    int x0 = (int)(x * fx), x1 = (int)Math.Ceiling((x + 1) * fx);
                    if (x1 > sw) x1 = sw;
                    if (x1 <= x0) x1 = x0 + 1;

                    int sum = 0, n = 0;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        int rowBase = yy * sw;
                        for (int xx = x0; xx < x1; xx++) { sum += src[rowBase + xx]; n++; }
                    }
                    dst[y * dw + x] = (byte)(n > 0 ? sum / n : 0);
                }
            }
            return dst;
        }

        /// <summary>One bit per pair of horizontal neighbours on a 9x8 grid.</summary>
        static ulong DHash(byte[] g)
        {
            ulong bits = 0;
            int bit = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++, bit++)
                    if (g[y * 9 + x] > g[y * 9 + x + 1]) bits |= 1UL << bit;
            return bits;
        }

        // Cosine table of the 32-point DCT-II, only the 8 coefficients kept.
        static readonly double[] Cos = BuildCos();

        static double[] BuildCos()
        {
            var t = new double[8 * Grid];
            for (int u = 0; u < 8; u++)
                for (int x = 0; x < Grid; x++)
                    t[u * Grid + x] = Math.Cos((2 * x + 1) * u * Math.PI / (2.0 * Grid));
            return t;
        }

        /// <summary>
        /// The 8x8 low-frequency corner of a 32x32 DCT, thresholded at its
        /// median. Only the coefficients that are kept are worked out - the
        /// separable transform costs about ten thousand operations, nothing
        /// next to decoding the image.
        /// </summary>
        static ulong PHash(byte[] g)
        {
            var rows = new double[Grid * 8];          // DCT along x, 8 columns kept
            for (int y = 0; y < Grid; y++)
            {
                int b = y * Grid;
                for (int u = 0; u < 8; u++)
                {
                    double s = 0;
                    int c = u * Grid;
                    for (int x = 0; x < Grid; x++) s += g[b + x] * Cos[c + x];
                    rows[y * 8 + u] = s;
                }
            }

            var block = new double[64];
            for (int u = 0; u < 8; u++)
            {
                for (int v = 0; v < 8; v++)
                {
                    double s = 0;
                    int c = v * Grid;
                    for (int y = 0; y < Grid; y++) s += rows[y * 8 + u] * Cos[c + y];
                    block[v * 8 + u] = s;
                }
            }

            // The DC term carries the average brightness, which says nothing
            // about the picture, so it is left out of the median.
            var sorted = new double[63];
            Array.Copy(block, 1, sorted, 0, 63);
            Array.Sort(sorted);
            double median = (sorted[30] + sorted[31]) / 2.0;

            ulong bits = 0;
            for (int i = 0; i < 64; i++)
                if (block[i] > median) bits |= 1UL << i;
            return bits;
        }

        static double StdDev(byte[] g)
        {
            double sum = 0, sum2 = 0;
            for (int i = 0; i < g.Length; i++) { sum += g[i]; sum2 += (double)g[i] * g[i]; }
            double mean = sum / g.Length;
            double var = sum2 / g.Length - mean * mean;
            return var > 0 ? Math.Sqrt(var) : 0;
        }

        // ---------------- comparing -------------------------------------

        /// <summary>Number of differing bits, the .NET 4 way (no POPCNT intrinsic).</summary>
        public static int Distance(ulong a, ulong b)
        {
            ulong v = a ^ b;
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }

        /// <summary>
        /// Mean absolute difference between two 8x8 grids, each shifted to its
        /// own average first, so that the same picture exported darker or
        /// lighter still matches while two different flat images do not.
        /// </summary>
        public static double GridDifference(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 255;
            int sa = 0, sb = 0;
            for (int i = 0; i < a.Length; i++) { sa += a[i]; sb += b[i]; }
            double ma = (double)sa / a.Length, mb = (double)sb / b.Length;
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += Math.Abs((a[i] - ma) - (b[i] - mb));
            return sum / a.Length;
        }
    }

    // -----------------------------------------------------------------
    //  Fingerprint cache
    //
    //  Decoding is the whole cost of a visual scan, and the answer for a
    //  file that has not changed is always the same. Keeping it on disk
    //  makes the second scan of a folder almost free, which is the usual
    //  case: people re-scan the same pictures.
    //
    //  A row is trusted only while the file's size and modification time
    //  still match, so an edited image is fingerprinted again.
    // -----------------------------------------------------------------
    public class FingerprintCache
    {
        const int MaxRows = 400000;          // ~60 MB on disk, far beyond any real library

        class Row
        {
            public long Size;
            public long Ticks;
            public long Seen;
            public Fingerprint Fp;
        }

        readonly Dictionary<string, Row> _rows =
            new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        readonly object _lock = new object();
        bool _dirty;

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TwinPix");
                return Path.Combine(dir, "fingerprints.bin");
            }
        }

        /// <summary>Tells a cache written by a different decoder from this one.</summary>
        static int DecoderId
        {
            get
            {
#if WIC
                return 1;
#else
                return 0;
#endif
            }
        }

        public int Count { get { return _rows.Count; } }

        public void Load()
        {
            _rows.Clear();
            _dirty = false;
            try
            {
                string file = FilePath;
                if (!File.Exists(file)) return;
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                                               FileShare.Read, 65536))
                using (var r = new BinaryReader(fs))
                {
                    if (r.ReadInt32() != 0x50465054) return;            // "TPFP"
                    if (r.ReadInt32() != ImageHash.Version) return;     // hashing changed
                    if (r.ReadInt32() != DecoderId) return;             // grids would differ
                    int n = r.ReadInt32();
                    if (n < 0 || n > MaxRows) return;
                    for (int i = 0; i < n; i++)
                    {
                        string path = r.ReadString();
                        var row = new Row();
                        row.Size = r.ReadInt64();
                        row.Ticks = r.ReadInt64();
                        row.Seen = r.ReadInt64();
                        var fp = new Fingerprint();
                        fp.DHash = r.ReadUInt64();
                        fp.PHash = r.ReadUInt64();
                        fp.PixelWidth = r.ReadInt32();
                        fp.PixelHeight = r.ReadInt32();
                        fp.Flat = r.ReadBoolean();
                        fp.Grid = r.ReadBytes(64);
                        if (fp.Grid.Length != 64) return;               // truncated file
                        row.Fp = fp;
                        _rows[path] = row;
                    }
                }
            }
            catch { _rows.Clear(); }        // a damaged cache is just a slower scan
        }

        /// <summary>The stored fingerprint, or null when it is missing or stale.</summary>
        public Fingerprint Get(string path, long size, long ticks)
        {
            lock (_lock)
            {
                Row row;
                if (!_rows.TryGetValue(path, out row)) return null;
                if (row.Size != size || row.Ticks != ticks) return null;
                row.Seen = DateTime.UtcNow.Ticks;
                return row.Fp;
            }
        }

        public void Put(string path, long size, long ticks, Fingerprint fp)
        {
            if (fp == null) return;
            lock (_lock)
            {
                var row = new Row();
                row.Size = size;
                row.Ticks = ticks;
                row.Seen = DateTime.UtcNow.Ticks;
                row.Fp = fp;
                _rows[path] = row;
                _dirty = true;
            }
        }

        public void Save()
        {
            if (!_dirty) return;
            try
            {
                var keys = new List<string>(_rows.Keys);
                if (keys.Count > MaxRows)
                {
                    // keep the most recently used rows
                    keys.Sort(delegate(string a, string b)
                    {
                        return _rows[b].Seen.CompareTo(_rows[a].Seen);
                    });
                    keys.RemoveRange(MaxRows, keys.Count - MaxRows);
                }

                string file = FilePath;
                string dir = Path.GetDirectoryName(file);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // written beside the real file, then swapped in: an interrupted
                // save leaves the previous cache intact
                string tmp = file + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write,
                                               FileShare.None, 65536))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write(0x50465054);
                    w.Write(ImageHash.Version);
                    w.Write(DecoderId);
                    w.Write(keys.Count);
                    foreach (string path in keys)
                    {
                        Row row = _rows[path];
                        w.Write(path);
                        w.Write(row.Size);
                        w.Write(row.Ticks);
                        w.Write(row.Seen);
                        w.Write(row.Fp.DHash);
                        w.Write(row.Fp.PHash);
                        w.Write(row.Fp.PixelWidth);
                        w.Write(row.Fp.PixelHeight);
                        w.Write(row.Fp.Flat);
                        w.Write(row.Fp.Grid, 0, 64);
                    }
                }
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
                _dirty = false;
            }
            catch { }       // never lose a scan over a cache that cannot be written
        }

        public void Clear()
        {
            lock (_lock) { _rows.Clear(); _dirty = true; }
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { }
        }
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

        /// <summary>Reads one file into an entry, or returns null.</summary>
        static FileEntry MakeEntry(string path, ScanOptions o, ScanResult res)
        {
            FileInfo fi;
            try { fi = new FileInfo(path); if (!fi.Exists) return null; }
            catch { res.Errors++; return null; }

            var e = new FileEntry();
            e.FullPath = fi.FullName;
            e.FileName = fi.Name;
            e.Extension = (fi.Extension ?? "").ToLowerInvariant();
            try { e.Size = fi.Length; }
            catch { res.Errors++; return null; }
            try { e.Modified = fi.LastWriteTime; }
            catch { e.Modified = DateTime.MinValue; }
            e.InPreferred = IsUnder(fi.FullName, o.Preferred);
            return e;
        }

        public static ScanResult Scan(ScanOptions o, BackgroundWorker bw)
        {
            var res = new ScanResult();
            var paths = new List<string>();
            Walk(o.Root, o, paths, res, bw);
            res.FilesScanned = paths.Count;

            List<DupGroup> groups = o.Mode == MatchMode.Visual
                                  ? GroupByAppearance(o, res, paths, bw)
                                  : GroupByBytes(o, res, paths, bw);
            if (groups == null) return res;          // cancelled

            foreach (var g in groups)
            {
                g.Files.Sort(delegate(FileEntry a, FileEntry b)
                {
                    return string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase);
                });
                g.Recompute();
                AutoSelect(g, o.Mode == MatchMode.Visual ? KeepRule.BestResolution : DefaultRule,
                           o.Preferred.Length > 0);
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

        // ---------------- matching by bytes ----------------------------

        /// <summary>Same extension and same size, optionally confirmed by MD5.</summary>
        static List<DupGroup> GroupByBytes(ScanOptions o, ScanResult res,
                                           List<string> paths, BackgroundWorker bw)
        {
            // Grouping: extension (lower-cased, so .JPG and .jpg match) | size
            var map = new Dictionary<string, DupGroup>(StringComparer.Ordinal);

            for (int i = 0; i < paths.Count; i++)
            {
                if (bw != null && bw.CancellationPending) return null;
                FileEntry e = MakeEntry(paths[i], o, res);
                if (e == null) continue;

                string key = e.Extension + "|" + e.Size.ToString(CultureInfo.InvariantCulture);
                DupGroup g;
                if (!map.TryGetValue(key, out g)) { g = new DupGroup(); map[key] = g; }
                g.Files.Add(e);

                if (bw != null && (i % 200) == 0)
                    bw.ReportProgress(0, "Grouping: " + (i + 1) + " / " + paths.Count);
            }

            var groups = new List<DupGroup>();
            foreach (var kv in map)
                if (kv.Value.Files.Count > 1) groups.Add(kv.Value);

            if (!o.CompareContent) return groups;

            // Content check: split each group by MD5
            var refined = new List<DupGroup>();
            int done = 0;
            foreach (var g in groups)
            {
                if (bw != null && bw.CancellationPending) return null;
                var byHash = new Dictionary<string, DupGroup>(StringComparer.Ordinal);
                foreach (var f in g.Files)
                {
                    string h;
                    try { h = Md5(f.FullPath); }
                    catch { h = "ERR:" + f.FullPath; res.Errors++; }
                    f.Hash = h;
                    DupGroup sub;
                    if (!byHash.TryGetValue(h, out sub)) { sub = new DupGroup(); byHash[h] = sub; }
                    sub.Files.Add(f);
                }
                foreach (var kv in byHash)
                    if (kv.Value.Files.Count > 1) refined.Add(kv.Value);

                done++;
                if (bw != null)
                    bw.ReportProgress(0, "Checking content: " + done + " / " + groups.Count);
            }
            return refined;
        }

        // ---------------- matching by appearance -----------------------

        /// <summary>Below this, an image carries too little to be compared.</summary>
        const int MinSide = 32;

        /// <summary>
        /// A band holding this many images is a degenerate one - a wall of
        /// identical-looking thumbnails - and comparing it pair by pair would
        /// cost more than it is worth. Its other bands still catch real pairs.
        /// </summary>
        const int MaxBucket = 3000;

        static List<DupGroup> GroupByAppearance(ScanOptions o, ScanResult res,
                                                List<string> paths, BackgroundWorker bw)
        {
            var entries = new List<FileEntry>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                if (bw != null && bw.CancellationPending) return null;
                FileEntry e = MakeEntry(paths[i], o, res);
                if (e != null) entries.Add(e);
            }
            if (entries.Count == 0) return new List<DupGroup>();

            if (!ComputeFingerprints(entries, o, res, bw)) return null;

            // Keep what can meaningfully be compared.
            var usable = new List<FileEntry>(entries.Count);
            foreach (var e in entries)
            {
                if (e.Fp == null) { res.Errors++; continue; }
                if (e.Fp.Flat || e.PixelWidth < MinSide || e.PixelHeight < MinSide)
                {
                    res.Skipped++;
                    continue;
                }
                usable.Add(e);
            }
            if (usable.Count < 2) return new List<DupGroup>();

            if (bw != null) bw.ReportProgress(0, "Comparing " + usable.Count + " image(s)...");
            return Cluster(usable, o.MaxDistance, bw);
        }

        /// <summary>
        /// Fingerprints every image, several at a time. Decoding is what a
        /// visual scan costs, and it is pure computation on independent files,
        /// so it scales with the number of cores. Anything already in the
        /// cache costs nothing at all.
        /// </summary>
        static bool ComputeFingerprints(List<FileEntry> entries, ScanOptions o, ScanResult res,
                                        BackgroundWorker bw)
        {
            int done = 0, hits = 0, computed = 0;
            bool cancelled = false;

            var po = new System.Threading.Tasks.ParallelOptions();
            po.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount);

            System.Threading.Tasks.Parallel.For(0, entries.Count, po,
                delegate(int i, System.Threading.Tasks.ParallelLoopState state)
            {
                if (bw != null && bw.CancellationPending) { cancelled = true; state.Stop(); return; }

                FileEntry e = entries[i];
                long ticks = e.Modified.Ticks;
                Fingerprint fp = o.Cache == null ? null : o.Cache.Get(e.FullPath, e.Size, ticks);
                if (fp != null)
                {
                    System.Threading.Interlocked.Increment(ref hits);
                }
                else
                {
                    fp = ImageHash.Compute(e.FullPath);
                    if (fp != null)
                    {
                        System.Threading.Interlocked.Increment(ref computed);
                        if (o.Cache != null) o.Cache.Put(e.FullPath, e.Size, ticks, fp);
                    }
                }
                e.Fp = fp;

                int n = System.Threading.Interlocked.Increment(ref done);
                if (bw != null && (n % 64) == 0)
                    bw.ReportProgress(0, "Fingerprinting: " + n + " / " + entries.Count);
            });

            res.Fingerprinted = computed;
            res.FromCache = hits;
            return !cancelled;
        }

        /// <summary>
        /// Two fingerprints at most <paramref name="maxD"/> bits apart share at
        /// least one of k = 2^ceil(log2(maxD+1)) bands, because maxD differing
        /// bits cannot touch more than maxD of them. Indexing every band
        /// therefore finds every pair without comparing everything with
        /// everything: what is left is to confirm the candidates.
        /// </summary>
        static List<DupGroup> Cluster(List<FileEntry> items, int maxD, BackgroundWorker bw)
        {
            int k = 2;
            while (k <= maxD) k *= 2;
            if (k > 16) k = 16;
            int bandBits = 64 / k;
            ulong mask = bandBits >= 64 ? ulong.MaxValue : (1UL << bandBits) - 1;

            var parent = new int[items.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            for (int band = 0; band < k; band++)
            {
                if (bw != null && bw.CancellationPending) return null;

                var buckets = new Dictionary<ulong, List<int>>();
                int shift = band * bandBits;
                for (int i = 0; i < items.Count; i++)
                {
                    ulong key = (items[i].Fp.DHash >> shift) & mask;
                    List<int> list;
                    if (!buckets.TryGetValue(key, out list)) { list = new List<int>(); buckets[key] = list; }
                    list.Add(i);
                }

                foreach (var kv in buckets)
                {
                    List<int> list = kv.Value;
                    if (list.Count < 2 || list.Count > MaxBucket) continue;
                    for (int a = 0; a < list.Count; a++)
                    {
                        for (int b = a + 1; b < list.Count; b++)
                        {
                            int ia = list[a], ib = list[b];
                            if (Find(parent, ia) == Find(parent, ib)) continue;   // already together
                            if (SamePicture(items[ia].Fp, items[ib].Fp, maxD))
                                Union(parent, ia, ib);
                        }
                    }
                }

                if (bw != null)
                    bw.ReportProgress(0, "Comparing: band " + (band + 1) + " / " + k);
            }

            // components of two or more files become groups
            var byRoot = new Dictionary<int, DupGroup>();
            for (int i = 0; i < items.Count; i++)
            {
                int root = Find(parent, i);
                DupGroup g;
                if (!byRoot.TryGetValue(root, out g))
                {
                    g = new DupGroup();
                    g.Visual = true;
                    byRoot[root] = g;
                }
                g.Files.Add(items[i]);
            }

            var groups = new List<DupGroup>();
            foreach (var kv in byRoot)
            {
                DupGroup g = kv.Value;
                if (g.Files.Count < 2) continue;

                // The copy with the most pixels is the reference: every other
                // one is shown as so many bits away from it.
                FileEntry reference = g.Files[0];
                foreach (var f in g.Files)
                    if (f.Pixels > reference.Pixels
                        || (f.Pixels == reference.Pixels && f.Size > reference.Size))
                        reference = f;
                foreach (var f in g.Files)
                    f.Distance = ImageHash.Distance(f.Fp.DHash, reference.Fp.DHash);

                groups.Add(g);
            }
            return groups;
        }

        /// <summary>
        /// Confirms a candidate pair. The two hashes agreeing is not enough on
        /// its own: the framing has to match, and the grey grids have to line
        /// up pixel by pixel, which is what tells two genuinely similar
        /// pictures apart from one picture stored twice.
        /// </summary>
        static bool SamePicture(Fingerprint a, Fingerprint b, int maxD)
        {
            if (ImageHash.Distance(a.DHash, b.DHash) > maxD) return false;
            if (ImageHash.Distance(a.PHash, b.PHash) > maxD + 4) return false;

            double ra = a.AspectRatio, rb = b.AspectRatio;
            if (ra <= 0 || rb <= 0) return false;
            double ratio = ra > rb ? ra / rb : rb / ra;
            if (ratio > 1.08) return false;             // re-framed, not re-saved

            return ImageHash.GridDifference(a.Grid, b.Grid) <= 10.0 + maxD;
        }

        static int Find(int[] parent, int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }

        public enum KeepRule { Oldest, Newest, ShortestPath, BestResolution, LargestFile }

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
                case KeepRule.BestResolution:
                    // the most pixels, then the heaviest file: the copy closest
                    // to the original when the same picture exists several times
                    if (a.Pixels != b.Pixels) return a.Pixels > b.Pixels;
                    if (a.Size != b.Size) return a.Size > b.Size;
                    break;
                case KeepRule.LargestFile:
                    if (a.Size != b.Size) return a.Size > b.Size;
                    if (a.Pixels != b.Pixels) return a.Pixels > b.Pixels;
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

        public FileCard(FileEntry e, bool visual, EventHandler onKeepChanged, ToolTip tip)
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
            _lblInfo.AutoEllipsis = true;
            _lblInfo.Text = Util.FormatSize(e.Size)
                          + (e.Dimensions.Length > 0 ? "  -  " + e.Dimensions : "")
                          + "  -  " + e.Modified.ToString("yyyy-MM-dd");
            Controls.Add(_lblInfo);

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
            if (note.Length > 0)
            {
                var star = new Label();
                star.Location = new Point(6, 234);
                star.Size = new Size(206, 20);
                star.AutoEllipsis = true;
                star.ForeColor = e.InPreferred ? KeepBorder : Color.DimGray;
                star.Text = note;
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
                             + (e.Dimensions.Length > 0 ? "\r\n" + e.Dimensions + " pixels" : "")
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
        ComboBox _cboMatch, _cboSensitivity;
        TextBox _txtExt;
        Button _btnRoot, _btnPreferred, _btnQuarantine, _btnScan;
        CheckBox _chkRecursive, _chkPreserveTree, _chkTrash;
        Label _lblQuarantine, _lblSensitivity;

        // Fingerprints survive from one scan to the next, and from one run of
        // the program to the next: a second visual scan decodes almost nothing.
        readonly FingerprintCache _fingerprints = new FingerprintCache();
        bool _cacheLoaded;
        ListView _lv;
        ImageList _fileIcons;
        FlowLayoutPanel _cards;
        Label _lblGroupTitle;
        SplitContainer _split;
        GroupBox _sourceBox, _destBox;
        TableLayoutPanel _sourceGrid, _destGrid;

        MenuStrip _menu;
        ToolStripMenuItem _miScan, _miExport, _miMoveAll,
                          _miKeepPreferred, _miKeepOldest, _miKeepNewest, _miKeepShortest,
                          _miKeepBest, _miKeepLargest;
        ToolStrip _toolbar;
        FlowLayoutPanel _keepBar;
        ToolStripButton _tsScan, _tsMoveAll, _tsExport;
        CheckBox _chkKeepPreferred, _chkKeepOldest, _chkKeepNewest, _chkKeepShortest,
                 _chkKeepBest, _chkKeepLargest;
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

            // Exactly one of these is always ticked.
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
            // The two that matter once copies no longer share a size: visual
            // matching puts a thumbnail and its original in the same group.
            _chkKeepBest = MakeCheck("Best resolution", false);
            _tip.SetToolTip(_chkKeepBest,
                "Of the remaining copies, keep the one with the most pixels,"
                + "\r\nthen the heaviest. The closest thing to the original.");
            _chkKeepLargest = MakeCheck("Largest file", false);
            _tip.SetToolTip(_chkKeepLargest,
                "Of the remaining copies, keep the heaviest file.");
            _keepBar.Controls.Add(_chkKeepOldest);
            _keepBar.Controls.Add(_chkKeepNewest);
            _keepBar.Controls.Add(_chkKeepShortest);
            _keepBar.Controls.Add(_chkKeepBest);
            _keepBar.Controls.Add(_chkKeepLargest);

            _chkKeepPreferred.CheckedChanged += delegate { RuleChanged(null); };
            _chkKeepOldest.CheckedChanged += delegate { RuleChanged(_chkKeepOldest); };
            _chkKeepNewest.CheckedChanged += delegate { RuleChanged(_chkKeepNewest); };
            _chkKeepShortest.CheckedChanged += delegate { RuleChanged(_chkKeepShortest); };
            _chkKeepBest.CheckedChanged += delegate { RuleChanged(_chkKeepBest); };
            _chkKeepLargest.CheckedChanged += delegate { RuleChanged(_chkKeepLargest); };

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
            _chkRecursive.CheckedChanged += delegate { ScanOptionChanged(); };
            opts.Controls.Add(_chkRecursive);

            // What counts as a duplicate. Each entry costs more than the one
            // above it, and finds what the one above it cannot.
            opts.Controls.Add(NewInlineLabel("Matc&hing:"));
            _cboMatch = NewOptionCombo(250);
            _cboMatch.Items.AddRange(new object[] {
                "Name + size (fastest)",
                "Same bytes (MD5)",
                "Same picture (visual)" });
            _cboMatch.SelectedIndex = 0;
            _tip.SetToolTip(_cboMatch,
                "Name + size: groups files of the same size and extension.\r\n"
                + "Same bytes: confirms with an MD5 of the whole file.\r\n"
                + "Same picture: finds the same image again whatever its size,\r\n"
                + "format or quality - resized, re-saved or converted copies.\r\n\r\n"
                + "Changing this searches the folder again.");
            _cboMatch.SelectedIndexChanged += delegate { MatchModeChanged(); };
            opts.Controls.Add(_cboMatch);

            _lblSensitivity = NewInlineLabel("Se&nsitivity:");
            opts.Controls.Add(_lblSensitivity);
            _cboSensitivity = NewOptionCombo(210);
            _cboSensitivity.Items.AddRange(new object[] {
                "Strict - re-saved copies",
                "Normal",
                "Loose - lightly edited" });
            _cboSensitivity.SelectedIndex = 1;
            _tip.SetToolTip(_cboSensitivity,
                "How far apart two pictures may look and still count as the same."
                + "\r\nLoose finds more, and is likelier to put two different"
                + "\r\nphotographs of the same scene in one group."
                + "\r\n\r\nVisual matching only.");
            _cboSensitivity.SelectedIndexChanged += delegate { ScanOptionChanged(); };
            opts.Controls.Add(_cboSensitivity);
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
            _miKeepBest = NewMenuItem("Then keep the &best resolution",
                Keys.None, delegate { _chkKeepBest.Checked = true; });
            _miKeepLargest = NewMenuItem("Then keep the &largest file",
                Keys.None, delegate { _chkKeepLargest.Checked = true; });
            mEdit.DropDownItems.AddRange(new ToolStripItem[] {
                _miKeepPreferred, new ToolStripSeparator(),
                _miKeepOldest, _miKeepNewest, _miKeepShortest,
                _miKeepBest, _miKeepLargest });

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
            _cboSensitivity.Enabled = false;      // visual matching only
            _lblSensitivity.Enabled = false;
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
                "Finds duplicate images - by size and extension, by content, or by "
                + "what the picture actually looks like - then moves the copies you "
                + "do not keep to a folder of your choice or to the Recycle Bin.\r\n\r\n"
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

        /// <summary>A caption sitting on the same line as the control it names.</summary>
        static Label NewInlineLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(14, 8, 2, 0);
            return l;
        }

        static ComboBox NewOptionCombo(int width)
        {
            var c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Width = width;
            c.Margin = new Padding(3, 4, 6, 4);
            return c;
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
            o.Mode = SelectedMode;
            o.MaxDistance = SelectedDistance;
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
                                : "No two images share the same name, size and extension "
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
                _cards.Controls.Add(new FileCard(f, g.Visual, CardKeepChanged, _tip));
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
                if (_cboMatch == null) return MatchMode.NameSize;
                switch (_cboMatch.SelectedIndex)
                {
                    case 1: return MatchMode.Content;
                    case 2: return MatchMode.Visual;
                    default: return MatchMode.NameSize;
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
            if (_cacheLoaded) _fingerprints.Save();
            SavePlacement();
            _history.Add(KeyScan, _cboRoot.Text);
            _history.Add(KeyPreferred, _cboPreferred.Text);
            _history.Add(KeyDestination, _cboQuarantine.Text);
            _history.Save();
            base.OnFormClosing(e);
        }
    }
}
