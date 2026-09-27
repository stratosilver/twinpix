using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TwinPix
{
    /// <summary>
    /// Runs a scan: lists the images (FolderWalker), groups the ones that are
    /// the same (by bytes, or by appearance through VisualMatcher), sets the
    /// second names of one file aside, and marks the copy to keep in each
    /// group (KeepSelector).
    /// </summary>
    /// <remarks>
    /// Runs on a background thread (see MainForm.StartScan), so it never
    /// touches the window; progress goes through
    /// <see cref="BackgroundWorker.ReportProgress(int, object)"/>, and it
    /// stops early when the worker is cancelled.
    /// </remarks>
    public static class Scanner
    {
        /// <summary>Below this, an image carries too little to be compared.</summary>
        private const int MinSide = 32;

        /// <summary>
        /// Scans <see cref="ScanOptions.Root"/>. The groups come back sorted by
        /// reclaimable space, each with its copy to keep already marked.
        /// </summary>
        public static ScanResult Scan(ScanOptions options, BackgroundWorker worker)
        {
            var result = new ScanResult();
            List<FileInfo> files = FolderWalker.FindImages(options, result, worker);
            result.FilesScanned = files.Count;

            List<FileEntry> entries = ReadEntries(files, result, worker);
            if (entries == null) return result;                        // cancelled

            List<DupGroup> groups = options.Mode == MatchMode.Visual
                                  ? GroupByAppearance(entries, options, result, worker)
                                  : GroupByBytes(entries, result, worker);
            if (groups == null) return result;                         // cancelled

            groups = DropSecondNames(groups, result);

            foreach (DupGroup g in groups)
            {
                g.Files.Sort((a, b) => string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase));
                g.Recompute();
                if (g.Visual) VisualMatcher.SetDistances(g);
                // the preferred folders are applied by the window, once the scan is back
                KeepSelector.AutoSelect(g, options.Mode == MatchMode.Visual
                                           ? KeepRule.BestResolution : KeepSelector.DefaultRule, false);
            }

            groups.Sort((a, b) =>
            {
                int c = b.Wasted.CompareTo(a.Wasted);
                if (c != 0) return c;
                c = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return a.Size.CompareTo(b.Size);
            });

            result.Groups = groups;
            return result;
        }

        /// <summary>One entry per file; null when cancelled.</summary>
        private static List<FileEntry> ReadEntries(List<FileInfo> files, ScanResult result, BackgroundWorker worker)
        {
            var entries = new List<FileEntry>(files.Count);
            foreach (FileInfo file in files)
            {
                if (Cancelled(worker)) return null;
                try { entries.Add(FileEntry.FromFile(file)); }
                catch (Exception ex)
                {
                    if (!PathHelper.IsFileSystemError(ex)) throw;
                    result.Errors++;                 // vanished since the listing
                }
            }
            return entries;
        }

        // ---------------- matching by bytes ----------------------------

        /// <summary>
        /// Same extension and same size, then same MD5. MD5 only sorts the
        /// candidates here: before a copy is moved, DuplicateRemover compares
        /// it with the kept one byte by byte, so even an MD5 collision could
        /// not cost a file. Returns null when cancelled.
        /// </summary>
        private static List<DupGroup> GroupByBytes(List<FileEntry> entries, ScanResult result, BackgroundWorker worker)
        {
            // PHP note: a Dictionary is PHP's associative array, with typed keys
            // and values. Key: extension (lower case, so .JPG and .jpg match) | size.
            var bySize = new Dictionary<string, DupGroup>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                if (Cancelled(worker)) return null;
                FileEntry e = entries[i];
                string key = e.Extension + "|" + e.Size.ToString(CultureInfo.InvariantCulture);
                DupGroup g;
                if (!bySize.TryGetValue(key, out g)) { g = new DupGroup(); bySize[key] = g; }
                g.Files.Add(e);

                if (worker != null && (i % 200) == 0)
                    worker.ReportProgress(0, "Grouping: " + (i + 1) + " / " + entries.Count);
            }

            var candidates = new List<DupGroup>();
            foreach (DupGroup g in bySize.Values)
                if (g.Files.Count > 1) candidates.Add(g);

            // Split each candidate group by content.
            var groups = new List<DupGroup>();
            int done = 0;
            foreach (DupGroup g in candidates)
            {
                if (Cancelled(worker)) return null;
                var byHash = new Dictionary<string, DupGroup>(StringComparer.Ordinal);
                foreach (FileEntry f in g.Files)
                {
                    string hash;
                    try { hash = Md5(f.FullPath); }
                    catch (Exception ex)
                    {
                        if (!PathHelper.IsFileSystemError(ex)) throw;
                        hash = null;
                        result.Errors++;
                    }
                    if (hash == null) continue;             // unreadable: never grouped with anything
                    f.Hash = hash;
                    DupGroup sub;
                    if (!byHash.TryGetValue(hash, out sub)) { sub = new DupGroup(); byHash[hash] = sub; }
                    sub.Files.Add(f);
                }
                foreach (DupGroup sub in byHash.Values)
                    if (sub.Files.Count > 1) groups.Add(sub);

                done++;
                if (worker != null)
                    worker.ReportProgress(0, "Checking content: " + done + " / " + candidates.Count);
            }
            return groups;
        }

        /// <summary>MD5 of a file's content, as 32 hexadecimal digits (PHP's md5_file()).</summary>
        public static string Md5(string path)
        {
            using (var md5 = MD5.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            {
                byte[] h = md5.ComputeHash(fs);
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        // ---------------- matching by appearance -----------------------

        /// <summary>Fingerprints every image and groups the ones that match. Null when cancelled.</summary>
        private static List<DupGroup> GroupByAppearance(List<FileEntry> entries, ScanOptions options,
                                                        ScanResult result, BackgroundWorker worker)
        {
            if (entries.Count == 0) return new List<DupGroup>();
            if (!ComputeFingerprints(entries, options.Cache, result, worker)) return null;

            // Keep what can meaningfully be compared.
            var usable = new List<FileEntry>(entries.Count);
            foreach (FileEntry e in entries)
            {
                if (e.Fp == null) { result.Errors++; continue; }             // could not be decoded
                if (e.Fp.Flat || e.Fp.Frames > 1
                    || e.PixelWidth < MinSide || e.PixelHeight < MinSide)
                {
                    result.Skipped++;
                    continue;
                }
                usable.Add(e);
            }
            if (usable.Count < 2) return new List<DupGroup>();

            if (worker != null) worker.ReportProgress(0, "Comparing " + usable.Count + " image(s)...");
            return VisualMatcher.Cluster(usable, options.MaxDistance, options.FineCheck, worker);
        }

        /// <summary>
        /// Fingerprints every image, several at a time. Decoding is what a
        /// visual scan costs, and it is pure computation on independent files,
        /// so it scales with the number of cores. Anything already in the cache
        /// costs nothing at all. Returns false when cancelled.
        /// </summary>
        /// <remarks>
        /// PHP note: Parallel.For runs the loop body on several threads at once.
        /// The counters shared between them are updated with Interlocked, which
        /// makes "count++" atomic; each entry is written by one thread only.
        /// </remarks>
        private static bool ComputeFingerprints(List<FileEntry> entries, FingerprintCache cache,
                                                ScanResult result, BackgroundWorker worker)
        {
            int done = 0, hits = 0, computed = 0;
            int cancelled = 0;

            var po = new ParallelOptions();
            po.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount);

            Parallel.For(0, entries.Count, po, (i, state) =>
            {
                if (Cancelled(worker)) { Interlocked.Exchange(ref cancelled, 1); state.Stop(); return; }

                FileEntry e = entries[i];
                Fingerprint fp = cache == null ? null : cache.Get(e.FullPath, e.Size, e.ModifiedUtc);
                if (fp != null)
                {
                    Interlocked.Increment(ref hits);
                }
                else
                {
                    fp = ImageHash.Compute(e.FullPath);
                    if (fp != null)
                    {
                        Interlocked.Increment(ref computed);
                        if (cache != null) cache.Put(e.FullPath, e.Size, e.ModifiedUtc, fp);
                    }
                }
                e.Fp = fp;

                int n = Interlocked.Increment(ref done);
                if (worker != null && (n % 64) == 0)
                    worker.ReportProgress(0, "Fingerprinting: " + n + " / " + entries.Count);
            });

            result.Fingerprinted = computed;
            result.FromCache = hits;
            return cancelled == 0;
        }

        // ---------------- second names of one file ---------------------

        /// <summary>
        /// Takes out of every group the extra names of a file already in it -
        /// hard links, mostly: two names, one file, so identical by definition
        /// and yet not a copy. Groups left with a single file disappear. The
        /// removal checks look again just before moving anything; this only
        /// keeps such pairs from being shown as duplicates in the first place.
        /// </summary>
        private static List<DupGroup> DropSecondNames(List<DupGroup> groups, ScanResult result)
        {
            var kept = new List<DupGroup>(groups.Count);
            foreach (DupGroup g in groups)
            {
                var seen = new List<FileIdentity>();
                var distinct = new List<FileEntry>(g.Files.Count);
                foreach (FileEntry f in g.Files)
                {
                    FileIdentity id = FileIdentity.Of(f.FullPath);
                    bool again = false;
                    foreach (FileIdentity other in seen)
                        if (id.IsSameFileAs(other)) { again = true; break; }
                    if (again) { result.LinksIgnored++; continue; }
                    seen.Add(id);
                    distinct.Add(f);
                }
                if (distinct.Count < 2) continue;
                if (distinct.Count != g.Files.Count)
                {
                    g.Files.Clear();
                    g.Files.AddRange(distinct);
                }
                kept.Add(g);
            }
            return kept;
        }

        private static bool Cancelled(BackgroundWorker worker)
        {
            return worker != null && worker.CancellationPending;
        }
    }
}
