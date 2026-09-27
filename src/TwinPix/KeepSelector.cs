using System;
using System.IO;

namespace TwinPix
{
    /// <summary>Marks, in a group, the one copy to keep.</summary>
    public static class KeepSelector
    {
        /// <summary>The rule the window starts with in byte matching, and the one a scan applies first.</summary>
        public const KeepRule DefaultRule = KeepRule.ShortestPath;

        /// <summary>
        /// Marks exactly one file of the group as kept and every other one as
        /// not kept. With <paramref name="preferFolder"/>, the files sitting in
        /// a preferred folder come first; <paramref name="rule"/> then decides
        /// between the remaining candidates, and the path breaks any tie, so the
        /// choice never depends on the order of the list.
        /// </summary>
        public static void AutoSelect(DupGroup group, KeepRule rule, bool preferFolder)
        {
            FileEntry best = null;
            foreach (FileEntry f in group.Files)
                if (best == null || IsBetter(f, best, rule, preferFolder)) best = f;

            // PHP note: ReferenceEquals is identity, like === between two PHP
            // objects: "this very object", not "an equal object".
            foreach (FileEntry f in group.Files) f.Keep = ReferenceEquals(f, best);
        }

        /// <summary>True when <paramref name="a"/> should be kept rather than <paramref name="b"/>.</summary>
        private static bool IsBetter(FileEntry a, FileEntry b, KeepRule rule, bool preferFolder)
        {
            if (preferFolder && a.InPreferred != b.InPreferred) return a.InPreferred;

            switch (rule)
            {
                case KeepRule.Oldest:
                    if (a.ModifiedUtc != b.ModifiedUtc) return a.ModifiedUtc < b.ModifiedUtc;
                    break;
                case KeepRule.Newest:
                    if (a.ModifiedUtc != b.ModifiedUtc) return a.ModifiedUtc > b.ModifiedUtc;
                    break;
                case KeepRule.BestResolution:
                    if (a.Pixels != b.Pixels) return a.Pixels > b.Pixels;
                    if (a.Size != b.Size) return a.Size > b.Size;
                    break;
                case KeepRule.LargestFile:
                    if (a.Size != b.Size) return a.Size > b.Size;
                    if (a.Pixels != b.Pixels) return a.Pixels > b.Pixels;
                    break;
                default:    // ShortestPath: fewest folders deep, then shortest path
                    int da = Depth(a.FullPath), db = Depth(b.FullPath);
                    if (da != db) return da < db;
                    if (a.FullPath.Length != b.FullPath.Length) return a.FullPath.Length < b.FullPath.Length;
                    break;
            }
            // PHP note: unlike PHP's switch, a C# case cannot fall through
            // into the next one - each ends with break or return.
            return string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>Number of folder separators in the path.</summary>
        private static int Depth(string path)
        {
            int n = 0;
            foreach (char c in path)
                if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar) n++;
            return n;
        }
    }
}
