using System;
using System.IO;

namespace TwinPix
{
    /// <summary>
    /// Who a file really is, as opposed to what it is called.
    /// </summary>
    /// <remarks>
    /// Windows can give one file several names: hard links, symbolic links,
    /// folder junctions, a folder reached both through a drive letter and a
    /// network path. Two such names hold the same bytes, so a duplicate finder
    /// comparing contents sees "two identical files" - and removing "the
    /// copy" removes the file itself, since the name that was kept pointed to
    /// the very same data. This is one of the classic ways duplicate finders
    /// have destroyed the only copy of a file.
    ///
    /// Two names are one file when the volume serial number and the file index
    /// Windows reports for them (GetFileInformationByHandle) are equal.
    ///
    /// PHP note: this is a "struct", a value type - assigning it copies it,
    /// like a PHP array or int; a "class" is a reference type, like a PHP
    /// object.
    /// </remarks>
    public struct FileIdentity
    {
        private readonly bool _readable;
        private readonly uint _volume;
        private readonly ulong _index;

        private FileIdentity(bool readable, uint volume, ulong index)
        {
            _readable = readable;
            _volume = volume;
            _index = index;
        }

        /// <summary>The file could be opened for reading.</summary>
        public bool Readable { get { return _readable; } }

        /// <summary>
        /// The file system reported an identity. Some network file systems
        /// report none; two such files cannot be told apart this way.
        /// </summary>
        public bool Known { get { return _readable && (_volume != 0 || _index != 0); } }

        /// <summary>True only when both identities are known and equal.</summary>
        public bool IsSameFileAs(FileIdentity other)
        {
            return Known && other.Known && _volume == other._volume && _index == other._index;
        }

        /// <summary>
        /// Reads the identity of the file at <paramref name="path"/>. Never
        /// throws: a file that cannot be opened gives an identity that is
        /// neither <see cref="Readable"/> nor <see cref="Known"/>.
        /// </summary>
        public static FileIdentity Of(string path)
        {
            try
            {
                // Read access, and every kind of sharing, so that nobody else
                // is disturbed; a symbolic link is followed to its target.
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite | FileShare.Delete, 1))
                {
                    if (!Native.IsWindows) return new FileIdentity(true, 0, 0);

                    NativeMethods.BY_HANDLE_FILE_INFORMATION info;
                    if (!NativeMethods.GetFileInformationByHandle(fs.SafeFileHandle, out info))
                        return new FileIdentity(true, 0, 0);
                    ulong index = ((ulong)info.nFileIndexHigh << 32) | info.nFileIndexLow;
                    return new FileIdentity(true, info.dwVolumeSerialNumber, index);
                }
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex) && !(ex is EntryPointNotFoundException)
                    && !(ex is DllNotFoundException)) throw;
                return new FileIdentity(false, 0, 0);
            }
        }
    }
}
