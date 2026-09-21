# TwinPix

A Windows application (WinForms, C#) that finds **duplicate images** — same size,
same extension — in a folder and its subfolders, shows them side by side with a preview, and lets you pick the
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

`assets\TwinPix.manifest` is applied with `/win32manifest`. It pulls in version 6
of the common controls, which is what themes the controls and makes the Vista
task dialog available; without it the build still succeeds and the dialogs fall
back to plain message boxes.

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

Two images are treated as duplicates when both of the following match:

1. **the exact size** in bytes;
2. **the extension**, compared without regard to case — `IMG_4471.JPG` and
   `photo.jpg` are the same extension, `.jpg` and `.jpeg` are not.

File names play no part: two images with the same size and extension are
grouped whatever they are called. That is a deliberately wide net, so
*Check content (MD5)* matters here — it hashes every file of a group and splits
apart those whose bytes differ, turning "same size by coincidence" into
"identical file". It is slower, but it is what makes the result trustworthy on
a large library.

Because a group has no single name, the list shows the name of the file you are
keeping (*Kept file*) and the folder it lives in (*Kept in*); both follow your
selection.

## Usage

1. **Folder to scan** — the root of the walk (subfolders included).
2. **Preferred folder** *(optional)* — images living there are pre-selected as the
   ones to keep in every group.
   Every folder field is a drop-down that remembers the folders used before:
   pick one from the list, or keep typing — the path auto-completes against the
   file system. The three lists are saved in
   `%APPDATA%\TwinPix\folders.txt` (which also holds the window placement) and
   are restored at the next start, with the
   most recent entry pre-selected. Right-click a field to clear its list.
3. **SCAN** — the list on the left shows one row per duplicate group, sorted by
   reclaimable space, and takes three quarters of the window. Click any column
   header to sort by it (kept file, extension,
   size, number of copies, reclaimable space, folder being kept); click the same
   header again to reverse the order. The active column carries a `^` or `v`
   marker.
4. Select a group: its thumbnails appear on the right, titled with the size and
   extension shared by the files. Click a thumbnail (or
   "Keep this file") to mark the copy to keep; it turns green.
   The *Keep* check boxes above the list decide the choice made for you, and
   always apply to **every** group at once:

   - **Preferred folder** follows the field of the same name: it ticks itself as
     soon as a folder is given and greys out when the field is emptied. While it
     is ticked, a copy sitting in that folder is kept whatever the rule below
     says. Untick it to ignore the folder without clearing the field.
   - **Oldest**, **Newest** and **Shortest path** are exclusive — exactly one is
     always active — and settle the choice between the remaining copies.
     *Shortest path* (the copy closest to the scanned folder) is the default.

   Changing any box, or pointing the preferred folder somewhere else, re-applies
   the choice to the whole list immediately. A path typed by hand is taken into
   account as soon as typing pauses, so the list does not re-sort at every
   keystroke.
5. **Move duplicates to** — enter the destination folder, then *Move ALL
   duplicates*. *Keep folder structure* recreates the original relative path
   inside the destination, so everything can be put back if needed.
   *Move to trash* (unticked by default) sends the duplicates to the Windows
   Recycle Bin instead, from where they can be restored; the destination field,
   its *Browse* button and *Keep folder structure* are greyed out while it is
   ticked. The whole batch goes in one shell operation, so a single *Undo* in
   Explorer puts every file back, and Windows still asks before destroying for
   good a file too large for the bin.

*Include subfolders* and *Check content (MD5)* change what a scan finds, so
ticking or unticking either one searches the folder again and rebuilds the list
of duplicates on the spot. Nothing happens before the first scan, and a change
made while a scan is running simply applies to the next one.

The menu bar and the toolbar carry the same commands, with the usual shortcuts:
F5 to scan, Ctrl+Shift+M to move, Ctrl+E to export. Every field and
check box has an access key (Alt+F for the folder to scan, and so on).

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

## Native look

The interface deliberately stays on stock Win32 controls and lets Windows draw
them:

- the group list is themed as Explorer themes its own (`SetWindowTheme`), is
  double-buffered by the control itself, and its **sort arrow is drawn in the
  column header** by the header control, not faked with a text marker;
- each row carries the shell icon of its file type (`SHGetFileInfo`);
- empty folder fields show a grey prompt (`CB_SETCUEBANNER`);
- confirmations use the Vista task dialog — a large question, the details below
  it — and fall back to a message box on older systems;
- the window remembers its size and position between runs, and ignores them if
  the monitor they belonged to is gone.

All of it is optional: each helper in the `Native` class checks the platform and
does nothing when it is not available, so the application still runs (with the
framework's default appearance) anywhere WinForms runs.

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
