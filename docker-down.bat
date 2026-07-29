@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Stopping Docker Containers
echo ========================================
echo.

cd /d "%~dp0"

where docker >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker CLI not found on PATH!
    echo Install Docker Desktop, then open a new terminal so "docker" is available.
    pause
    exit /b 1
)

docker compose down
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed to stop containers!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================
echo  Containers stopped!
echo ========================================
echo.
echo RAM and CPU freed!
pause
exit /b 0
