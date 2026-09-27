using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TwinPix
{
    /// <summary>
    /// Sends files to the Windows Recycle Bin - and refuses to when Windows
    /// would silently delete them for good instead.
    /// </summary>
    /// <remarks>
    /// "Move to the Recycle Bin" (SHFileOperation with FOF_ALLOWUNDO) is only a
    /// request: where there is no Recycle Bin, Windows deletes the file
    /// permanently, and with FOF_NOCONFIRMATION it does not even ask. That
    /// happens on network drives and shares, on most USB sticks and memory
    /// cards, on SUBST drives, when the bin is turned off for the drive
    /// ("Don't move files to the Recycle Bin") or by a policy, when a file is
    /// larger than the bin, and for paths longer than 260 characters.
    /// <see cref="WhyNotAvailable"/> checks all of these before anything is
    /// sent; FOF_WANTNUKEWARNING stays set as a last net, so that Windows
    /// still asks if it was about to destroy a file after all.
    /// </remarks>
    public static class RecycleBin
    {
        private const int MaxPath = 260;
        private const string VolumeSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\";
        private const string ExplorerPolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";

        /// <summary>
        /// Null when every file can go to the Recycle Bin; otherwise, in plain
        /// words, why not - and then nothing must be sent.
        /// </summary>
        public static string WhyNotAvailable(IList<string> paths)
        {
            if (!Native.IsWindows) return "The Recycle Bin is only available on Windows.";
            if (DisabledByPolicy())
                return "The Recycle Bin is turned off on this computer (by a policy): Windows would delete the files for good.";

            // Files grouped by volume: each volume has its own bin and settings.
            var bytesPerVolume = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                if (!PathHelper.IsAbsolute(path) || path.IndexOfAny(new[] { '*', '?' }) >= 0)
                    return "Unexpected file path: " + path;
                if (path.Length >= MaxPath)
                    return "This path is too long for the Recycle Bin, so Windows would delete the file for good:\r\n" + path;

                string volume = VolumeOf(path);
                if (volume == null) return "The drive of this file cannot be identified:\r\n" + path;

                long size = 0;
                try { size = new FileInfo(path).Length; }
                catch (Exception ex) { if (!PathHelper.IsFileSystemError(ex)) throw; }
                long total;
                bytesPerVolume.TryGetValue(volume, out total);
                bytesPerVolume[volume] = total + size;
            }

            foreach (KeyValuePair<string, long> kv in bytesPerVolume)
            {
                string why = WhyNotAvailableOn(kv.Key, kv.Value);
                if (why != null) return why;
            }
            return null;
        }

        /// <summary>The checks for one volume (drive), given the total size headed for its bin.</summary>
        private static string WhyNotAvailableOn(string volume, long totalBytes)
        {
            uint type = NativeMethods.GetDriveType(volume);
            if (type == NativeMethods.DRIVE_REMOTE)
                return volume + " is a network drive. Windows has no Recycle Bin there and would delete the files for good.";
            if (type == NativeMethods.DRIVE_REMOVABLE)
                return volume + " is a removable drive (USB stick, memory card). Windows usually has no Recycle Bin there and would delete the files for good.";
            if (type != NativeMethods.DRIVE_FIXED)
                return volume + " is not a local hard disk. Windows has no Recycle Bin there and would delete the files for good.";

            string guid = VolumeGuid(volume);
            if (guid == null)
                return "Windows does not report a Recycle Bin for " + volume + " (a SUBST drive, for instance), so it might delete the files for good.";

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(VolumeSettingsKey + guid))
                {
                    if (key == null) return null;                    // never configured: the defaults, bin on
                    if (ToInt(key.GetValue("NukeOnDelete")) == 1)
                        return "The Recycle Bin of " + volume + " is set to \"Don't move files to the Recycle Bin\": Windows would delete the files for good.";
                    int maxMb = ToInt(key.GetValue("MaxCapacity"));
                    if (maxMb > 0 && totalBytes > (long)maxMb * 1024 * 1024)
                        return "The files (" + Format.FileSize(totalBytes) + ") are larger than the Recycle Bin of "
                             + volume + " (" + Format.FileSize((long)maxMb * 1024 * 1024) + "): Windows would delete files for good to make room.";
                }
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;        // registry unreadable: rely on the defaults
            }
            return null;
        }

        /// <summary>The root of the volume holding the file: "C:\", "\\server\share\", or a mount folder.</summary>
        private static string VolumeOf(string path)
        {
            var sb = new StringBuilder(MaxPath + 1);
            if (!NativeMethods.GetVolumePathName(path, sb, sb.Capacity)) return null;
            return sb.ToString();
        }

        /// <summary>"{GUID}" of a local volume, as the Recycle Bin settings are keyed; null when there is none.</summary>
        private static string VolumeGuid(string volumeRoot)
        {
            var sb = new StringBuilder(64);
            if (!NativeMethods.GetVolumeNameForVolumeMountPoint(volumeRoot, sb, sb.Capacity)) return null;
            string name = sb.ToString();                             // \\?\Volume{GUID}\
            int open = name.IndexOf('{'), close = name.IndexOf('}');
            return open >= 0 && close > open ? name.Substring(open, close - open + 1) : null;
        }

        /// <summary>True when a group policy (NoRecycleFiles) turns the bin off for every drive.</summary>
        private static bool DisabledByPolicy()
        {
            try
            {
                using (RegistryKey user = Registry.CurrentUser.OpenSubKey(ExplorerPolicyKey))
                    if (user != null && ToInt(user.GetValue("NoRecycleFiles")) == 1) return true;

                RegistryView view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey machine = hklm.OpenSubKey(ExplorerPolicyKey))
                    if (machine != null && ToInt(machine.GetValue("NoRecycleFiles")) == 1) return true;
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
            }
            return false;
        }

        private static int ToInt(object registryValue)
        {
            return registryValue is int ? (int)registryValue : 0;
        }

        /// <summary>
        /// Sends the files to the Recycle Bin in one shell operation, so the
        /// whole batch is a single "Undo" in Explorer. Call
        /// <see cref="WhyNotAvailable"/> first. Returns null when the shell
        /// reported success, otherwise a message; whether each file actually
        /// left is for the caller to check.
        /// </summary>
        public static string Send(IWin32Window owner, IList<string> paths)
        {
            if (!Native.IsWindows) return "The Recycle Bin is only available on Windows.";
            if (paths == null || paths.Count == 0) return null;

            // pFrom is a list of full paths, each ended by a null character,
            // the whole list ended by a second one. The marshaller adds the
            // very last null; the others are added here. Without it the shell
            // would read on past the string - and could delete whatever file
            // names it found in that memory.
            var sb = new StringBuilder();
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) || !PathHelper.IsAbsolute(path))
                    return "Unexpected file path: " + path;
                sb.Append(path).Append('\0');
            }
            string from = sb.ToString();
            ushort flags = (ushort)(NativeMethods.FOF_ALLOWUNDO | NativeMethods.FOF_NOCONFIRMATION
                                    | NativeMethods.FOF_WANTNUKEWARNING | NativeMethods.FOF_NOERRORUI);
            IntPtr parent = owner == null ? IntPtr.Zero : owner.Handle;

            try
            {
                int result;
                bool aborted;
                if (IntPtr.Size == 8)                   // 64-bit process
                {
                    var op = new NativeMethods.SHFILEOPSTRUCT64();
                    op.hwnd = parent;
                    op.wFunc = NativeMethods.FO_DELETE;
                    op.pFrom = from;
                    op.fFlags = flags;
                    result = NativeMethods.SHFileOperation64(ref op);
                    aborted = op.fAnyOperationsAborted;
                }
                else
                {
                    var op = new NativeMethods.SHFILEOPSTRUCT32();
                    op.hwnd = parent;
                    op.wFunc = NativeMethods.FO_DELETE;
                    op.pFrom = from;
                    op.fFlags = flags;
                    result = NativeMethods.SHFileOperation32(ref op);
                    aborted = op.fAnyOperationsAborted;
                }

                if (result != 0) return "The Recycle Bin refused the operation (code " + result + ").";
                if (aborted) return "The operation was cancelled before every file was moved.";
                return null;
            }
            catch (Exception ex)            // P/Invoke failures: missing DLL, entry point...
            {
                return ex.Message;
            }
        }
    }
}
