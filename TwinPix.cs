// =====================================================================
//  TwinPix - find and manage duplicate images
//
//  A Windows desktop application (WinForms, C# 5, .NET Framework 4.x)
//  that builds with nothing but the compiler shipped with Windows:
//      %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
//  See build.bat - or open TwinPix.csproj in Visual Studio.
//
//  This file holds what applies to the program as a whole - the
//  "assembly attributes" below - and a map of the source files. Every
//  class lives in a file of its own name, all of them in the TwinPix
//  namespace, all of them next to this file (build.bat compiles *.cs).
//
//  Start-up
//    Program.cs               entry point, last-resort error report
//
//  User interface
//    MainForm.cs              main window: scan, keep rules, removal, export
//    MainForm.Designer.cs     its controls and layout (Visual Studio designer)
//    FileCard.cs              one thumbnail card of the right-hand panel
//    FileCard.Designer.cs     its layout (Visual Studio designer)
//    GroupComparer.cs         sort order of the duplicate-group list
//    FolderHistory.cs         remembered folders and window placement
//    UiStyle.cs               shared fonts, colours and the application icon
//    Thumbnail.cs             preview decoding for the cards
//    Shell.cs                 open a file, show it in Explorer
//
//  Finding duplicates
//    ScanOptions.cs           what the user asked for
//    ScanResult.cs            what a scan found, with its counters
//    ScannedFolder.cs         one folder of the Preferred folders list
//    MatchMode.cs             same bytes, or same picture
//    FolderWalker.cs          which folders and files a scan looks at
//    Scanner.cs               runs a scan, groups identical files
//    FileEntry.cs             one image file
//    DupGroup.cs              the copies of one image
//    Fingerprint.cs           what a picture looks like, in 265 bytes
//    ImageHash.cs             computes fingerprints
//    FingerprintCache.cs      keeps fingerprints between runs
//    VisualMatcher.cs         groups images showing the same picture
//    KeepRule.cs              the rules that choose the copy to keep
//    KeepSelector.cs          applies them
//
//  Removing duplicates safely
//    DuplicateRemover.cs      checks every copy again, then moves it
//    RemovalItem.cs           one copy to remove, and the copy that stays
//    RemovalOutcome.cs        what happened to it
//    RemovalSettings.cs       where the copies go
//    RemovalJournal.cs        CSV log of every file moved
//    RecycleBin.cs            the Recycle Bin, refused where Windows would
//                             delete for good
//    FileIdentity.cs          tells two names of one file from two files
//
//  Windows plumbing and helpers
//    NativeMethods.cs         every Win32 function the program calls
//    Native.cs                the look-and-feel helpers built on them
//    PathHelper.cs            path comparisons, free file names
//    Format.cs                human-readable sizes
//
//  Coming from PHP? DEVELOPER.md walks through the C# features this code
//  relies on, each with its PHP counterpart, and explains the safety
//  rules every change must keep.
// =====================================================================

// PHP note: "#if WIC" is conditional compilation. The lines up to #endif
// only exist when the compiler is given /define:WIC (build.bat does so
// when the WPF assemblies are present; TwinPix.csproj always does).

#if WIC
// The WPF imaging classes used for visual matching (WIC) declare the whole
// process DPI-aware the first time they run. On a screen scaled above 100 %
// Windows then stops enlarging the window, and it shrinks - text included -
// in the middle of a scan. The program is meant to stay DPI-unaware (see
// AutoScaleMode = None in the designer files), so WPF is told to leave the
// setting alone.
[assembly: System.Windows.Media.DisableDpiAwareness]
#endif
