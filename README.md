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

Equivalent manual command:

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe ^
  /reference:System.dll /reference:System.Core.dll ^
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /out:TwinPix.exe TwinPix.cs
```

## Duplicate criteria

Two images are treated as duplicates when all three of the following match:

1. **the file name**, copy suffixes ignored — `photo.jpg`, `photo (1).jpg`,
   `photo - Copy.jpg`, `photo copy 2.jpg` end up in a single group;
2. **the exact size** in bytes;
3. **the extension** (`.jpg` and `.jpeg` stay distinct).

Options:

- *Also ignore `_1` / `-1`* — widens normalization to plain numeric suffixes (off
  by default, since `IMG_1234` would otherwise be truncated);
- *Check content (MD5)* — hashes every file in a group and splits apart those
  whose content differs. Slower, but guarantees real duplicates.

## Usage

1. **Folder to scan** — the root of the walk (subfolders included).
2. **Preferred folder** *(optional)* — images living there are pre-selected as the
   ones to keep in every group.
3. **SCAN** — the list on the left shows one row per duplicate group, sorted by
   reclaimable space.
4. Select a group: its thumbnails appear on the right. Click a thumbnail (or
   "Keep this file") to mark the copy to keep; it turns green.
   The *Auto-select* buttons apply a rule to every group: preferred folder,
   oldest, newest, shortest path. The preferred folder always wins over the
   other rules.
5. **Move duplicates to** — enter the destination folder, then *Move duplicates
   in group* or *Move ALL duplicates*. *Keep folder structure* recreates the
   original relative path inside the destination, so everything can be put back
   if needed.

**SCAN** is the window's default button (Enter key); it turns orange and reads
*CANCEL* while a scan runs. **Move ALL duplicates** is highlighted in green and
stays greyed out until there are results.

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
