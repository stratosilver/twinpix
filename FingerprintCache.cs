using System;
using System.Collections.Generic;
using System.IO;

namespace TwinPix
{
    /// <summary>
    /// Keeps fingerprints on disk between runs, in
    /// %APPDATA%\TwinPix\fingerprints.bin.
    /// </summary>
    /// <remarks>
    /// Decoding is the whole cost of a visual scan, and the answer for a file
    /// that has not changed is always the same: keeping it makes the second
    /// scan of a folder almost free. A row is trusted only while the file's
    /// size and modification time (UTC) still match, so an edited image is
    /// fingerprinted again.
    ///
    /// PHP note: Get() and Put() are called from several threads at once (the
    /// scan decodes on every core), so every access to the dictionary goes
    /// through lock(_lock) - a mutex: one thread at a time inside the block.
    /// A PHP request never shares memory with another thread, so it never
    /// needs this.
    /// </remarks>
    public class FingerprintCache
    {
        /// <summary>At most this many rows are kept (~110 MB on disk), the most recently used ones.</summary>
        private const int MaxRows = 400000;

        /// <summary>"TPFP" read as a little-endian integer: marks the file as ours.</summary>
        private const int Magic = 0x50465054;

        private class Row
        {
            public long Size;
            public long UtcTicks;
            public long Seen;             // last time the row was used, for trimming
            public Fingerprint Fp;
        }

        private readonly Dictionary<string, Row> _rows =
            new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private bool _dirty;

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TwinPix");
                return Path.Combine(dir, "fingerprints.bin");
            }
        }

        /// <summary>Tells a cache written by a different decoder from this one: the grids would differ.</summary>
        private static int DecoderId
        {
            get
            {
#if WIC
                return 1;
#else
                return 0;
#endif
            }
        }

        /// <summary>
        /// Reads the cache from disk. A missing, older or damaged file just
        /// leaves the cache empty: the only cost is a slower scan.
        /// </summary>
        public void Load()
        {
            var loaded = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string file = FilePath;
                if (File.Exists(file))
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                    using (var r = new BinaryReader(fs))
                        ReadRows(r, loaded);
                }
            }
            catch
            {
                // Whatever the damage - truncated, garbled, unreadable - a bad
                // cache is only a slower scan, never a reason to stop.
                loaded.Clear();
            }

            lock (_lock)
            {
                _rows.Clear();
                foreach (KeyValuePair<string, Row> kv in loaded) _rows[kv.Key] = kv.Value;
                _dirty = false;
            }
        }

        /// <summary>Fills <paramref name="into"/>; leaves it empty when the file is not a current cache.</summary>
        private static void ReadRows(BinaryReader r, Dictionary<string, Row> into)
        {
            if (r.ReadInt32() != Magic) return;
            if (r.ReadInt32() != ImageHash.Version) return;     // hashing changed
            if (r.ReadInt32() != DecoderId) return;             // grids would differ
            int n = r.ReadInt32();
            if (n < 0 || n > MaxRows) return;

            for (int i = 0; i < n; i++)
            {
                string path = r.ReadString();
                var row = new Row();
                row.Size = r.ReadInt64();
                row.UtcTicks = r.ReadInt64();
                row.Seen = r.ReadInt64();
                var fp = new Fingerprint();
                fp.DHash = r.ReadUInt64();
                fp.PHash = r.ReadUInt64();
                fp.PixelWidth = r.ReadInt32();
                fp.PixelHeight = r.ReadInt32();
                fp.Frames = r.ReadInt32();
                fp.Flat = r.ReadBoolean();
                fp.Grid = r.ReadBytes(ImageHash.CoarseLength);
                fp.FineGrid = r.ReadBytes(ImageHash.FineLength);
                if (fp.Grid.Length != ImageHash.CoarseLength || fp.FineGrid.Length != ImageHash.FineLength)
                {
                    into.Clear();                                // truncated file
                    return;
                }
                row.Fp = fp;
                into[path] = row;
            }
        }

        /// <summary>The stored fingerprint, or null when it is missing or stale.</summary>
        public Fingerprint Get(string path, long size, DateTime modifiedUtc)
        {
            lock (_lock)
            {
                Row row;
                if (!_rows.TryGetValue(path, out row)) return null;
                if (row.Size != size || row.UtcTicks != modifiedUtc.Ticks) return null;
                row.Seen = DateTime.UtcNow.Ticks;
                return row.Fp;
            }
        }

        public void Put(string path, long size, DateTime modifiedUtc, Fingerprint fp)
        {
            if (fp == null) return;
            var row = new Row();
            row.Size = size;
            row.UtcTicks = modifiedUtc.Ticks;
            row.Seen = DateTime.UtcNow.Ticks;
            row.Fp = fp;
            lock (_lock)
            {
                _rows[path] = row;
                _dirty = true;
            }
        }

        /// <summary>
        /// Writes the cache to disk if it changed. The file is written beside
        /// the real one and then swapped in, so an interrupted save leaves the
        /// previous cache intact. Never throws: a cache that cannot be written
        /// only costs a slower scan next time.
        /// </summary>
        public void Save()
        {
            // Work on a snapshot, so that the lock is not held while writing.
            List<KeyValuePair<string, Row>> rows;
            lock (_lock)
            {
                if (!_dirty) return;
                rows = new List<KeyValuePair<string, Row>>(_rows);
                _dirty = false;
            }

            if (rows.Count > MaxRows)
            {
                // keep the most recently used rows
                rows.Sort((a, b) => b.Value.Seen.CompareTo(a.Value.Seen));
                rows.RemoveRange(MaxRows, rows.Count - MaxRows);
            }

            string file = FilePath;
            string tmp = file + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write(Magic);
                    w.Write(ImageHash.Version);
                    w.Write(DecoderId);
                    w.Write(rows.Count);
                    foreach (KeyValuePair<string, Row> kv in rows)
                    {
                        Row row = kv.Value;
                        w.Write(kv.Key);
                        w.Write(row.Size);
                        w.Write(row.UtcTicks);
                        w.Write(row.Seen);
                        w.Write(row.Fp.DHash);
                        w.Write(row.Fp.PHash);
                        w.Write(row.Fp.PixelWidth);
                        w.Write(row.Fp.PixelHeight);
                        w.Write(row.Fp.Frames);
                        w.Write(row.Fp.Flat);
                        w.Write(row.Fp.Grid, 0, ImageHash.CoarseLength);
                        w.Write(row.Fp.FineGrid, 0, ImageHash.FineLength);
                    }
                }

                if (!File.Exists(file))
                {
                    File.Move(tmp, file);
                }
                else
                {
                    try { File.Replace(tmp, file, null); }      // one atomic swap
                    catch (IOException)
                    {
                        // Some volumes refuse the swap: replace in two steps.
                        File.Delete(file);
                        File.Move(tmp, file);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
                lock (_lock) _dirty = true;          // try again at the next save
                try { File.Delete(tmp); }
                catch { }                            // a stray .tmp is harmless
            }
        }
    }
}
