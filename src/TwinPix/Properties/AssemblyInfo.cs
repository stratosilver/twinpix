// =====================================================================
//  TwinPix - settings that apply to the program as a whole
//
//  "Assembly attributes" describe the compiled program itself rather
//  than one of its classes. Visual Studio's templates keep them here,
//  in Properties\AssemblyInfo.cs.
//
//  The map of the source files is in DEVELOPER.md, at the root of the
//  repository - with the C# features this code relies on, each next to
//  its PHP counterpart, and the safety rules every change must keep.
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
