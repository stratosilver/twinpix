# TwinPix

A Windows application (WinForms, C#) that finds **duplicate images** in a folder
and its subfolders, shows them side by side with a preview, and lets you pick the
one to keep. The others are moved to a quarantine folder — nothing is ever
deleted.

## Build

Nothing to install: the `csc.exe` compiler shipped with Windows is enough.

```
build.bat
```

The script looks for `csc.exe` in `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`
(then the 32-bit path), compiles `TwinPix.cs` and writes `TwinPix.exe` next to
it. The executable is self-contained and can be copied anywhere.

`assets\twinpix.ico` is applied twice: as the executable's own icon
(`/win32icon`, what Explorer and the shortcut show) and as an embedded resource
(`/resource`, what the window and the taskbar show at run time). It is built
from `assets\twinpix.png` and holds the 16/20/24/32/40/48/64 px frames Windows
asks for plus a 256 px one for the large-icon views. If the file is missing the
build still succeeds, with a warning, and the application falls back to the
default icon.

Equivalent manual command:

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe ^
  /reference:System.dll /reference:System.Core.dll ^
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /out:TwinPix.exe TwinPix.cs
```

## Duplicate criteria

Two images are treated as duplicates when all three of the following match:

1. **the file name**, compared as it is, case aside — `photo.jpg` and
   `PHOTO.jpg` are the same name, `photo (1).jpg` is not;
2. **the exact size** in bytes;
3. **the extension** (`.jpg` and `.jpeg` stay distinct).

Option: *Check content (MD5)* — hashes every file in a group and splits apart
those whose content differs. Slower, but guarantees real duplicates.

## Usage

1. **Folder to scan** — the root of the walk (subfolders included).
2. **Preferred folder** *(optional)* — images living there are pre-selected as the
   ones to keep in every group.
   Every folder field is a drop-down that remembers the folders used before:
   pick one from the list, or keep typing — the path auto-completes against the
   file system. The three lists are saved in
   `%APPDATA%\TwinPix\folders.txt` and are restored at the next start, with the
   most recent entry pre-selected. Right-click a field to clear its list.
3. **SCAN** — the list on the left shows one row per duplicate group, sorted by
   reclaimable space, and takes three quarters of the window. Click any column
   header to sort by it (name, extension,
   size, number of copies, reclaimable space, folder being kept); click the same
   header again to reverse the order. The active column carries a `^` or `v`
   marker.
4. Select a group: its thumbnails appear on the right. Click a thumbnail (or
   "Keep this file") to mark the copy to keep; it turns green.
   The *Keep automatically* buttons above the list apply a rule to every group:
   preferred folder, oldest, newest, shortest path. The preferred folder always
   wins over the other rules.
5. **Move duplicates to** — enter the destination folder, then *Move duplicates
   in group* or *Move ALL duplicates*. *Keep folder structure* recreates the
   original relative path inside the destination, so everything can be put back
   if needed.

**SCAN** is the window's default button (Enter key), which Windows outlines on
its own; it reads *CANCEL* while a scan runs. It and **Move ALL duplicates**
carry a bold label to mark them as the primary actions. They are deliberately
left in the system's own button style: giving a button a custom background
colour makes Windows drop the visual-style rendering and fall back to a
square-cornered classic button, which no longer matches its neighbours.

*Export CSV* writes the full inventory (group, file, folder, size, date, action)
without changing anything.

Extras: double-click a thumbnail to open the image in the default viewer;
right-click offers "Open containing folder" and "Copy path".

## Notes

- Nothing is ever deleted: duplicates are **moved**, and name collisions in the
  destination are resolved with a ` (1)`, ` (2)`… suffix.
- If the destination sits inside the scanned folder, the application warns you
  (the moved files would be found again by the next scan).
- Thumbnails are loaded into memory and the file is closed immediately, so no
  lock ever blocks a move.
- Formats without a GDI+ decoder (HEIC, RAW, PSD…) are still detected and moved;
  only the preview shows "No preview". The list of scanned extensions can be
  edited in the options bar.
- Folders that cannot be read (insufficient rights) are skipped and counted in
  the status bar.
