@echo off
REM ---------------------------------------------------------------
REM  Builds RCXTowerGUI.exe with the C# compiler that already ships
REM  with Windows (.NET Framework). Nothing to install, nothing to
REM  download.
REM ---------------------------------------------------------------
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find the Windows C# compiler.
    echo Expected at: %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
    pause
    exit /b 1
)

echo Building RCXTowerGUI.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /out:RCXTowerGUI.exe ^
    /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    /resource:assets\rcx.ico,rcx.ico /win32icon:assets\rcx.ico ^
    src\RCXTowerGUI.cs

if errorlevel 1 (
    echo.
    echo *** Build failed. ***
    pause
    exit /b 1
)

echo.
echo Done. RCXTowerGUI.exe is in this folder.
echo Copy it next to rcx_tool.py to use it.
pause
