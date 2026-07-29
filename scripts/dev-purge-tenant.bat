@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Development-only catalog purge via API. See scripts\dev-purge-tenant-catalog.ps1

cd /d "%~dp0.."
set "PS_SCRIPT=%~dp0dev-purge-tenant-catalog.ps1"

echo Purging Tenant Catalog...
echo.
echo This will delete all test data for the 'dev' tenant ^(Development only^).
echo.
set /p confirm="Are you sure? (y/N): "
if /i not "%confirm%"=="y" (
    echo Cancelled.
    pause
    exit /b 0
)

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
