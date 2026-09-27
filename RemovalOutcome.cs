namespace TwinPix
{
    /// <summary>What happened to one duplicate the user asked to remove.</summary>
    public enum RemovalOutcome
    {
        /// <summary>Not handled yet - or, in Recycle Bin mode, checked and waiting for the bin.</summary>
        Pending,

        /// <summary>Moved to the destination folder.</summary>
        Moved,

        /// <summary>Sent to the Recycle Bin.</summary>
        Recycled,

        /// <summary>
        /// Left where it is because the last check before removal failed: the
        /// kept copy is gone, the two differ, they are one file under two
        /// names... <see cref="RemovalItem.Detail"/> says why.
        /// </summary>
        LeftInPlace,

        /// <summary>The move itself failed (file in use, access denied...). The file is untouched.</summary>
        Failed
    }
}
