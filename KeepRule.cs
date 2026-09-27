namespace TwinPix
{
    /// <summary>
    /// The rule that picks, in every group, the copy that stays. Exactly one
    /// is active at a time (the Keep bar above the list); the "Preferred
    /// folders" box is not a rule but a filter applied before it.
    /// </summary>
    /// <remarks>
    /// PHP note: an enum is a closed set of named constants, checked by the
    /// compiler - like PHP 8.1's enum.
    /// </remarks>
    public enum KeepRule
    {
        /// <summary>The copy with the oldest modification date.</summary>
        Oldest,

        /// <summary>The copy with the newest modification date.</summary>
        Newest,

        /// <summary>The copy closest to the scanned folder: fewest folders deep, then shortest path.</summary>
        ShortestPath,

        /// <summary>The copy with the most pixels, then the heaviest file: the one closest to the original.</summary>
        BestResolution,

        /// <summary>The heaviest file, then the one with the most pixels.</summary>
        LargestFile
    }
}
