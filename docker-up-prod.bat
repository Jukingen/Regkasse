@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse — Production Docker (local)
echo ========================================
echo.
echo  Full stack: Postgres + Redis + API + Admin + Sites + POS
echo  Soft TSE is OFF (Device/Real). Fill Fiskaly in .env.production.
echo.

cd /d "%~dp0"

docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker is not running. Start Docker Desktop first.
    pause
    exit /b 1
)

REM Pass-through: -ApiOnly | -NoBuild | -SkipConfirm | profile overrides via PowerShell
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\docker-up-prod.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
) else (
    echo [OK] See URLs above. Stop with docker-down-prod.bat
)
echo.
pause
exit /b %EXIT_CODE%
