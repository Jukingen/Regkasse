@echo off
setlocal EnableExtensions
chcp 65001 >nul

cd /d "%~dp0"
echo Running ensure-docker-desktop.ps1...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ensure-docker-desktop.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
) else (
    echo [OK] Done.
)
echo.
pause
exit /b %EXIT_CODE%
