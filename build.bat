@echo off
echo ========================================
echo  WinNotch - Build Release
echo ========================================
echo.

cd /d "%~dp0"

echo Cleaning previous builds...
if exist "publish" rmdir /s /q "publish"

echo Building self-contained single-file executable...
dotnet publish src\WinNotch\WinNotch.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
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

:: Show file size
for %%A in ("publish\WinNotch.exe") do echo  Size: %%~zA bytes
echo.
pause
