@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Docker Container Status
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
docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker is not running!
    echo Please start Docker Desktop first.
    pause
    exit /b 1
)

docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo.
echo ========================================
pause
exit /b 0
