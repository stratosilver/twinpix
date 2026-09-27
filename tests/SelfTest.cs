// =====================================================================
//  TwinPix self-test
//
//  Checks, on this very computer, the safety rules of the engine: what a
//  scan leaves out, and what the removal refuses to touch. The parts that
//  depend on Windows itself - hard links, folder junctions, the Recycle
//  Bin settings - can only be tested here, on Windows.
//
//  It builds its own little photo library in a temporary folder, works
//  there, and deletes it at the end. It never looks at your pictures.
//
//  Built and run by selftest.bat (TwinPix.exe itself does not contain it:
//  build.bat only compiles the sources of src\TwinPix\, not those in tests\).
// =====================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TwinPix
{
    internal static class SelfTest
    {
        private static int _passed, _failed, _skipped, _notes;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateHardLinkW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(string newName, string existingName, IntPtr security);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateSymbolicLinkW")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CreateSymbolicLink(string link, string target, int flags);

        private const int SymbolicLinkAllowUnprivileged = 0x2;   // Developer Mode

        [STAThread]
        private static int Main()
        {
            string work = Path.Combine(Path.GetTempPath(),
                                       "TwinPix-selftest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            string root = Path.Combine(work, "photos");
            string dest = Path.Combine(work, "set-aside");
            Console.WriteLine("TwinPix self-test, working in " + work);
            Console.WriteLine();

            try
            {
                Library lib = Library.Build(root);
                ContentScan(lib);
                FolderRemoval(lib, dest);
                LastMomentChecks(lib);
                VisualScan(lib);
                RecycleBinChecks(lib);
            }
            catch (Exception ex)
            {
                Fail("unexpected error: " + ex);
            }
            finally
            {
                try { Directory.Delete(work, true); }
                catch (Exception ex) { Console.WriteLine("(could not delete " + work + ": " + ex.Message + ")"); }
            }

            Console.WriteLine();
            Console.WriteLine(_passed + " passed, " + _failed + " failed, " + _skipped + " skipped"
                              + (_notes > 0 ? ", " + _notes + " note(s)" : ""));
            Console.WriteLine(_failed == 0 ? "ALL SAFETY CHECKS PASSED" : "SOME CHECKS FAILED - do not use this build");
            return _failed == 0 ? 0 : 1;
        }

        // ---------------- the tests ------------------------------------

        private static void ContentScan(Library lib)
        {
            Section("Byte matching: what a scan leaves out");
            ScanResult r = Scanner.Scan(lib.Options(MatchMode.Content, 3), null);
            List<string> grouped = r.Groups.SelectMany(g => g.Files).Select(f => lib.Rel(f.FullPath)).ToList();
            Console.WriteLine("    groups: " + lib.Describe(r.Groups));

            DupGroup photo = r.Groups.FirstOrDefault(g => g.Files.Any(f => f.FullPath == lib.Photo));
            Check(photo != null && photo.Files.Count == 2, "the two real copies of photo1 form one group");
            Check(!grouped.Any(p => p.StartsWith("$RECYCLE.BIN")), "a copy inside $RECYCLE.BIN is never scanned");
            Check(!grouped.Any(p => p.StartsWith("_set-aside")), "the destination folder is never scanned");
            Check(!grouped.Any(p => p.EndsWith("same_size_diff.jpg")), "same size, different bytes: not a duplicate");
            if (lib.JunctionMade)
                Check(!grouped.Any(p => p.StartsWith("junction")), "a folder junction is not followed (no file seen twice)");
            else Skip("folder junction", lib.JunctionNote);
            if (lib.HardLinkMade)
                Check(!r.Groups.Any(g => g.Files.Any(f => f.FileName == "different.jpg")),
                      "two names of one file (hard link) are not offered as duplicates");
            else Skip("hard link", Native.IsWindows ? "could not be created here" : "Windows only");
            if (lib.FileLinkMade)
                Check(!grouped.Any(p => p.EndsWith("link.jpg")), "a symbolic link to a file is ignored");
            else Skip("symbolic link to a file", "needs Developer Mode or an administrator");
            Check(photo != null && photo.Files.Count(f => f.Keep) == 1, "exactly one copy is marked to be kept");
        }

        private static void FolderRemoval(Library lib, string dest)
        {
            Section("Removal to a folder");
            ScanResult r = Scanner.Scan(lib.Options(MatchMode.Content, 3), null);
            DupGroup photo = r.Groups.First(g => g.Files.Any(f => f.FullPath == lib.Photo));
            foreach (FileEntry f in photo.Files) f.Keep = f.FullPath == lib.Photo;

            // An unrelated file already sits where the duplicate would go.
            Directory.CreateDirectory(Path.Combine(dest, "b"));
            File.WriteAllText(Path.Combine(dest, "b", "photo1_copy.jpg"), "not a picture");

            var settings = new RemovalSettings();
            settings.Destination = dest;
            settings.KeepFolderStructure = true;
            settings.ScanRoot = lib.Root;
            List<RemovalItem> plan = DuplicateRemover.Plan(new[] { photo });
            string journalPath = Path.Combine(dest, RemovalJournal.FileName);
            using (RemovalJournal journal = RemovalJournal.Open(journalPath))
                new DuplicateRemover(settings, journal).Run(plan, null);

            RemovalItem item = plan.Single();
            Check(item.Outcome == RemovalOutcome.Moved, "the duplicate was moved (" + item.Outcome + " " + item.Detail + ")");
            Check(File.Exists(lib.Photo), "the kept copy is still in place");
            Check(item.NewPath != null && Path.GetFileName(item.NewPath) == "photo1_copy (1).jpg",
                  "a free name was used: " + (item.NewPath == null ? "-" : Path.GetFileName(item.NewPath)));
            Check(File.ReadAllText(Path.Combine(dest, "b", "photo1_copy.jpg")) == "not a picture",
                  "the file already in the destination was not overwritten");
            string[] lines = File.ReadAllLines(journalPath);
            Check(lines.Length == 2 && lines[1].Contains(";Moved;"), "the move is written to the journal");
        }

        private static void LastMomentChecks(Library lib)
        {
            Section("Checks made just before a file moves");

            string keep = lib.Copy("checks\\keep.jpg"), dup = lib.Copy("checks\\dup.jpg");
            DupGroup g = Group(false, keep, dup);
            File.Delete(keep);
            Check(Refusal(g) == "the kept copy is no longer there", "kept copy deleted after the scan: " + Refusal(g));

            lib.Copy("checks\\keep.jpg");
            g = Group(false, keep, dup);
            using (var fs = new FileStream(dup, FileMode.Open, FileAccess.ReadWrite)) { fs.Position = 500; fs.WriteByte(0x42); }
            Check(Refusal(g) == "no longer identical to the kept copy", "duplicate edited after the scan: " + Refusal(g));

            if (lib.HardLinkMade)
            {
                DupGroup linked = Group(false, lib.Path("c\\different.jpg"), lib.Path("c\\different_hardlink.jpg"));
                Check(Refusal(linked) == "same file as the kept copy, under another name (a link)",
                      "a hard link of the kept copy is refused: " + Refusal(linked));
            }
            else Skip("hard link refusal", Native.IsWindows ? "could not be created here" : "Windows only");

            DupGroup none = Group(false, keep, dup);
            foreach (FileEntry f in none.Files) f.Keep = false;
            Check(DuplicateRemover.Plan(new[] { none }).Count == 0, "a group without a kept copy is left alone");
        }

        private static void VisualScan(Library lib)
        {
            Section("Visual matching");
            ScanResult r = Scanner.Scan(lib.Options(MatchMode.Visual, 6), null);
            Console.WriteLine("    groups: " + lib.Describe(r.Groups));
            List<string> grouped = r.Groups.SelectMany(g => g.Files).Select(f => lib.Rel(f.FullPath)).ToList();
            Expect(grouped.Any(p => p.EndsWith("photo1_small.jpg")), "a resized copy is found");
            Check(!r.Groups.Any(g => g.Files.Any(f => f.FileName == "IMG_0042.jpg") && g.Files.Any(f => f.FileName == "IMG_0042.png")),
                  "a RAW+JPEG-style pair (same name, same folder) is never grouped");
            if (lib.TiffMade)
                Check(!grouped.Any(p => p.EndsWith(".tif")), "multi-page files that merely start alike are never grouped");
            else Skip("multi-page TIFF", "could not be written here");
            Check(!grouped.Any(p => p.Contains("black")), "plain black images are never grouped");
        }

        private static void RecycleBinChecks(Library lib)
        {
            Section("Recycle Bin guard");
            if (!Native.IsWindows) { Skip("Recycle Bin", "Windows only"); return; }

            string why = RecycleBin.WhyNotAvailable(new[] { lib.Photo });
            Console.WriteLine("    local file: " + (why ?? "the Recycle Bin can be used"));
            Check(RecycleBin.WhyNotAvailable(new[] { @"\\localhost\C$\TwinPix-selftest.jpg" }) != null,
                  "a network path is refused");
            Check(RecycleBin.WhyNotAvailable(new[] { "photo.jpg" }) != null, "a relative path is refused");
            Check(RecycleBin.WhyNotAvailable(new[] { @"C:\" + new string('x', 270) + ".jpg" }) != null,
                  "a path too long for the Recycle Bin is refused");
        }

        // ---------------- helpers --------------------------------------

        private static string Refusal(DupGroup g)
        {
            List<RemovalItem> plan = DuplicateRemover.Plan(new[] { g });
            return plan.Count == 0 ? "(nothing planned)" : (DuplicateRemover.WhyNotRemovable(plan[0]) ?? "(allowed)");
        }

        private static DupGroup Group(bool visual, params string[] paths)
        {
            var g = new DupGroup();
            g.Visual = visual;
            foreach (string p in paths) g.Files.Add(FileEntry.FromFile(new FileInfo(p)));
            g.Files[0].Keep = true;
            g.Recompute();
            return g;
        }

        private static void Section(string title) { Console.WriteLine(); Console.WriteLine(title); }

        private static void Check(bool ok, string what)
        {
            if (ok) { _passed++; Console.WriteLine("  ok    " + what); }
            else Fail(what);
        }

        private static void Fail(string what) { _failed++; Console.WriteLine("  FAIL  " + what); }

        /// <summary>
        /// A detection-quality expectation, not a safety rule: when it is not
        /// met, a duplicate is merely missed, so it is reported without failing
        /// the run.
        /// </summary>
        private static void Expect(bool ok, string what)
        {
            if (ok) { _passed++; Console.WriteLine("  ok    " + what); }
            else { _notes++; Console.WriteLine("  note  " + what + " - not met (a missed duplicate, not a safety issue)"); }
        }

        private static void Skip(string what, string why) { _skipped++; Console.WriteLine("  skip  " + what + " (" + why + ")"); }

        /// <summary>The temporary photo library the tests work on.</summary>
        private sealed class Library
        {
            public string Root { get; private set; }
            public string Photo { get { return Path("a\\photo1.jpg"); } }
            public bool JunctionMade { get; private set; }
            public string JunctionNote { get; private set; }
            public bool HardLinkMade { get; private set; }
            public bool FileLinkMade { get; private set; }
            public bool TiffMade { get; private set; }

            public static Library Build(string root)
            {
                var lib = new Library();
                lib.Root = root;
                using (Bitmap a = Picture(1, 800, 600))
                using (Bitmap b = Picture(2, 800, 600))
                using (Bitmap c = Picture(3, 800, 600))
                {
                    Save(a, lib.Path("a\\photo1.jpg"), ImageFormat.Jpeg);
                    lib.Copy("b\\photo1_copy.jpg");
                    using (var small = new Bitmap(a, 640, 480)) Save(small, lib.Path("b\\photo1_small.jpg"), ImageFormat.Jpeg);
                    Save(b, lib.Path("c\\different.jpg"), ImageFormat.Jpeg);
                    Save(c, lib.Path("d\\IMG_0042.jpg"), ImageFormat.Jpeg);
                    Save(c, lib.Path("d\\IMG_0042.png"), ImageFormat.Png);
                }

                byte[] bytes = File.ReadAllBytes(lib.Photo);
                bytes[bytes.Length / 2] ^= 1;
                File.WriteAllBytes(lib.Path("c\\same_size_diff.jpg"), bytes);

                using (var black = new Bitmap(640, 480))
                {
                    using (Graphics gr = Graphics.FromImage(black)) gr.Clear(Color.Black);
                    Save(black, lib.Path("f\\black1.jpg"), ImageFormat.Jpeg);
                    Save(black, lib.Path("f\\black2.png"), ImageFormat.Png);
                }

                lib.TiffMade = TwoPageTiff(lib.Path("e\\scan1.tif"), 4, 5) && TwoPageTiff(lib.Path("e\\scan2.tif"), 4, 6);

                lib.Copy("$RECYCLE.BIN\\$R123.jpg");
                lib.Copy("_set-aside\\photo1.jpg");
                lib.MakeLinks();
                return lib;
            }

            public string Path(string relative)
            {
                string p = System.IO.Path.Combine(Root, relative.Replace('\\', System.IO.Path.DirectorySeparatorChar));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p));
                return p;
            }

            /// <summary>Copies photo1.jpg to a new place; returns the new path.</summary>
            public string Copy(string relative)
            {
                string p = Path(relative);
                File.Copy(Photo, p, true);
                return p;
            }

            public string Rel(string full) { return full.Substring(Root.Length + 1); }

            public string Describe(List<DupGroup> groups)
            {
                return string.Join("  ", groups.Select(g => "{" + string.Join(", ", g.Files.Select(f => Rel(f.FullPath) + (f.Keep ? "*" : ""))) + "}"));
            }

            public ScanOptions Options(MatchMode mode, int maxDistance)
            {
                var o = new ScanOptions();
                o.Root = Root;
                o.Mode = mode;
                o.MaxDistance = maxDistance;
                o.Cache = new FingerprintCache();
                foreach (string e in new[] { ".jpg", ".png", ".tif" }) o.Extensions.Add(e);
                o.ExcludedFolders.Add(Path("_set-aside"));
                return o;
            }

            private void MakeLinks()
            {
                string junction = System.IO.Path.Combine(Root, "junction");
                if (Native.IsWindows)
                {
                    HardLinkMade = CreateHardLink(Path("c\\different_hardlink.jpg"), Path("c\\different.jpg"), IntPtr.Zero);
                    FileLinkMade = CreateSymbolicLink(Path("b\\link.jpg"), Photo, SymbolicLinkAllowUnprivileged);
                    JunctionMade = Run("cmd.exe", "/c mklink /J \"" + junction + "\" \"" + Path("a") + "\"");
                }
                else
                {
                    // Telling two names of one file apart needs Windows' file
                    // index (FileIdentity): no hard link is made elsewhere.
                    FileLinkMade = Run("ln", "-s \"" + Photo + "\" \"" + Path("b/link.jpg") + "\"");
                    JunctionMade = Run("ln", "-s \"" + Path("a") + "\" \"" + junction + "\"");
                }
                JunctionNote = JunctionMade ? "" : "mklink /J failed";
            }

            private static bool Run(string exe, string args)
            {
                try
                {
                    var psi = new ProcessStartInfo(exe, args);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit(10000);
                        return p.HasExited && p.ExitCode == 0;
                    }
                }
                catch (Exception) { return false; }
            }

            private static Bitmap Picture(int seed, int w, int h)
            {
                var bmp = new Bitmap(w, h);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(20 + seed * 30 % 200, 60, 120));
                    for (int i = 0; i < 12; i++)
                    {
                        int x = (i * 97 + seed * 53) % w, y = (i * 61 + seed * 29) % h;
                        using (var b1 = new SolidBrush(Color.FromArgb((i * 40 + seed * 70) % 256, (i * 90) % 256, (255 - i * 20 + 256) % 256)))
                            g.FillEllipse(b1, x, y, 120 + seed * 7, 90);
                        using (var b2 = new SolidBrush(Color.FromArgb((seed * 90 + i * 10) % 256, 200, (i * 33) % 256)))
                            g.FillRectangle(b2, w - x - 60, h - y - 40, 60, 40);
                    }
                }
                return bmp;
            }

            private static void Save(Image img, string path, ImageFormat format)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                img.Save(path, format);
            }

            /// <summary>A two-page TIFF whose first page is picture <paramref name="first"/>.</summary>
            private static bool TwoPageTiff(string path, int first, int second)
            {
                try
                {
                    ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/tiff");
                    using (Bitmap p1 = Picture(first, 320, 240))
                    using (Bitmap p2 = Picture(second, 320, 240))
                    using (var ps = new EncoderParameters(1))
                    {
                        ps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
                        p1.Save(path, codec, ps);
                        ps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionPage);
                        p1.SaveAdd(p2, ps);
                        ps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
                        p1.SaveAdd(ps);
                    }
                    using (Image check = Image.FromFile(path))
                        return check.GetFrameCount(FrameDimension.Page) == 2;
                }
                catch (Exception) { return false; }
            }
        }
    }
}
