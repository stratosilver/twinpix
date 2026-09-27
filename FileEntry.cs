using System;
using System.IO;

namespace TwinPix
{
    /// <summary>
    /// One image file found by a scan: where it is, what it weighed and when
    /// it was last written at the time of the scan, and - for visual
    /// matching - what it looks like.
    /// </summary>
    /// <remarks>
    /// PHP note: "{ get; set; }" declares a property whose storage the
    /// compiler writes for you - to the caller it reads like a public field,
    /// but it can later grow a getter or setter body without the callers
    /// changing. "private set" means only this class may assign it.
    /// </remarks>
    public class FileEntry
    {
        /// <summary>Full path, as the scan found it.</summary>
        public string FullPath { get; set; }

        /// <summary>Name with extension, no folder.</summary>
        public string FileName { get; set; }

        /// <summary>Extension in lower case, dot included (".jpg").</summary>
        public string Extension { get; set; }

        /// <summary>
        /// Size in bytes at scan time.
        /// PHP note: a C# "int" is always 32 bits (at most 2 GB), which is why
        /// file sizes are "long" (64 bits) everywhere in this program.
        /// </summary>
        public long Size { get; set; }

        /// <summary>Last write time, local time: shown to the user and used by the keep rules.</summary>
        public DateTime Modified { get; set; }

        /// <summary>
        /// Last write time in UTC: what the safety checks and the fingerprint
        /// cache compare, because local time shifts by an hour when daylight
        /// saving time starts or ends.
        /// </summary>
        public DateTime ModifiedUtc { get; set; }

        /// <summary>Sits in one of the folders ticked as preferred.</summary>
        public bool InPreferred { get; set; }

        /// <summary>The copy of its group that stays where it is.</summary>
        public bool Keep { get; set; }

        /// <summary>MD5 of the content, byte matching only (null otherwise).</summary>
        public string Hash { get; set; }

        /// <summary>Visual matching only: what the picture looks like.</summary>
        public Fingerprint Fp { get; set; }

        /// <summary>
        /// Visual matching only: how far this copy sits from the reference
        /// copy of its group (the one with the most pixels), in bits out of 64.
        /// </summary>
        public int Distance { get; set; }

        public int PixelWidth { get { return Fp == null ? 0 : Fp.PixelWidth; } }
        public int PixelHeight { get { return Fp == null ? 0 : Fp.PixelHeight; } }
        public long Pixels { get { return (long)PixelWidth * PixelHeight; } }

        /// <summary>"4000x3000", or "" when the size is not known.</summary>
        public string Dimensions
        {
            get
            {
                return PixelWidth > 0 && PixelHeight > 0
                     ? PixelWidth + "x" + PixelHeight : "";
            }
        }

        /// <summary>The folder holding the file, or "" if the path is malformed.</summary>
        public string DirectoryPath
        {
            get
            {
                try { return Path.GetDirectoryName(FullPath) ?? ""; }
                catch (ArgumentException) { return ""; }
                catch (PathTooLongException) { return ""; }
            }
        }

        /// <summary>
        /// Builds an entry from what the folder listing returned. The FileInfo
        /// objects of a listing already carry size and dates, so this does not
        /// touch the disk again. Throws when the file vanished in between.
        /// </summary>
        public static FileEntry FromFile(FileInfo file)
        {
            var e = new FileEntry();
            e.FullPath = file.FullName;
            e.FileName = file.Name;
            e.Extension = (file.Extension ?? "").ToLowerInvariant();
            e.Size = file.Length;
            e.ModifiedUtc = file.LastWriteTimeUtc;
            e.Modified = e.ModifiedUtc.ToLocalTime();
            return e;
        }
    }
}
