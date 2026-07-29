@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Removes corrupted backend bin/obj outputs. See scripts\clean-backend-build.ps1

cd /d "%~dp0.."
set "PS_SCRIPT=%~dp0clean-backend-build.ps1"

echo Cleaning Backend Build...
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
