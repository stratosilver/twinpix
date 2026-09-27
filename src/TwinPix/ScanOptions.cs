using System;
using System.Collections.Generic;

namespace TwinPix
{
    /// <summary>What the user asked a scan to do. Filled by the window, read by the scanner.</summary>
    public class ScanOptions
    {
        public ScanOptions()
        {
            Root = "";
            Recursive = true;
            Mode = MatchMode.Content;
            MaxDistance = 6;
            Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExcludedFolders = new List<string>();
        }

        /// <summary>The folder to scan.</summary>
        public string Root { get; set; }

        /// <summary>Also look inside the subfolders.</summary>
        public bool Recursive { get; set; }

        public MatchMode Mode { get; set; }

        /// <summary>Visual mode only: bits out of 64 two fingerprints may differ by.</summary>
        public int MaxDistance { get; set; }

        /// <summary>
        /// Visual mode only: candidates are confirmed on the 11x11 grid
        /// (121 points) instead of the 8x8 one (64 points).
        /// </summary>
        public bool FineCheck { get; set; }

        /// <summary>Visual mode only. Shared with the window, so it survives from one scan to the next.</summary>
        public FingerprintCache Cache { get; set; }

        /// <summary>
        /// Extensions that count as images, dot included, any case.
        /// PHP note: a HashSet is a set of unique values with a fast Contains -
        /// what array_flip() + isset() gives you in PHP.
        /// </summary>
        public HashSet<string> Extensions { get; private set; }

        /// <summary>
        /// Folders left out of the walk, with everything below them: the
        /// destination of the moves, so that copies already set aside are
        /// never scanned again - where a keep rule could pick one of them as
        /// "the" copy to keep and send the original after it.
        /// </summary>
        public List<string> ExcludedFolders { get; private set; }
    }
}
