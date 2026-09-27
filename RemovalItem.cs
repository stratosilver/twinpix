namespace TwinPix
{
    /// <summary>
    /// One duplicate to take away, and the copy that stays in its place. The
    /// pairs are fixed when the user confirms (<see cref="DuplicateRemover.Plan"/>),
    /// so nothing the window does afterwards can change what is removed.
    /// </summary>
    public class RemovalItem
    {
        public RemovalItem(DupGroup group, FileEntry duplicate, FileEntry keptCopy)
        {
            Group = group;
            Duplicate = duplicate;
            KeptCopy = keptCopy;
            Outcome = RemovalOutcome.Pending;
        }

        public DupGroup Group { get; private set; }

        /// <summary>The file to move away.</summary>
        public FileEntry Duplicate { get; private set; }

        /// <summary>The copy that stays - checked again just before the duplicate moves.</summary>
        public FileEntry KeptCopy { get; private set; }

        public RemovalOutcome Outcome { get; set; }

        /// <summary>Why the file was left in place, or the error that stopped the move.</summary>
        public string Detail { get; set; }

        /// <summary>Where the file went (destination folder mode).</summary>
        public string NewPath { get; set; }

        /// <summary>What proved the two files the same, for the journal: "identical bytes", "same picture"...</summary>
        public string Evidence { get; set; }
    }
}
