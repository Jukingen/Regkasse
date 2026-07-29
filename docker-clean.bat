@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Docker Clean (Full Reset)
echo ========================================
echo.
echo WARNING: This will remove ALL containers, volumes, and unused images!
echo This means ALL Compose volume data will be lost!
echo.
set /p confirm="Are you sure? (y/N): "
if /i not "%confirm%"=="y" (
    echo Cancelled.
    pause
    exit /b 0
)

cd /d "%~dp0"

where docker >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker CLI not found on PATH!
    echo Install Docker Desktop, then open a new terminal so "docker" is available.
    pause
    exit /b 1
)

echo.
echo Stopping and removing containers...
docker compose down -v
if %ERRORLEVEL% neq 0 (
    echo [WARNING] Some containers may not have been running
)

echo.
echo Removing unused images...
docker system prune -f
if %ERRORLEVEL% neq 0 (
    echo [ERROR] docker system prune failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================
echo  Docker clean complete!
echo ========================================
echo.
echo All containers, volumes, and unused images removed.
pause
exit /b 0
