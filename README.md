# TwinPix

A Windows application (WinForms, C#) that finds **duplicate images** in a folder
and its subfolders — by size and extension, by content, or by what the picture
actually looks like — shows them side by side with a preview, and lets you pick
the one to keep. The others are moved to a quarantine folder or to the Recycle
Bin; nothing is destroyed outright.

## Project layout

| File | What is in it |
| --- | --- |
| `TwinPix.cs` | the engine: model, fingerprints, cache, scanner, and the Win32 and helper classes |
| `MainForm.cs` | the main window's behaviour - scanning, keep rules, moving, export |
| `MainForm.Designer.cs` | every control of the main window and its layout |
| `FileCard.cs` | the thumbnail card's behaviour |
| `FileCard.Designer.cs` | the thumbnail card's layout |
| `MainForm.resx`, `FileCard.resx` | designer resources (empty; kept for Visual Studio) |
| `TwinPix.csproj`, `TwinPix.sln` | Visual Studio project and solution |

The two `.Designer.cs` files hold nothing but control creation and property
assignments, which is what lets Visual Studio's **Windows Forms designer** open
`MainForm.cs` and `FileCard.cs` on the design surface (double-click either in
the Solution Explorer). Move a button, change a caption, add a control: the
designer rewrites the `.Designer.cs` file and leaves the rest alone. Nothing
outside those two files creates a control, so the design surface and the running
window can never disagree.

Events added in the designer's property grid land in `MainForm.cs` or
`FileCard.cs` as ordinary methods; the handlers already there are thin wrappers
that call the real work, so a new button is two clicks and one line.

## Build

Nothing to install: the `csc.exe` compiler shipped with Windows is enough.

```
build.bat
```

The script looks for `csc.exe` in `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`
(then the 32-bit path), compiles every `.cs` file next to it and writes
`TwinPix.exe` in the same folder. The executable is self-contained and can be
copied anywhere.

`TwinPix.csproj` builds those very same sources in Visual Studio (F5, output in
`bin\Debug`). Neither build needs the other, and the project file is there for
the designer: the application itself still depends on nothing but the framework
shipped with Windows.

The `.resx` files are for the designer only - nothing in them is loaded at run
time, which is why `csc.exe` does not need them. Dropping a picture on a form in
the designer would put it in a `.resx` that `build.bat` cannot embed; keep images
out of the designer, or build with Visual Studio from that point on.

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
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
%FW%\csc.exe /target:winexe ^
  /define:WIC /lib:"%FW%\WPF" ^
  /reference:PresentationCore.dll /reference:WindowsBase.dll /reference:System.Xaml.dll ^
  /reference:System.dll /reference:System.Core.dll ^
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /out:TwinPix.exe *.cs
```

The three WPF references and `/define:WIC` are what enable the fast image
decoder used by visual matching; drop all four and the build still succeeds,
falling back to GDI+ (`build.bat nowic` does exactly that). The .NET Framework
4.x is required — the scan runs on every core.

## Duplicate criteria

The *Matching* list decides what counts as a duplicate.

**Same bytes (MD5)** (the default). Files are first grouped by **exact size** in
bytes and **extension**, compared without regard to case — `IMG_4471.JPG` and
`photo.jpg` are the same extension, `.jpg` and `.jpeg` are not. Every file of a
group is then hashed and those whose bytes differ are split apart, so a group
only ever holds identical files. File names play no part.

**Same picture (visual)** drops the byte-level criteria entirely and compares
what the images look like, so it finds the same photograph again after it has
been resized, re-saved at another quality, converted to another format, stripped
of its EXIF or turned to greyscale — cases the byte comparison cannot see at
all, because not one byte is shared. See below.

Because a group has no single name, the list shows the name of the file you are
keeping (*Kept file*) and the folder it lives in (*Kept in*); both follow your
selection.

## Visual matching

Each image is reduced to a fingerprint of 265 bytes, and only fingerprints are
compared:

- **dHash**, 64 bits — a 9×8 grey grid, one bit per "is this pixel brighter than
  the one on its right". Unaffected by scaling, by re-compression and by a shift
  in overall brightness.
- **pHash**, 64 bits — the low frequencies of a 32×32 DCT, thresholded at their
  median. Sturdier, used to confirm.
- an **8×8 grey grid** — kept so that two candidates can be compared pixel by
  pixel, which is what rejects the look-alikes the two hashes agree on by
  accident.
- an **11×11 grey grid** — the same, with about twice the points (121 instead
  of 64). It replaces the 8×8 grid at the *Normal* sensitivity.

A pair is accepted only when the hashes agree, the aspect ratio matches within
8 %, and the grids line up. Accepted pairs are merged with a union-find, so a
group is a connected component: a thumbnail matching a medium copy that matches
the original puts all three together even if the thumbnail and the original are
too far apart to match each other directly.

*Sensitivity* sets how many of the 64 bits may differ — 3 for re-saved and
resized copies, 6 by default, 10 for cropped or lightly retouched ones. At
*Normal*, the pixel-by-pixel check also runs on the 11×11 grid rather than the
8×8 one, which rejects more near-misses.

Nothing is compared with everything: the 64 bits are cut into *k* bands, where
*k* is the smallest power of two above the threshold. Two fingerprints that
differ by at most *k−1* bits cannot differ in every band, so indexing each band
finds every pair while only ever comparing images that already share a band.
The cost grows with the number of images, not with its square.

Images below 32 px on a side, and images too uniform to be told apart (an empty
sky, a black scan), are left out and counted separately in the status bar.

**Performance.** Decoding is the whole cost. Where the WPF assemblies sit next
to the compiler — they always do on a standard Windows install — `build.bat`
compiles in the WIC decoder, which asks for a scaled-down image and lets a JPEG
stop at one eighth of its resolution instead of unpacking every pixel. Failing
that, the build falls back to GDI+, which is correct and several times slower.
Either way the work is spread over every core.

**Cache.** Fingerprints are kept in `%APPDATA%\TwinPix\fingerprints.bin`, keyed
by path, size and modification time, so re-scanning a folder decodes only what
has changed since. On a 200-image test set the first scan took 720 ms and the
second 2 ms. The file is rewritten whenever the way a fingerprint is computed
changes, and an unreadable one simply costs a slower scan.

**What it will not do.** A group holds copies of different sizes and formats, so
*Oldest*, *Newest* and *Shortest path* stop saying anything useful there:
*Best resolution* is selected automatically with this mode and keeps the copy
with the most pixels, then the heaviest — the one closest to the original.
Rotated and mirrored copies are not matched. And a perceptual match is a
judgement, not a proof: *Move to trash* is the safer destination for its
results.

## Usage

1. **Folder to scan** — the root of the walk (subfolders included).
   Every folder field is a drop-down that remembers the folders used before:
   pick one from the list, or keep typing — the path auto-completes against the
   file system. The two lists are saved in
   `%APPDATA%\TwinPix\folders.txt` (which also holds the window placement) and
   are restored at the next start, with the
   most recent entry pre-selected. Right-click a field to clear its list.
2. **SCAN** — the list on the left shows one row per duplicate group, sorted by
   reclaimable space, and takes three quarters of the window. Click any column
   header to sort by it (kept file, extension,
   size, number of copies, reclaimable space, folder being kept); click the same
   header again to reverse the order. The active column carries a `^` or `v`
   marker.
3. **Preferred folders** — below the groups, every subfolder of the scanned
   folder is listed with the number of images it holds and how many of them sit
   in a duplicate group. Tick a folder and the copies it holds are kept in every
   group. Only the files directly inside a ticked folder count, not those of its
   subfolders — tick those too if they should count. The ticks survive a new
   scan of the same folder.
4. Select a group: its thumbnails appear on the right, titled with the size and
   extension shared by the files. Click a thumbnail (or
   "Keep this file") to mark the copy to keep; it turns green.
   The *Keep* check boxes above the list decide the choice made for you, and
   always apply to **every** group at once:

   - **Preferred folders** follows the list of the same name: it ticks itself as
     soon as a folder is ticked there and greys out when none is. While it is
     ticked, a copy sitting in a ticked folder is kept whatever the rule below
     says. Untick it to ignore the folders without clearing the ticks.
   - **Oldest**, **Newest**, **Shortest path**, **Best resolution** and
     **Largest file** are exclusive — exactly one is always active — and settle
     the choice between the remaining copies. *Shortest path* (the copy closest
     to the scanned folder) is the default; picking visual matching switches to
     *Best resolution*, which is the one that makes sense when the copies no
     longer share a size.

   Changing any box, or ticking a preferred folder, re-applies the choice to the
   whole list immediately.
5. **Move duplicates to** — enter the destination folder, then **MOVE ALL**,
   at the bottom right. *Keep folder structure* recreates the original relative path
   inside the destination, so everything can be put back if needed.
   *Move to trash* (unticked by default) sends the duplicates to the Windows
   Recycle Bin instead, from where they can be restored; the destination field,
   its *Browse* button and *Keep folder structure* are greyed out while it is
   ticked. The whole batch goes in one shell operation, so a single *Undo* in
   Explorer puts every file back, and Windows still asks before destroying for
   good a file too large for the bin.

*Include subfolders*, *Matching* and *Sensitivity* change what a scan finds, so
changing any of them searches the folder again and rebuilds the list of
duplicates on the spot. Nothing happens before the first scan, and a change made
while a scan is running simply applies to the next one.

The *File* menu carries the same commands, with the usual shortcuts:
F5 to scan, Ctrl+Shift+M to move, Ctrl+E to export. Every field and
check box has an access key (Alt+F for the folder to scan, and so on).

**SCAN** is the window's default button (Enter key), which Windows outlines on
its own; it reads *CANCEL* while a scan runs. It and **MOVE ALL**
carry a bold label to mark them as the primary actions. They are deliberately
left in the system's own button style: giving a button a custom background
colour makes Windows drop the visual-style rendering and fall back to a
square-cornered classic button, which no longer matches its neighbours.

*File > Export list to CSV* writes the full inventory (group, file, folder, size, date, action)
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
