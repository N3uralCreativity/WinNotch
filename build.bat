@echo off
echo ========================================
echo  WinNotch v0.3.1 - Build Release
echo ========================================
echo.

cd /d "%~dp0"

echo Cleaning previous builds...
if exist "publish" rmdir /s /q "publish"

echo.
echo [1/2] Building self-contained single-file executable...
dotnet publish src\WinNotch\WinNotch.csproj ^
    -c Release ^
    -o publish

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Build complete!
echo  Output: publish\WinNotch.exe
echo ========================================
for %%A in ("publish\WinNotch.exe") do echo  Size: %%~zA bytes
echo.

:: Build installer if Inno Setup is available
set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if defined ISCC (
    echo [2/2] Building installer with Inno Setup...
    "%ISCC%" installer\WinNotch.iss
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo INSTALLER BUILD FAILED!
        pause
        exit /b 1
    )
    echo.
    echo ========================================
    echo  Installer created!
    echo  Output: publish\installer\WinNotch-0.3.1-Setup.exe
    echo ========================================
    for %%A in ("publish\installer\WinNotch-0.3.1-Setup.exe") do echo  Size: %%~zA bytes
) else (
    echo [2/2] Inno Setup not found - skipping installer.
    echo  Install from: https://jrsoftware.org/isinfo.php
    echo  Then re-run this script to build the installer.
)

echo.
echo Done! Release artifacts:
echo   - publish\WinNotch.exe          (standalone)
if defined ISCC echo   - publish\installer\WinNotch-0.1.0-Setup.exe (installer)
echo.
pause
