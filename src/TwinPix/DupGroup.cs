using System;
using System.Collections.Generic;

namespace TwinPix
{
    /// <summary>
    /// The copies of one image: two files or more that a scan found to be
    /// the same, exactly one of which is marked to be kept.
    /// </summary>
    public class DupGroup
    {
        public DupGroup()
        {
            Extension = "";
            Files = new List<FileEntry>();
        }

        /// <summary>
        /// The copies. PHP note: a List is an object, handed around by
        /// reference - unlike a PHP array, it is never copied behind your back,
        /// so every piece of code holding this group sees the same list.
        /// </summary>
        public List<FileEntry> Files { get; private set; }

        /// <summary>The copies' extension, or "mixed" when they differ (visual matching).</summary>
        public string Extension { get; private set; }

        /// <summary>Size of the largest copy: what the list shows and sorts on.</summary>
        public long Size { get; private set; }

        /// <summary>Size of the smallest copy.</summary>
        public long SizeMin { get; private set; }

        /// <summary>Space freed by keeping only the largest copy.</summary>
        public long Wasted { get; private set; }

        /// <summary>Matched by appearance, so the copies may differ in bytes.</summary>
        public bool Visual { get; set; }

        /// <summary>
        /// Visual matching only: the thresholds the group was built with, so
        /// that each copy can be checked against the kept one with the very
        /// same strictness just before it is removed.
        /// </summary>
        public int MaxDistance { get; set; }

        /// <summary>Visual matching only: compared on the 11x11 grid rather than the 8x8 one.</summary>
        public bool FineCheck { get; set; }

        public bool SizesDiffer { get { return SizeMin != Size; } }

        /// <summary>The copy marked to be kept, or null when none is.</summary>
        public FileEntry Kept
        {
            get
            {
                foreach (FileEntry f in Files)
                    if (f.Keep) return f;
                return null;
            }
        }

        /// <summary>
        /// The name shown for the group: the kept copy's, failing that the
        /// first file's. The copies can all be named differently.
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

        /// <summary>
        /// Refreshes what the group says about itself - needed after a move
        /// takes files out of it, and because the copies of a visual group no
        /// longer share one size or even one extension. Reclaimable space is
        /// counted against the largest copy.
        /// </summary>
        public void Recompute()
        {
            string ext = null;
            long max = 0, min = long.MaxValue, total = 0;
            foreach (FileEntry f in Files)
            {
                if (ext == null) ext = f.Extension;
                else if (!string.Equals(ext, f.Extension, StringComparison.OrdinalIgnoreCase))
                    ext = "mixed";
                if (f.Size > max) max = f.Size;
                if (f.Size < min) min = f.Size;
                total += f.Size;
            }
            Extension = ext ?? "";          // PHP note: ?? works as in PHP 7
            Size = max;
            SizeMin = Files.Count > 0 ? min : 0;
            Wasted = Files.Count > 1 ? total - max : 0;
        }
    }
}
