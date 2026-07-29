@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse — Push production images
echo ========================================
echo.
echo  Requires: docker login
echo  Set DOCKER_REGISTRY in .env.production, or pass -Registry to the script.
echo.
echo  Examples:
echo    docker-push-prod.bat
echo    powershell -File scripts\docker-push-prod.ps1 -Registry ghcr.io/org -Profile admin
echo.

cd /d "%~dp0"

docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker is not running.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\docker-push-prod.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
) else (
    echo [OK] Push finished.
)
echo.
pause
exit /b %EXIT_CODE%
