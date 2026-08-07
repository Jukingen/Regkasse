@echo off
title Regkasse Docker Status
color 0B

:: Log directory
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_status.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Status - %date% %time% >> "%LOG_FILE%"
echo ======================================== >> "%LOG_FILE%"

:: Project root (scripts\docker\host -> repo root)
for %%I in ("%~dp0..\..\..") do set "PROJECT_ROOT=%%~fI"

if not exist "%PROJECT_ROOT%" (
    echo [ERROR] Folder not found: %PROJECT_ROOT%
    echo [ERROR] Folder not found: %PROJECT_ROOT% >> "%LOG_FILE%"
    echo.
    echo Press any key to close this window...
    pause > nul
    exit /b 1
)

cd /d "%PROJECT_ROOT%"

echo.
echo ========================================
echo    Regkasse Docker Status
echo ========================================
echo.
echo Project path: %PROJECT_ROOT%
echo Log file: %LOG_FILE%
echo.

:: Check Docker CLI + engine (installed vs not running)
call "%~dp0_require-docker.bat"
if errorlevel 1 (
    echo %date% %time% - ERROR: Docker CLI missing or engine down >> "%LOG_FILE%"
    echo.
    echo Press any key to close this window...
    pause > nul
    exit /b %errorlevel%
)

echo [OK] Docker is running!
echo.

echo %date% %time% - Checking container status... >> "%LOG_FILE%"

echo --- docker compose ps ---
echo --- docker compose ps --- >> "%LOG_FILE%"
docker compose ps
docker compose ps >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] docker compose ps failed!
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo See log for details: %LOG_FILE%
    echo.
    echo Press any key to close this window...
    pause > nul
    exit /b %errorlevel%
)

echo.
echo --- regkasse containers ---
echo --- regkasse containers --- >> "%LOG_FILE%"
docker ps --filter "name=regkasse" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
docker ps --filter "name=regkasse" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" >> "%LOG_FILE%" 2>&1

echo.
echo ========================================
echo    Durum kontrolu tamamlandi
echo ========================================
echo.
echo Log: %LOG_FILE%
echo ========================================
echo.
echo Press any key to close this window...
pause > nul
exit /b 0
