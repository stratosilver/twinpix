using System.Collections.Generic;

namespace TwinPix
{
    /// <summary>What a scan found, and the counters shown in the status bar.</summary>
    public class ScanResult
    {
        public ScanResult()
        {
            Groups = new List<DupGroup>();
            Folders = new List<ScannedFolder>();
        }

        public List<DupGroup> Groups { get; set; }

        /// <summary>Every folder walked, root first.</summary>
        public List<ScannedFolder> Folders { get; private set; }

        /// <summary>Image files found (before any grouping).</summary>
        public int FilesScanned { get; set; }

        /// <summary>Files or folders that could not be read.</summary>
        public int Errors { get; set; }

        /// <summary>Visual mode: images actually decoded this time.</summary>
        public int Fingerprinted { get; set; }

        /// <summary>Visual mode: images whose fingerprint was already known.</summary>
        public int FromCache { get; set; }

        /// <summary>Visual mode: too small, too uniform, or several pictures in one file.</summary>
        public int Skipped { get; set; }

        /// <summary>
        /// Links left aside: symbolic links to files, and second names (hard
        /// links) of a file already in the same group. A link is another name
        /// for a file, not a copy of it - removing it "as a duplicate" could
        /// take the only real file along.
        /// </summary>
        public int LinksIgnored { get; set; }

        /// <summary>
        /// Folders left out: junctions and symbolic links to folders, protected
        /// system folders ($Recycle.Bin, System Volume Information), and the
        /// destination of the moves.
        /// </summary>
        public int FoldersLeftOut { get; set; }

        /// <summary>
        /// Cloud files (OneDrive and the like) that are not on this computer.
        /// Reading them would download each one; they are left out instead.
        /// </summary>
        public int OnlineOnly { get; set; }
    }
}
