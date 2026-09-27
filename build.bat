@echo off
rem =====================================================================
rem  Builds TwinPix with nothing but the compiler shipped with Windows
rem  (csc.exe from the .NET Framework 4.x, present on every Windows 8+
rem  box and on any Windows 7 with the framework enabled).
rem  No Visual Studio, no SDK, no download required.
rem
rem  Every .cs file next to this script is compiled - the engine
rem  (TwinPix.cs) and the user interface (MainForm.cs, MainForm.Designer.cs,
rem  FileCard.cs, FileCard.Designer.cs). TwinPix.csproj builds those very
rem  same files in Visual Studio; neither build needs the other.
rem
rem  Every .resx file next to this script is embedded as well, under the
rem  name the form's ComponentResourceManager asks for at run time
rem  (TwinPix.<FormName>.resources), so tooltips, strings and pictures
rem  added in the designer work here exactly as they do in Visual Studio.
rem  The conversion is done by tools\ResxToResources.cs, which this script
rem  compiles with the same csc.exe - see the "Designer resources" section
rem  further down.
rem =====================================================================
setlocal enabledelayedexpansion

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

rem ---------------------------------------------------------------------
rem  Designer resources (.resx)
rem
rem  csc.exe cannot read a .resx, and resgen.exe only comes with the SDK.
rem  So the converter is built here, with the compiler already found
rem  above: tools\ResxToResources.cs is a few lines around the framework's
rem  own ResXResourceReader. Each .resx next to this script then becomes
rem  obj\resx\<name>.resources and is embedded as TwinPix.<name>.resources,
rem  which is the name "new ComponentResourceManager(typeof(MainForm))"
rem  looks for. Leave this step out and a form whose designer wrote a
rem  tooltip or an image into its .resx throws
rem  MissingManifestResourceException the moment it is constructed.
rem ---------------------------------------------------------------------
set "NS=TwinPix"
set "RESSRC=%~dp0tools\ResxToResources.cs"
set "RESDIR=%~dp0obj\resx"
set "RESGEN=%RESDIR%\ResxToResources.exe"
set "RESOPT="

if exist "%RESSRC%" (
  if not exist "%RESDIR%" mkdir "%RESDIR%"

  "%CSC%" /nologo /target:exe /out:"%RESGEN%" ^
    /reference:System.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    "%RESSRC%"

  if errorlevel 1 (
    echo.
    echo [ERROR] Could not build the .resx converter ^(tools\ResxToResources.cs^).
    pause
    exit /b 1
  )

  for %%R in ("%~dp0*.resx") do (
    "%RESGEN%" "%%~fR" "%RESDIR%\%%~nR.resources"
    if errorlevel 1 (
      echo.
      echo [ERROR] Could not convert %%~nxR.
      pause
      exit /b 1
    )
    set RESOPT=!RESOPT! /resource:"%RESDIR%\%%~nR.resources",%NS%.%%~nR.resources
    echo Resources: %%~nxR embedded as %NS%.%%~nR.resources
  )
  echo.
) else (
  echo [WARN] tools\ResxToResources.cs not found - designer resources are not embedded.
  echo        A form whose .resx holds a tooltip or a picture will fail to start.
)

rem Visual matching decodes every image down to a 32x32 grey grid. WIC, which
rem ships with WPF, can ask a JPEG decoder for a scaled-down image and stops at
rem one eighth of the resolution instead of unpacking every pixel - the
rem difference between roughly fifty images a second and a few thousand. The
rem assemblies live next to the compiler, so this needs no download either;
rem where they are missing, the build falls back to the GDI+ decoder, which is
rem correct and only slower. "build.bat nowic" forces that fallback.
set "WICOPT="
if exist "%FWDIR%\WPF\PresentationCore.dll" (
  set WICOPT=/define:WIC /lib:"%FWDIR%\WPF" /reference:PresentationCore.dll /reference:WindowsBase.dll /reference:System.Xaml.dll
  echo Visual matching: WIC scaled decoding.
) else (
  echo [WARN] WPF assemblies not found next to the compiler.
  echo        Visual matching will use the slower GDI+ decoder.
)
if /I "%~1"=="nowic" (
  set "WICOPT="
  echo [INFO] WIC skipped on request: GDI+ decoding.
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /out:"%~dp0TwinPix.exe" ^
  %ICONOPT% %MANIFESTOPT% %WICOPT% %RESOPT% ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  "%~dp0*.cs"

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
