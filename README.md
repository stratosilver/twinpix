# TwinPix 🏔️🏔️

A very light 300Kb Windows application (WinForms, C#) that finds **duplicate images** in a folder
and its subfolders — by content, or by what the picture actually looks like —
shows them side by side with a preview, and lets you pick the one to keep. The
others are moved to a folder of your choice or to the Recycle Bin, each one
checked again against the kept copy at the moment it moves; nothing is
destroyed outright. See [Safety](#safety) for everything that stands between a
click and a lost picture.

## Project layout

Every class lives in a file of its own name, next to the others (one namespace,
`TwinPix`). The map at the top of `TwinPix.cs` lists them all; in short:

| Files | What is in them |
| --- | --- |
| `Program.cs` | entry point and last-resort error report |
| `MainForm.cs`, `MainForm.Designer.cs` | the main window: behaviour, then controls and layout |
| `FileCard.cs`, `FileCard.Designer.cs` | the thumbnail card: behaviour, then layout |
| `FolderWalker.cs`, `Scanner.cs`, `ImageHash.cs`, `VisualMatcher.cs`, `KeepSelector.cs`… | finding duplicates |
| `DuplicateRemover.cs`, `RecycleBin.cs`, `FileIdentity.cs`, `RemovalJournal.cs`… | removing them safely |
| `NativeMethods.cs`, `Native.cs` | the Windows API calls, and the look-and-feel helpers built on them |
| `TwinPix.cs` | assembly-wide settings and the map of the files |
| `MainForm.resx`, `FileCard.resx` | designer resources |
| `TwinPix.csproj`, `TwinPix.sln` | Visual Studio project and solution |
| `build.bat`, `selftest.bat` | build the application; build and run the safety self-test |
| `tools\` | the `.resx` converter used by `build.bat`, and the self-test |
| `DEVELOPER.md` | the C# this code relies on, explained for a PHP developer, and the rules a change must keep |

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

That compiler only knows **C# 5** (2012). `TwinPix.csproj` sets
`LangVersion` to 5, so Visual Studio refuses the newer syntax too and code
written there always builds with `build.bat`; `DEVELOPER.md` lists what is not
available and what to write instead.

`TwinPix.csproj` builds those very same sources in Visual Studio (F5, output in
`bin\Debug`). Neither build needs the other, and the project file is there for
the designer: the application itself still depends on nothing but the framework
shipped with Windows. A new `.cs` file is picked up by `build.bat` on its own,
but must be added to `TwinPix.csproj`.

Every `.resx` file is converted and embedded by `build.bat` (through
`tools\ResxToResources.cs`), under the name the form asks for at run time, so a
tooltip or a string set in the designer works in both builds.

`assets\TwinPix.manifest` is applied with `/win32manifest`. It pulls in version 6
of the common controls, which is what themes the controls and makes the Vista
task dialog available; without it the build still succeeds and the dialogs fall
back to plain message boxes.

`assets\twinpix.ico` is applied twice: as the executable's own icon
(`/win32icon`, what Explorer and the shortcut show) and as an embedded resource
(`/resource`, what the window and the taskbar show at run time). If the file is
missing the build still succeeds, with a warning, and the application falls
back to the default icon.

The three WPF references and `/define:WIC` are what enable the fast image
decoder used by visual matching; `build.bat nowic` leaves them out and the
build falls back to GDI+. The .NET Framework 4.x is required — the scan runs on
every core.

### Self-test

```
selftest.bat
```

builds the application's sources together with `tools\SelfTest.cs` and runs the
result. It creates a small photo library in a temporary folder — copies, a
Recycle Bin folder, a folder junction, a hard link, a RAW+JPEG-style pair,
multi-page TIFFs, plain images — and checks every safety rule of this README on
it: what a scan leaves out, what the removal refuses, the Recycle Bin guard. It
never looks at your pictures and deletes its folder at the end. Run it after any
change to the engine; the last line must read `ALL SAFETY CHECKS PASSED`.

## Duplicate criteria

The *Matching* list decides what counts as a duplicate.

**Same picture (visual)**, the default, with the *Strict* sensitivity, compares
what the images look like, so it finds the same photograph again after it has
been resized, re-saved at another quality, converted to another format,
stripped of its EXIF or turned to greyscale — cases the byte comparison cannot
see at all, because not one byte is shared. See below.

**Same bytes (MD5)**. Files are first grouped by **exact size** in bytes and
**extension**, compared without regard to case — `IMG_4471.JPG` and `photo.jpg`
are the same extension, `.jpg` and `.jpeg` are not. Every file of a group is then
hashed and those whose bytes differ are split apart. File names play no part.
MD5 only sorts the candidates: before a copy is moved, it is compared with the
kept copy **byte by byte**, so not even an MD5 collision could cost a file.

Because a group has no single name, the list shows the name of the file you are
keeping (*Kept file*) and the folder it lives in (*Kept in*); both follow your
selection.

## Visual matching

Each image is reduced to a fingerprint of a few hundred bytes, and only
fingerprints are compared:

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
too far apart to match each other directly. **Removal is stricter than
grouping**: a copy is only ever moved if it matches the *kept* copy directly;
one that is only linked to it through a third look-alike stays where it is.

*Sensitivity* sets how many of the 64 bits may differ — 3 for re-saved and
resized copies (*Strict*, the default), 6 at *Normal*, 10 for cropped or lightly
retouched ones (*Loose*). At *Normal*, the pixel-by-pixel check also runs on
the 11×11 grid rather than the 8×8 one, which rejects more near-misses.

Nothing is compared with everything: the 64 bits are cut into *k* bands, where
*k* is the smallest power of two above the threshold. Two fingerprints that
differ by at most *k−1* bits cannot differ in every band, so indexing each band
finds every pair while only ever comparing images that already share a band.
The cost grows with the number of images, not with its square.

Never matched, and counted in the status bar:

- images below 32 px on a side, and images too uniform to be told apart (an
  empty sky, a black scan);
- files holding several pictures — animated GIFs, multi-page TIFFs. Only the
  first picture is fingerprinted, so two such files that merely *start* alike
  would look identical;
- two files with the same name in the same folder — `IMG_0042.CR2` and
  `IMG_0042.JPG`, a HEIC and the JPEG exported beside it. Such a pair is made on
  purpose; its files are never duplicates of each other.

**Performance.** Decoding is the whole cost. Where the WPF assemblies sit next
to the compiler — they always do on a standard Windows install — `build.bat`
compiles in the WIC decoder, which asks for a scaled-down image and lets a JPEG
stop at one eighth of its resolution instead of unpacking every pixel. Failing
that, the build falls back to GDI+, which is correct and several times slower.
Either way the work is spread over every core.

**Cache.** Fingerprints are kept in `%APPDATA%\TwinPix\fingerprints.bin`, keyed
by path, size and modification time (UTC, so daylight saving time changes
nothing), so re-scanning a folder decodes only what has changed since. The file
is rewritten whenever the way a fingerprint is computed changes, and an
unreadable one simply costs a slower scan.

**What it will not do.** A group holds copies of different sizes and formats, so
*Oldest*, *Newest* and *Shortest path* stop saying anything useful there:
*Best resolution* is selected automatically with this mode and keeps the copy
with the most pixels, then the heaviest — the one closest to the original.
Rotated and mirrored copies are not matched. And a perceptual match is a
judgement, not a proof: two photographs of the same scene taken a second apart
can look alike. Look through the groups before moving anything.

## Usage

1. **Folder to scan** — the root of the walk (subfolders included), as a full
   path. Every folder field is a drop-down that remembers the folders used
   before: pick one from the list, or keep typing — the path auto-completes
   against the file system. The two lists are saved in
   `%APPDATA%\TwinPix\folders.txt` (which also holds the window placement) and
   are restored at the next start, with the most recent entry pre-selected.
   Right-click a field to clear its list.
2. **SCAN** — the list on the left shows one row per duplicate group, sorted by
   reclaimable space. Click any column header to sort by it (kept file,
   extension, size, number of copies, reclaimable space, folder being kept);
   click the same header again to reverse the order. The status bar says what
   the scan found, and what it left out (see [Safety](#safety)).
3. **Preferred folders** — below the groups, every subfolder of the scanned
   folder is listed with the number of images it holds and how many of them sit
   in a duplicate group. Tick a folder and the copies it holds are kept in every
   group. Ticking a folder ticks all its subfolders with it, and unticking it
   unticks them; a subfolder can then be unticked on its own. The ticks survive a
   new scan of the same folder. The band between the two lists (and the one
   left of the thumbnails) can be dragged to share the space differently.
4. Select a group: its thumbnails appear on the right, titled with the size and
   extension shared by the files. Click a thumbnail (or "Keep this file") to
   mark the copy to keep; it turns green. The *Keep* check boxes above the list
   decide the choice made for you, and always apply to **every** group at once:

   - **Preferred folders** follows the list of the same name: it ticks itself as
     soon as a folder is ticked there and greys out when none is. While it is
     ticked, a copy sitting in a ticked folder is kept whatever the rule below
     says. Untick it to ignore the folders without clearing the ticks.
   - **Oldest**, **Newest**, **Shortest path**, **Best resolution** and
     **Largest file** are exclusive — exactly one is always active — and settle
     the choice between the remaining copies. Visual matching selects
     *Best resolution*, the one that makes sense when the copies no longer share
     a size; byte matching starts with *Shortest path* (the copy closest to the
     scanned folder).

   Changing any box, or ticking a preferred folder, re-applies the choice to the
   whole list immediately.
5. **Move duplicates to** — enter the destination folder (a full path, not the
   scanned folder itself nor one of its parents), then **MOVE ALL**, at the
   bottom right. *Keep folder structure* recreates the original relative path
   inside the destination, so everything can be put back if needed.
   *Move to trash* (unticked by default) sends the duplicates to the Windows
   Recycle Bin instead — only where Windows really has one (see below); the
   destination field, its *Browse* button and *Keep folder structure* are greyed
   out while it is ticked. The whole batch goes in one shell operation, so a
   single *Undo* in Explorer puts every file back.

   The confirmation's default button is *Cancel*: Enter alone never moves
   anything. While the files move, the window is locked and the button reads
   **STOP**, which stops after the current file. The report at the end says how
   many files moved, which ones were left in place and why, and where the
   journal is.

*Include subfolders*, *Matching* and *Sensitivity* change what a scan finds, so
changing any of them searches the folder again and rebuilds the list of
duplicates on the spot. Nothing happens before the first scan, and a change made
while a scan is running simply applies to the next one.

The *File* menu carries the same commands, with the usual shortcuts:
F5 to scan, Ctrl+Shift+M to move, Ctrl+E to export. Every field and
check box has an access key (Alt+F for the folder to scan, and so on).

**SCAN** is the window's default button (Enter key); it reads *CANCEL* while a
scan runs. It and **MOVE ALL** carry a bold label to mark them as the primary
actions. They are deliberately left in the system's own button style: giving a
button a custom background colour makes Windows drop the visual-style rendering
and fall back to a square-cornered classic button, which no longer matches its
neighbours.

*File > Export list to CSV* writes the full inventory (group, file, folder, size,
dimensions, date, action) without changing anything.

Extras: double-click a thumbnail to open the image in the default viewer;
right-click offers "Open containing folder" and "Copy path".

## Safety

A duplicate finder is a program whose job is to take files away, and the ways
such programs have destroyed the only copy of a picture are well documented.
Each rule below answers one of them; `selftest.bat` checks them on your
computer.

**What a scan never looks at** (`FolderWalker.cs`)

- **Links.** A junction or symbolic link to a folder, and a symbolic link to a
  file, are other names for something that lives elsewhere. Followed, they make
  one file look like its own duplicate — and "removing the copy" removes the
  file the kept name points to. They are left out (OneDrive placeholders, which
  are also reparse points but not links, are scanned normally).
- **Second names of one file.** Two hard links are one file under two names:
  identical by definition, yet not a copy. Windows' file index tells them apart
  (`FileIdentity.cs`); the second name is left out of the group.
- **The Recycle Bin and system folders.** `$Recycle.Bin`, `System Volume
  Information` and any folder Explorer hides as a protected system folder
  (Hidden + System). A copy found in the bin could otherwise be chosen as the one
  to keep — and be destroyed the next time the bin is emptied.
- **The destination folder.** Copies already set aside are never scanned again,
  so a keep rule can never pick one of them and send the original after it.
- **Online-only cloud files** (OneDrive "Files On-Demand"). Reading one
  downloads it; they are left out and counted instead.

**What happens just before each file moves** (`DuplicateRemover.cs`)

The scan may be minutes or days old. Right before each duplicate moves, it is
checked again: the kept copy must still exist; the two must be two different
files, not two names of one; in byte matching they must be identical, compared
byte by byte; in visual matching neither may have changed since the scan, they
must not be a RAW+JPEG-style pair, and the duplicate must match the kept copy
directly, with the scan's own strictness. A file that fails stays where it is,
and the final report says why. Moves go to a free name (` (1)`, ` (2)`…):
nothing already in the destination is ever overwritten, and a move that would
land outside the destination folder is refused.

**The Recycle Bin** (`RecycleBin.cs`)

"Move to the Recycle Bin" is only a request: where there is no bin, Windows
deletes for good, and without being asked. That is the case on network drives,
most USB sticks and memory cards, SUBST drives, drives whose bin is set to
"Don't move files to the Recycle Bin", machines where a policy turns it off,
paths longer than 260 characters, and batches larger than the bin. TwinPix
checks all of these first and refuses the whole operation, suggesting a
destination folder instead; Windows is still asked to warn before destroying
anything (`FOF_WANTNUKEWARNING`) as a last net.

**The journal** (`RemovalJournal.cs`)

Every file moved is written, as it moves, to `TwinPix-journal.csv` — in the
destination folder, or in `%APPDATA%\TwinPix` for the Recycle Bin: date, where
the file was, where it went, the copy kept in its place and why the two were
judged the same. If the journal cannot be written, nothing moves.

**Everyday rules**

- One copy of every group is always kept: a group without one is skipped.
- The confirmation's default button is Cancel.
- The window is locked while files move, and cannot be closed half-way.
- Thumbnails are loaded into memory and the file is closed immediately, so no
  lock ever blocks a move.
- Formats without a GDI+ decoder (HEIC, RAW, PSD…) can still be matched by
  bytes; only the preview shows "No preview". The list of scanned extensions can
  be edited in the options bar.
- Folders that cannot be read (insufficient rights) are skipped and counted in
  the status bar.

## Native look

The interface deliberately stays on stock Win32 controls and lets Windows draw
them:

- the group list is themed as Explorer themes its own (`SetWindowTheme`), is
  double-buffered by the control itself, and its **sort arrow is drawn in the
  column header** by the header control, not faked with a text marker;
- each row carries the shell icon of its file type (`SHGetFileInfo`);
- empty folder fields show a grey prompt (`CB_SETCUEBANNER`);
- information and questions use the Vista task dialog — a large question, the
  details below it — and fall back to a message box on older systems;
- the window remembers its size and position between runs, and ignores them if
  the monitor they belonged to is gone.

All of it is optional: each helper in the `Native` class checks the platform and
does nothing when it is not available, so the application still runs (with the
framework's default appearance) anywhere WinForms runs.
