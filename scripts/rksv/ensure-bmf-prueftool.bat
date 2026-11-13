@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Downloads BMF Prüftool JARs into backend/Tests/ (gitignored). PowerShell — not a Node script.

cd /d "%~dp0..\.."
set "PS_SCRIPT=%~dp0ensure-bmf-prueftool.ps1"

echo Ensuring BMF Prueftool is installed...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
    pause
    exit /b %EXIT_CODE%
)

echo Done!
pause
exit /b 0
