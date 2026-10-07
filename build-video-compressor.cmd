@echo off
setlocal
cd /d "%~dp0"

echo Building VideoCompressor and downloading FFmpeg if needed...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-video-compressor.ps1"
if errorlevel 1 (
    echo.
    echo Build failed. Review the error above.
    pause
    exit /b 1
)

echo.
echo Build completed: release\VideoCompressor
pause
