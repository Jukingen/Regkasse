@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse — Stop production Docker
echo ========================================
echo.

cd /d "%~dp0"

set "EXTRA="
if /i "%~1"=="-Volumes" set "EXTRA=-Volumes"
if /i "%~1"=="--volumes" set "EXTRA=-Volumes"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\docker-down-prod.ps1" %EXTRA%
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
) else (
    echo [OK] Stopped.
)
echo.
pause
exit /b %EXIT_CODE%
