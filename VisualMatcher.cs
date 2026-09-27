using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace TwinPix
{
    /// <summary>
    /// Groups images that show the same picture, from their fingerprints.
    /// </summary>
    /// <remarks>
    /// A perceptual match is a judgement, not a proof: two photographs of the
    /// same scene taken a second apart can look alike to a hash. Three things
    /// keep that judgement on the safe side:
    /// <list type="bullet">
    /// <item>a pair is accepted only when both hashes, the framing and the grey
    ///   grids all agree (<see cref="SamePicture"/>);</item>
    /// <item>files that share a name in the same folder - IMG_0042.CR2 and
    ///   IMG_0042.JPG - are a RAW+JPEG pair the camera made on purpose, never
    ///   duplicates of each other (<see cref="AreCompanions"/>);</item>
    /// <item>groups are connected components (a thumbnail matching a medium
    ///   copy that matches the original puts the three together), but
    ///   DuplicateRemover only ever removes a copy that matches the kept one
    ///   directly - never through a chain of look-alikes.</item>
    /// </list>
    /// </remarks>
    public static class VisualMatcher
    {
        /// <summary>
        /// A band holding this many images is a degenerate one - a wall of
        /// identical-looking thumbnails - and comparing it pair by pair would
        /// cost more than it is worth. Its other bands still catch real pairs.
        /// </summary>
        private const int MaxBucket = 3000;

        /// <summary>Most the aspect ratios of two copies may differ: beyond, the picture was re-framed.</summary>
        private const double MaxAspectRatio = 1.08;

        /// <summary>
        /// Groups the images whose fingerprints match. Two fingerprints at most
        /// <paramref name="maxDistance"/> bits apart share at least one of
        /// k = 2^ceil(log2(maxDistance+1)) bands, because maxDistance differing
        /// bits cannot touch more than maxDistance of them. Indexing every band
        /// therefore finds every pair without comparing everything with
        /// everything: what is left is to confirm the candidates. Returns null
        /// when the worker is cancelled.
        /// </summary>
        public static List<DupGroup> Cluster(List<FileEntry> items, int maxDistance, bool fine,
                                             BackgroundWorker worker)
        {
            int k = 2;
            while (k <= maxDistance) k *= 2;
            if (k > 16) k = 16;
            int bandBits = 64 / k;
            ulong mask = bandBits >= 64 ? ulong.MaxValue : (1UL << bandBits) - 1;

            // Union-find: parent[i] leads to the representative of i's group.
            var parent = new int[items.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            for (int band = 0; band < k; band++)
            {
                if (worker != null && worker.CancellationPending) return null;

                var buckets = new Dictionary<ulong, List<int>>();
                int shift = band * bandBits;
                for (int i = 0; i < items.Count; i++)
                {
                    ulong key = (items[i].Fp.DHash >> shift) & mask;
                    List<int> list;
                    if (!buckets.TryGetValue(key, out list)) { list = new List<int>(); buckets[key] = list; }
                    list.Add(i);
                }

                foreach (List<int> list in buckets.Values)
                {
                    if (list.Count < 2 || list.Count > MaxBucket) continue;
                    for (int a = 0; a < list.Count; a++)
                    {
                        for (int b = a + 1; b < list.Count; b++)
                        {
                            int ia = list[a], ib = list[b];
                            if (Find(parent, ia) == Find(parent, ib)) continue;   // already together
                            if (SamePicture(items[ia], items[ib], maxDistance, fine))
                                Union(parent, ia, ib);
                        }
                    }
                }

                if (worker != null)
                    worker.ReportProgress(0, "Comparing: band " + (band + 1) + " / " + k);
            }

            // Components of two files or more become groups.
            var byRoot = new Dictionary<int, DupGroup>();
            for (int i = 0; i < items.Count; i++)
            {
                int root = Find(parent, i);
                DupGroup g;
                if (!byRoot.TryGetValue(root, out g))
                {
                    g = new DupGroup();
                    g.Visual = true;
                    g.MaxDistance = maxDistance;
                    g.FineCheck = fine;
                    byRoot[root] = g;
                }
                g.Files.Add(items[i]);
            }

            var groups = new List<DupGroup>();
            foreach (DupGroup g in byRoot.Values)
            {
                if (g.Files.Count < 2) continue;
                SetDistances(g);
                groups.Add(g);
            }
            return groups;
        }

        /// <summary>
        /// The copy with the most pixels is the group's reference: every copy
        /// is shown as so many bits away from it.
        /// </summary>
        public static void SetDistances(DupGroup g)
        {
            FileEntry reference = g.Files[0];
            foreach (FileEntry f in g.Files)
                if (f.Pixels > reference.Pixels
                    || (f.Pixels == reference.Pixels && f.Size > reference.Size))
                    reference = f;
            foreach (FileEntry f in g.Files)
                f.Distance = ImageHash.Distance(f.Fp.DHash, reference.Fp.DHash);
        }

        /// <summary>
        /// True when the two files show the same picture. The hashes agreeing
        /// is not enough on its own: the framing has to match, and the grey
        /// grids have to line up pixel by pixel, which is what tells two
        /// genuinely similar pictures apart from one picture stored twice. With
        /// <paramref name="fine"/> the grids compared are the 11x11 ones, about
        /// twice as many points as the 8x8 ones. Used both to build the groups
        /// and, by DuplicateRemover, to confirm each copy against the kept one.
        /// </summary>
        public static bool SamePicture(FileEntry a, FileEntry b, int maxDistance, bool fine)
        {
            if (a == null || b == null || a.Fp == null || b.Fp == null) return false;
            if (AreCompanions(a, b)) return false;

            Fingerprint fa = a.Fp, fb = b.Fp;
            if (fa.Frames > 1 || fb.Frames > 1) return false;       // only the first picture is known
            if (ImageHash.Distance(fa.DHash, fb.DHash) > maxDistance) return false;
            if (ImageHash.Distance(fa.PHash, fb.PHash) > maxDistance + 4) return false;

            double ra = fa.AspectRatio, rb = fb.AspectRatio;
            if (ra <= 0 || rb <= 0) return false;
            double ratio = ra > rb ? ra / rb : rb / ra;
            if (ratio > MaxAspectRatio) return false;               // re-framed, not re-saved

            byte[] ga = fine ? fa.FineGrid : fa.Grid;
            byte[] gb = fine ? fb.FineGrid : fb.Grid;
            return ImageHash.GridDifference(ga, gb) <= 10.0 + maxDistance;
        }

        /// <summary>
        /// True for two files in the same folder with the same name and
        /// different extensions - IMG_0042.CR2 and IMG_0042.JPG, a RAW+JPEG
        /// pair, or a HEIC and the JPEG exported beside it. They are kept
        /// together on purpose and are never offered as duplicates of each
        /// other.
        /// </summary>
        public static bool AreCompanions(FileEntry a, FileEntry b)
        {
            if (string.Equals(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(a.DirectoryPath, b.DirectoryPath, StringComparison.OrdinalIgnoreCase)) return false;
            return string.Equals(Path.GetFileNameWithoutExtension(a.FileName),
                                 Path.GetFileNameWithoutExtension(b.FileName),
                                 StringComparison.OrdinalIgnoreCase);
        }

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }
    }
}
