namespace TwinPix
{
    /// <summary>
    /// What a picture looks like, in a few hundred bytes, whatever its file
    /// size, its dimensions, its format or its JPEG quality:
    /// <list type="bullet">
    /// <item>dHash, 64 bits - a 9x8 grey grid, one bit per "is this pixel
    ///   brighter than the one on its right". Immune to scaling, to
    ///   re-compression and to a change of overall brightness.</item>
    /// <item>pHash, 64 bits - the low frequencies of a 32x32 DCT, thresholded
    ///   at their median. Sturdier, used to confirm.</item>
    /// <item>Grid, 64 bytes - an 8x8 grey grid, kept to compare two candidates
    ///   pixel by pixel and throw out the look-alikes the two hashes agree on
    ///   by accident.</item>
    /// <item>FineGrid, 121 bytes - the same at 11x11, about twice the points,
    ///   used instead of Grid at the Normal sensitivity.</item>
    /// </list>
    /// ImageHash computes it; FingerprintCache keeps it between runs.
    /// </summary>
    public class Fingerprint
    {
        /// <summary>
        /// PHP note: "ulong" is an unsigned 64-bit integer - PHP has no
        /// unsigned integers, which is why hashes in PHP are usually hex strings.
        /// </summary>
        public ulong DHash { get; set; }
        public ulong PHash { get; set; }

        /// <summary>8x8 grey levels.</summary>
        public byte[] Grid { get; set; }

        /// <summary>11x11 grey levels.</summary>
        public byte[] FineGrid { get; set; }

        /// <summary>The image's real size, read from its header.</summary>
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }

        /// <summary>Too uniform to be told apart from another (an empty sky, a black scan).</summary>
        public bool Flat { get; set; }

        /// <summary>
        /// Pictures held in the file: an animated GIF or a multi-page TIFF has
        /// several. Only the first is fingerprinted, so two such files that
        /// merely start alike would look identical: they are never matched.
        /// </summary>
        public int Frames { get; set; }

        public double AspectRatio
        {
            get { return PixelHeight > 0 ? (double)PixelWidth / PixelHeight : 0; }
        }
    }
}
