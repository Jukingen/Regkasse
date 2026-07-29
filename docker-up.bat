@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Starting Docker Containers
echo ========================================
echo.

echo Checking Docker...
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
echo [OK] Docker is running!
echo.

cd /d "%~dp0"

echo Starting containers...
docker compose up -d
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed to start containers!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================
echo  Containers started!
echo ========================================
echo   API:   http://localhost:5184
echo   Admin: http://localhost:3000
echo   POS:   http://localhost:8081
echo   Sites: http://localhost:3001
echo.
echo To view logs: docker compose logs -f
echo To stop: docker-down.bat
echo ========================================
pause
exit /b 0
