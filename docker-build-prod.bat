@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse — Build production images
echo ========================================
echo.

cd /d "%~dp0"

docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker is not running.
    pause
    exit /b 1
)

set "EXTRA="
set "NOCACHE="
:parse_args
if "%~1"=="" goto run_build
if /i "%~1"=="--no-cache" (
    set "NOCACHE=-NoCache"
    shift
    goto parse_args
)
set "EXTRA=%EXTRA% -Profile %~1"
shift
goto parse_args

:run_build
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\docker-build-prod.ps1" %EXTRA% %NOCACHE%
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
) else (
    echo [OK] Build finished.
)
echo.
pause
exit /b %EXIT_CODE%
