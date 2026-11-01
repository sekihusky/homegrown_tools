@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-video-trimmer.ps1"
if errorlevel 1 (echo Build failed. & pause & exit /b 1)
echo Build completed: release\VideoTrimmer
pause
