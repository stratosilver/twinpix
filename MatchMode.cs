namespace TwinPix
{
    /// <summary>What counts as a duplicate.</summary>
    public enum MatchMode
    {
        /// <summary>
        /// Same extension, same size and same bytes: grouped by MD5, and
        /// compared byte by byte once more before any copy is moved.
        /// </summary>
        Content,

        /// <summary>
        /// The same picture, whatever its size, format or quality. A judgement
        /// rather than a proof - see VisualMatcher.
        /// </summary>
        Visual
    }
}
