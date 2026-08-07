@echo off
setlocal EnableExtensions
title Regkasse Docker Clean - DANGER
color 0C

:: *** DANGER *** Wipes Compose volumes (Postgres/Redis data) and prunes unused images.

:: Log directory
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_clean.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Clean DANGER - %date% %time% >> "%LOG_FILE%"
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
echo    DANGER: Docker Clean ^(Full Reset^)
echo ========================================
echo.
echo Project path: %PROJECT_ROOT%
echo Log file: %LOG_FILE%
echo.
echo WARNING: This removes ALL containers, volumes, and unused images!
echo          Postgres/Redis data in Compose volumes will be DELETED!
echo.
set /p confirm="Type YES to continue: "
if /i not "%confirm%"=="YES" (
    echo Cancelled.
    echo %date% %time% - Clean cancelled by user >> "%LOG_FILE%"
    echo.
    echo Press any key to close this window...
    pause > nul
    exit /b 0
)

:: Check Docker CLI + engine
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

echo %date% %time% - Cleaning Docker stack... >> "%LOG_FILE%"

echo.
echo Removing containers and volumes...
docker compose --profile pos --profile sites down -v >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [WARN] Some containers may already be stopped.
    echo %date% %time% - WARNING: compose down -v exit %errorlevel% >> "%LOG_FILE%"
)

echo.
echo Pruning unused images...
docker system prune -f >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] docker system prune failed!
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo See log for details: %LOG_FILE%
    echo.
    echo Press any key to close this window...
    pause > nul
    exit /b %errorlevel%
)

echo.
echo ========================================
echo    Docker Clean finished
echo ========================================
echo.
echo Containers, volumes, and unused images were removed.
echo.
echo Log: %LOG_FILE%
echo ========================================
echo.
echo %date% %time% - Clean completed >> "%LOG_FILE%"
echo Press any key to close this window...
pause > nul
exit /b 0
