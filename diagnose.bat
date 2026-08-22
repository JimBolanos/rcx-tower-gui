@echo off
REM ---------------------------------------------------------------
REM  Runs the app with a visible console so any error is readable.
REM  Use only if RCXTowerGUI.exe does not open on double-click.
REM  Put this file in the same folder as the app.
REM ---------------------------------------------------------------
setlocal
cd /d "%~dp0"

echo === Folder ===
echo %CD%
echo.
echo === Files the app needs ===
if exist rcx_tool.py     (echo [OK]      rcx_tool.py) else (echo [MISSING] rcx_tool.py)
if exist nqc.exe         (echo [OK]      nqc.exe)      else (echo [note]    nqc.exe not here, will be looked up in PATH)
if exist firm0332.lgo    (echo [OK]      firm0332.lgo) else (echo [note]    firm0332.lgo not in this folder)
if exist RCXTowerGUI.exe (echo [OK]      RCXTowerGUI.exe) else (echo [MISSING] RCXTowerGUI.exe - antivirus may have removed it)
echo.

echo === Installed .NET Framework ===
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2>nul
echo.

echo === Starting the app ===
start /wait "" RCXTowerGUI.exe
echo Exit code: %ERRORLEVEL%
echo.

if exist "%TEMP%\RCXTowerGUI-error.txt" (
    echo === Recorded error ===
    type "%TEMP%\RCXTowerGUI-error.txt"
    echo.
)

echo Copy ALL of this text into a GitHub issue if something went wrong.
pause
