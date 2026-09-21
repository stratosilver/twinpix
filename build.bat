@echo off
rem =====================================================================
rem  Builds TwinPix with nothing but the compiler shipped with Windows
rem  (csc.exe from the .NET Framework, present on every Windows 7+ box).
rem  No Visual Studio, no SDK, no download required.
rem =====================================================================
setlocal enabledelayedexpansion

set "CSC="
for %%D in (
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
  "%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
  "%WINDIR%\Microsoft.NET\Framework64\v3.5"
  "%WINDIR%\Microsoft.NET\Framework\v3.5"
) do (
  if not defined CSC if exist "%%~D\csc.exe" set "CSC=%%~D\csc.exe"
)

if not defined CSC (
  echo.
  echo [ERROR] csc.exe not found under %WINDIR%\Microsoft.NET
  echo Enable the .NET Framework 4.x in "Windows Features".
  echo.
  pause
  exit /b 1
)

echo Compiler: %CSC%
echo.

rem Application icon: used for the executable itself and embedded as a
rem resource so the window and the taskbar show it too. Optional.
set "ICON=%~dp0assets\twinpix.ico"
set "ICONOPT="
if exist "%ICON%" (
  set ICONOPT=/win32icon:"%ICON%" /resource:"%ICON%",TwinPix.twinpix.ico
) else (
  echo [WARN] assets\twinpix.ico not found - building without an icon.
)

rem Manifest: pulls in version 6 of the common controls, so the controls are
rem themed and the Vista task dialog is available. Optional as well.
set "MANIFEST=%~dp0assets\TwinPix.manifest"
set "MANIFESTOPT="
if exist "%MANIFEST%" (
  set MANIFESTOPT=/win32manifest:"%MANIFEST%"
) else (
  echo [WARN] assets\TwinPix.manifest not found - dialogs fall back to message boxes.
)

rem "build.bat nomanifest" builds without it, to check whether a manifest
rem Windows refuses is what stops the program from starting.
if /I "%~1"=="nomanifest" (
  set "MANIFESTOPT="
  echo [INFO] Manifest skipped on request.
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /out:"%~dp0TwinPix.exe" ^
  %ICONOPT% %MANIFESTOPT% ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  "%~dp0TwinPix.cs"

if errorlevel 1 (
  echo.
  echo [ERROR] Build failed.
  pause
  exit /b 1
)

echo.
echo OK: "%~dp0TwinPix.exe"
echo.
choice /c YN /n /m "Run the application now? [Y/N] "
if errorlevel 2 goto :end
start "" "%~dp0TwinPix.exe"

:end
endlocal
