using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace TwinPix
{
    /// <summary>
    /// Walks the scanned folder and lists the image files a scan should look
    /// at - and, just as importantly, the ones it must not.
    /// </summary>
    /// <remarks>
    /// Left out, each for a reason that matters to the safety of the files:
    /// <list type="bullet">
    /// <item>Junctions and symbolic links to folders. They are other names for
    ///   folders that live elsewhere: following them would show one file under
    ///   two paths (a "duplicate" of itself), reach outside the chosen folder,
    ///   or loop forever. Windows' own "Application Data" junctions are one
    ///   example.</item>
    /// <item>Symbolic links to files, for the same reason.</item>
    /// <item>Protected operating-system folders (Hidden + System, as Explorer
    ///   hides them), and $Recycle.Bin and System Volume Information by name.
    ///   A copy found in the Recycle Bin could be picked as "the one to keep"
    ///   - then the real picture would be moved away, and the kept copy
    ///   destroyed the next time the bin is emptied.</item>
    /// <item>The destination folder of the moves (<see cref="ScanOptions.ExcludedFolders"/>).</item>
    /// <item>Cloud files that are not on this computer (OneDrive "online-only"):
    ///   reading one would download it.</item>
    /// </list>
    /// OneDrive placeholders are reparse points like links, but not "name
    /// surrogates", so OneDrive folders are walked normally.
    /// </remarks>
    public static class FolderWalker
    {
        /// <summary>Folder names never walked, whatever their attributes (FAT drives may not carry them).</summary>
        private static readonly HashSet<string> ProtectedNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "$RECYCLE.BIN", "RECYCLER", "RECYCLED", "System Volume Information"
            };

        private const FileAttributes HiddenSystem = FileAttributes.Hidden | FileAttributes.System;

        /// <summary>
        /// Every image file below <see cref="ScanOptions.Root"/> (or directly in
        /// it, when the scan is not recursive), in no particular order. Fills
        /// the folder list and the counters of <paramref name="result"/>.
        /// Stops early, with what it has, when the worker is cancelled.
        /// </summary>
        public static List<FileInfo> FindImages(ScanOptions options, ScanResult result, BackgroundWorker worker)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in options.ExcludedFolders)
                excluded.Add(PathHelper.NormalizeFolder(folder));

            var found = new List<FileInfo>();
            Walk(new DirectoryInfo(options.Root), options, excluded, found, result, worker);
            return found;
        }

        private static void Walk(DirectoryInfo dir, ScanOptions options, HashSet<string> excluded,
                                 List<FileInfo> found, ScanResult result, BackgroundWorker worker)
        {
            if (worker != null && worker.CancellationPending) return;

            // PHP note: the FileInfo objects of a listing already carry size,
            // dates and attributes, read in the same system call - the
            // equivalent of not calling stat() once more per file.
            FileInfo[] files = null;
            try { files = dir.GetFiles(); }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
                result.Errors++;                        // no access, vanished, path too long...
            }

            int images = 0;
            if (files != null)
            {
                foreach (FileInfo file in files)
                {
                    if (!options.Extensions.Contains(file.Extension)) continue;
                    FileAttributes a;
                    try { a = file.Attributes; }
                    catch (Exception ex)
                    {
                        if (!PathHelper.IsFileSystemError(ex)) throw;
                        result.Errors++;
                        continue;
                    }
                    if (IsOnlineOnly(a)) { result.OnlineOnly++; continue; }
                    if (IsLink(file.FullName, a)) { result.LinksIgnored++; continue; }
                    found.Add(file);
                    images++;
                }
                if (worker != null)
                    worker.ReportProgress(0, "Scanning: " + found.Count + " image(s) found...");
            }
            result.Folders.Add(new ScannedFolder { Path = dir.FullName, Images = images });

            if (!options.Recursive) return;

            DirectoryInfo[] subs = null;
            try { subs = dir.GetDirectories(); }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
                result.Errors++;
            }
            if (subs == null) return;

            foreach (DirectoryInfo sub in subs)
            {
                if (worker != null && worker.CancellationPending) return;
                if (IsLeftOut(sub, excluded, result)) { result.FoldersLeftOut++; continue; }
                Walk(sub, options, excluded, found, result, worker);
            }
        }

        /// <summary>True for a subfolder the walk must not enter (see the class remarks).</summary>
        private static bool IsLeftOut(DirectoryInfo dir, HashSet<string> excluded, ScanResult result)
        {
            FileAttributes a;
            try { a = dir.Attributes; }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
                result.Errors++;
                return true;                        // cannot even read its attributes
            }

            if (IsLink(dir.FullName, a)) return true;
            if ((a & HiddenSystem) == HiddenSystem) return true;
            if (ProtectedNames.Contains(dir.Name)) return true;
            if (excluded.Contains(PathHelper.NormalizeFolder(dir.FullName))) return true;
            return false;
        }

        /// <summary>
        /// True for a cloud file whose content is not on this computer: reading
        /// it would download it (OneDrive "online-only", offline storage).
        /// </summary>
        private static bool IsOnlineOnly(FileAttributes a)
        {
            uint bits = (uint)a;
            return (bits & (NativeMethods.FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
                            | NativeMethods.FILE_ATTRIBUTE_RECALL_ON_OPEN
                            | NativeMethods.FILE_ATTRIBUTE_OFFLINE)) != 0;
        }

        /// <summary>
        /// True for a junction or a symbolic link - a reparse point that names
        /// another file or folder. Other reparse points (OneDrive placeholders,
        /// deduplicated files) are ordinary files and folders to the user.
        /// When the kind of reparse point cannot be read, the answer is "link":
        /// leaving a folder out is always safer than walking it twice.
        /// </summary>
        private static bool IsLink(string path, FileAttributes a)
        {
            if ((a & FileAttributes.ReparsePoint) == 0) return false;
            if (!Native.IsWindows) return true;

            NativeMethods.WIN32_FIND_DATA data;
            IntPtr h = NativeMethods.FindFirstFile(path, out data);
            if (h == NativeMethods.InvalidHandle) return true;
            NativeMethods.FindClose(h);

            if ((data.dwFileAttributes & NativeMethods.FILE_ATTRIBUTE_REPARSE_POINT) == 0) return false;
            return (data.dwReserved0 & NativeMethods.REPARSE_TAG_NAME_SURROGATE_BIT) != 0;   // dwReserved0 = the tag
        }
    }
}
