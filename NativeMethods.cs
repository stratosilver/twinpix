using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace TwinPix
{
    /// <summary>
    /// Every Windows API function the program calls directly, with the
    /// structures and constants they need - in one place, as the .NET
    /// guidelines recommend. The rest of the code goes through Native,
    /// RecycleBin, FileIdentity and FolderWalker, never through here.
    /// </summary>
    /// <remarks>
    /// PHP note: [DllImport] is "P/Invoke", the equivalent of PHP's FFI - it
    /// calls a C function of a Windows DLL. The structures below must match
    /// the C declarations byte for byte (field order, sizes, packing), or
    /// Windows reads the wrong memory. Each one mirrors the Windows SDK
    /// header named in its comment; do not reorder their fields.
    /// </remarks>
    internal static class NativeMethods
    {
        internal static readonly IntPtr InvalidHandle = new IntPtr(-1);

        // ---- list view and header (commctrl.h) -----------------------------
        internal const int LVM_FIRST = 0x1000;
        internal const int LVM_GETHEADER = LVM_FIRST + 31;
        internal const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
        internal const int LVS_EX_DOUBLEBUFFER = 0x00010000;

        internal const int HDM_FIRST = 0x1200;
        internal const int HDM_GETITEM = HDM_FIRST + 11;
        internal const int HDM_SETITEM = HDM_FIRST + 12;
        internal const int HDI_FORMAT = 0x0004;
        internal const int HDF_SORTUP = 0x0400;
        internal const int HDF_SORTDOWN = 0x0200;

        // ---- edit and combo box (winuser.h, commctrl.h) --------------------
        internal const int EM_SETRECT = 0x00B3;
        internal const int EM_SETCUEBANNER = 0x1501;
        internal const int CB_SETCUEBANNER = 0x1703;

        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;

        // ---- shell icons (shellapi.h) --------------------------------------
        internal const uint SHGFI_ICON = 0x000000100;
        internal const uint SHGFI_SMALLICON = 0x000000001;
        internal const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        // ---- shell file operations (shellapi.h) ----------------------------
        internal const uint FO_DELETE = 0x0003;
        internal const ushort FOF_NOCONFIRMATION = 0x0010;   // no "are you sure" per file
        internal const ushort FOF_ALLOWUNDO = 0x0040;        // recycle instead of destroy
        internal const ushort FOF_NOERRORUI = 0x0400;        // errors come back as a code
        internal const ushort FOF_WANTNUKEWARNING = 0x4000;  // still ask before destroying for good

        // ---- files and volumes (winbase.h, winnt.h) ------------------------
        internal const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        internal const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        internal const uint FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00040000;
        internal const uint FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 0x00400000;

        /// <summary>
        /// Bit of a reparse tag set when the reparse point is another name for
        /// some other file or folder - a junction or a symbolic link
        /// (IsReparseTagNameSurrogate in winnt.h). OneDrive's placeholders are
        /// reparse points too, but not name surrogates.
        /// </summary>
        internal const uint REPARSE_TAG_NAME_SURROGATE_BIT = 0x20000000;

        internal const uint DRIVE_UNKNOWN = 0;
        internal const uint DRIVE_NO_ROOT_DIR = 1;
        internal const uint DRIVE_REMOVABLE = 2;
        internal const uint DRIVE_FIXED = 3;
        internal const uint DRIVE_REMOTE = 4;
        internal const uint DRIVE_CDROM = 5;
        internal const uint DRIVE_RAMDISK = 6;

        // ---- structures ----------------------------------------------------

        /// <summary>RECT (windef.h).</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        /// <summary>HDITEM (commctrl.h).</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct HDITEM
        {
            public int mask;
            public int cxy;
            public IntPtr pszText;
            public IntPtr hbm;
            public int cchTextMax;
            public int fmt;
            public IntPtr lParam;
            public int iImage;
            public int iOrder;
            public int type;
            public IntPtr pvFilter;
            public int state;
        }

        /// <summary>COMBOBOXINFO (winuser.h).</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct COMBOBOXINFO
        {
            public int cbSize;
            public RECT rcItem;
            public RECT rcButton;
            public int stateButton;
            public IntPtr hwndCombo;
            public IntPtr hwndItem;
            public IntPtr hwndList;
        }

        /// <summary>SHFILEINFOW (shellapi.h).</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        /// <summary>
        /// SHFILEOPSTRUCTW (shellapi.h) as a 32-bit process sees it: the header
        /// packs it to 1 byte in 32-bit builds only, so the structure is
        /// declared twice and RecycleBin picks the one matching the process.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        internal struct SHFILEOPSTRUCT32
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        /// <summary>SHFILEOPSTRUCTW (shellapi.h) as a 64-bit process sees it: natural alignment.</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SHFILEOPSTRUCT64
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        /// <summary>BY_HANDLE_FILE_INFORMATION (fileapi.h).</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct BY_HANDLE_FILE_INFORMATION
        {
            public uint dwFileAttributes;
            public ComTypes.FILETIME ftCreationTime;
            public ComTypes.FILETIME ftLastAccessTime;
            public ComTypes.FILETIME ftLastWriteTime;
            public uint dwVolumeSerialNumber;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint nNumberOfLinks;
            public uint nFileIndexHigh;
            public uint nFileIndexLow;
        }

        /// <summary>WIN32_FIND_DATAW (minwinbase.h).</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public ComTypes.FILETIME ftCreationTime;
            public ComTypes.FILETIME ftLastAccessTime;
            public ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;          // the reparse tag, for a reparse point
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
        }

        // ---- functions -----------------------------------------------------

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        internal static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref HDITEM lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetComboBoxInfo(IntPtr hwndCombo, ref COMBOBOXINFO info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref SHFILEINFO psfi,
                                                    uint cbFileInfo, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
        internal static extern int SHFileOperation32(ref SHFILEOPSTRUCT32 op);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
        internal static extern int SHFileOperation64(ref SHFILEOPSTRUCT64 op);

        [DllImport("comctl32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int TaskDialog(IntPtr hwndParent, IntPtr hInstance, string title,
                                              string mainInstruction, string content,
                                              int commonButtons, IntPtr icon, out int pressedButton);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(SafeFileHandle hFile,
                                                               out BY_HANDLE_FILE_INFORMATION info);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "FindFirstFileW")]
        internal static extern IntPtr FindFirstFile(string fileName, out WIN32_FIND_DATA data);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumePathNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumeNameForVolumeMountPointW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetVolumeNameForVolumeMountPoint(string volumeMountPoint, StringBuilder volumeName,
                                                                     int bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetDriveTypeW")]
        internal static extern uint GetDriveType(string rootPathName);
    }
}
