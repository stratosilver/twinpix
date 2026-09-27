@echo off
rem =====================================================================
rem  Builds and runs the TwinPix self-test (tools\SelfTest.cs) with the
rem  compiler shipped with Windows, and the same options as build.bat.
rem
rem  The self-test checks the safety rules of the engine on this very
rem  computer - what a scan leaves out (Recycle Bin, links, junctions,
rem  the destination folder) and what the removal refuses to touch. It
rem  works in a temporary folder of its own, deletes it at the end, and
rem  never looks at your pictures.
rem
rem  Run it after any change to the engine: every line must read "ok" or
rem  "skip", and the last one "ALL SAFETY CHECKS PASSED".
rem =====================================================================
setlocal

set "CSC="
set "FWDIR="
for %%D in (
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
  "%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
) do (
  if not defined CSC if exist "%%~D\csc.exe" (
    set "CSC=%%~D\csc.exe"
    set "FWDIR=%%~D"
  )
)
if not defined CSC (
  echo [ERROR] csc.exe not found under %WINDIR%\Microsoft.NET
  pause
  exit /b 1
)

rem Same decoder as the application: WIC when the WPF assemblies are there.
set "WICOPT="
if exist "%FWDIR%\WPF\PresentationCore.dll" (
  set WICOPT=/define:WIC /lib:"%FWDIR%\WPF" /reference:PresentationCore.dll /reference:WindowsBase.dll /reference:System.Xaml.dll
)

set "OUT=%~dp0obj\selftest"
if not exist "%OUT%" mkdir "%OUT%"

rem Every source of the application plus the self-test; /main picks the
rem self-test's entry point instead of the application's.
"%CSC%" /nologo /target:exe /platform:anycpu /warn:4 /main:TwinPix.SelfTest ^
  /out:"%OUT%\TwinPixSelfTest.exe" %WICOPT% ^
  /reference:System.dll /reference:System.Core.dll ^
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  "%~dp0*.cs" "%~dp0tools\SelfTest.cs"
if errorlevel 1 (
  echo.
  echo [ERROR] The self-test could not be built.
  pause
  exit /b 1
)

"%OUT%\TwinPixSelfTest.exe"
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
