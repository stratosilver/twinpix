using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace TwinPix
{
    /// <summary>
    /// Takes the duplicates away - to a folder, or to the Recycle Bin - after
    /// checking each one again, at the last moment, against the copy that
    /// stays.
    /// </summary>
    /// <remarks>
    /// The one rule of this class: <b>a file is never removed on the strength
    /// of the scan alone.</b> Minutes or days may separate the scan from the
    /// click, and in that time the kept copy may have been moved or edited,
    /// the duplicate may have become a different picture, or the two may turn
    /// out to be one file under two names. So, right before each file moves,
    /// <see cref="WhyNotRemovable"/> checks that
    /// <list type="number">
    /// <item>the kept copy is still there;</item>
    /// <item>the duplicate is still there;</item>
    /// <item>they are two different files, not two names of one file
    ///   (<see cref="FileIdentity"/>);</item>
    /// <item>byte matching: they are identical, compared byte by byte - not
    ///   just by MD5;</item>
    /// <item>visual matching: neither changed since the scan, they are not a
    ///   RAW+JPEG pair, and the duplicate matches the kept copy directly with
    ///   the scan's own strictness - not only through a third look-alike.</item>
    /// </list>
    /// A file that fails any check stays where it is, and the report says why.
    /// Nothing is ever overwritten: moves go to a free name, with File.Move,
    /// which refuses to replace an existing file. Every file moved is written
    /// to the <see cref="RemovalJournal"/>.
    /// </remarks>
    public sealed class DuplicateRemover
    {
        private const int CompareBufferSize = 1 << 16;      // 64 KB

        private readonly RemovalSettings _settings;
        private readonly RemovalJournal _journal;

        public DuplicateRemover(RemovalSettings settings, RemovalJournal journal)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (journal == null) throw new ArgumentNullException("journal");
            _settings = settings;
            _journal = journal;
        }

        /// <summary>
        /// The duplicates of every group, each paired with its group's kept
        /// copy. Groups without a kept copy are skipped - every copy would go.
        /// A path is never listed twice, and never both as a duplicate and as
        /// a kept copy, whatever state the groups are in.
        /// </summary>
        public static List<RemovalItem> Plan(IEnumerable<DupGroup> groups)
        {
            var keptPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DupGroup g in groups)
            {
                FileEntry kept = g.Kept;
                if (kept != null) keptPaths.Add(kept.FullPath);
            }

            var items = new List<RemovalItem>();
            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DupGroup g in groups)
            {
                FileEntry kept = g.Kept;
                if (kept == null) continue;
                foreach (FileEntry f in g.Files)
                {
                    if (ReferenceEquals(f, kept) || keptPaths.Contains(f.FullPath)) continue;
                    if (!listed.Add(f.FullPath)) continue;          // Add() is false when already there
                    items.Add(new RemovalItem(g, f, kept));
                }
            }
            return items;
        }

        /// <summary>
        /// Checks every item and, in folder mode, moves the ones that pass. In
        /// Recycle Bin mode the items that pass stay <see cref="RemovalOutcome.Pending"/>
        /// for <see cref="SendCheckedToRecycleBin"/>, which must run on the
        /// window's thread. Runs on a background thread; stops after the
        /// current file when the worker is cancelled, leaving the rest Pending.
        /// </summary>
        public void Run(IList<RemovalItem> items, BackgroundWorker worker)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (worker != null && worker.CancellationPending) return;
                RemovalItem item = items[i];

                string why = WhyNotRemovable(item);
                if (why != null)
                {
                    item.Outcome = RemovalOutcome.LeftInPlace;
                    item.Detail = why;
                }
                else if (!_settings.ToRecycleBin)
                {
                    MoveToFolder(item);
                }

                if (worker != null)
                    worker.ReportProgress(0, (_settings.ToRecycleBin ? "Checking: " : "Checking and moving: ")
                                             + (i + 1) + " / " + items.Count);
            }
        }

        /// <summary>
        /// Recycle Bin mode, second half, on the window's thread: sends the
        /// items that passed the checks to the bin in one operation (one Undo
        /// in Explorer), then looks which files actually left.
        /// </summary>
        public void SendCheckedToRecycleBin(IWin32Window owner, IList<RemovalItem> items)
        {
            var batch = new List<RemovalItem>();
            var paths = new List<string>();
            foreach (RemovalItem item in items)
            {
                if (item.Outcome != RemovalOutcome.Pending || item.Evidence == null) continue;   // not checked
                batch.Add(item);
                paths.Add(item.Duplicate.FullPath);
            }
            if (batch.Count == 0) return;

            string failure = RecycleBin.Send(owner, paths);
            foreach (RemovalItem item in batch)
            {
                if (File.Exists(item.Duplicate.FullPath))
                {
                    item.Outcome = RemovalOutcome.Failed;
                    item.Detail = failure ?? "still on disk";
                    continue;
                }
                item.Outcome = RemovalOutcome.Recycled;
                _journal.Record("Recycled", item.Duplicate.FullPath, "Recycle Bin",
                                item.KeptCopy.FullPath, item.Evidence);
            }
        }

        /// <summary>
        /// Null when the duplicate may be removed; otherwise why not, in words
        /// shown to the user. Sets <see cref="RemovalItem.Evidence"/> on success.
        /// </summary>
        public static string WhyNotRemovable(RemovalItem item)
        {
            FileEntry dup = item.Duplicate, kept = item.KeptCopy;
            if (kept == null || ReferenceEquals(dup, kept)) return "no copy is marked to be kept";

            try
            {
                var keptNow = new FileInfo(kept.FullPath);
                var dupNow = new FileInfo(dup.FullPath);
                if (!keptNow.Exists) return "the kept copy is no longer there";
                if (!dupNow.Exists) return "no longer there";

                FileIdentity keptId = FileIdentity.Of(kept.FullPath);
                FileIdentity dupId = FileIdentity.Of(dup.FullPath);
                if (!keptId.Readable) return "the kept copy cannot be read";
                if (!dupId.Readable) return "cannot be read (in use?)";
                if (keptId.IsSameFileAs(dupId)) return "same file as the kept copy, under another name (a link)";

                if (!item.Group.Visual)
                {
                    if (!SameBytes(kept.FullPath, dup.FullPath)) return "no longer identical to the kept copy";
                    item.Evidence = "identical bytes";
                    return null;
                }

                if (keptNow.Length != kept.Size || keptNow.LastWriteTimeUtc != kept.ModifiedUtc)
                    return "the kept copy changed since the scan";
                if (dupNow.Length != dup.Size || dupNow.LastWriteTimeUtc != dup.ModifiedUtc)
                    return "changed since the scan";
                if (VisualMatcher.AreCompanions(kept, dup))
                    return "RAW+JPEG pair with the kept copy";
                if (!VisualMatcher.SamePicture(kept, dup, item.Group.MaxDistance, item.Group.FineCheck))
                    return "matches the kept copy only through another copy";
                item.Evidence = "same picture (" + ImageHash.Distance(kept.Fp.DHash, dup.Fp.DHash) + "/64 bits apart)";
                return null;
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
                return "could not be checked: " + ex.Message;
            }
        }

        /// <summary>Folder mode: moves one checked duplicate to a free name in the destination.</summary>
        private void MoveToFolder(RemovalItem item)
        {
            try
            {
                string folder = TargetFolder(item.Duplicate);
                Directory.CreateDirectory(folder);
                string target = PathHelper.FreeFileName(folder, item.Duplicate.FileName);
                if (target == null)
                {
                    item.Outcome = RemovalOutcome.Failed;
                    item.Detail = "no free file name left in " + folder;
                    return;
                }

                // File.Move never replaces an existing file: if one appeared
                // under that name meanwhile, it throws and nothing moves.
                File.Move(item.Duplicate.FullPath, target);
                item.Outcome = RemovalOutcome.Moved;
                item.NewPath = target;
                _journal.Record("Moved", item.Duplicate.FullPath, target, item.KeptCopy.FullPath, item.Evidence);
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex) && !(ex is InvalidOperationException)) throw;
                item.Outcome = RemovalOutcome.Failed;
                item.Detail = ex.Message;
            }
        }

        /// <summary>
        /// The destination folder of one file: the destination itself, or with
        /// "Keep folder structure" the file's own subfolders recreated inside
        /// it. Throws if the result would ever fall outside the destination -
        /// see the Path.Combine trap described in PathHelper.
        /// </summary>
        private string TargetFolder(FileEntry file)
        {
            string destination = _settings.Destination;
            string folder = destination;
            if (_settings.KeepFolderStructure && !string.IsNullOrEmpty(_settings.ScanRoot))
            {
                string relative = PathHelper.RelativePath(file.DirectoryPath, _settings.ScanRoot);
                if (!string.IsNullOrEmpty(relative)) folder = Path.Combine(destination, relative);
            }
            if (!PathHelper.IsSameOrUnder(folder, destination))
                throw new InvalidOperationException("The target folder " + folder + " is outside " + destination + ".");
            return folder;
        }

        /// <summary>True when the two files hold exactly the same bytes.</summary>
        private static bool SameBytes(string pathA, string pathB)
        {
            // FileShare.Read: nobody may write to either file while they are
            // compared, so the answer holds at least until the move.
            using (var a = new FileStream(pathA, FileMode.Open, FileAccess.Read, FileShare.Read, CompareBufferSize))
            using (var b = new FileStream(pathB, FileMode.Open, FileAccess.Read, FileShare.Read, CompareBufferSize))
            {
                if (a.Length != b.Length) return false;
                var bufA = new byte[CompareBufferSize];
                var bufB = new byte[CompareBufferSize];
                while (true)
                {
                    int readA = ReadFully(a, bufA);
                    int readB = ReadFully(b, bufB);
                    if (readA != readB) return false;
                    if (readA == 0) return true;
                    for (int i = 0; i < readA; i++)
                        if (bufA[i] != bufB[i]) return false;
                }
            }
        }

        /// <summary>Fills the buffer as far as the file allows (Read may return less than asked).</summary>
        private static int ReadFully(Stream s, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = s.Read(buffer, total, buffer.Length - total);
                if (n == 0) break;
                total += n;
            }
            return total;
        }
    }
}
