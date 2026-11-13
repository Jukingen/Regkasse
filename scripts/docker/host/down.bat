@echo off
title Regkasse Docker Down
color 0C

:: Log directory
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_down.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Down - %date% %time% >> "%LOG_FILE%"
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
echo    Regkasse Docker Stopping...
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

:: Log
echo %date% %time% - Stopping Docker containers... >> "%LOG_FILE%"

:: Stop all profiles that may have been started
docker compose --profile pos --profile sites down >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo [ERROR] Docker Compose failed to stop!
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
echo    Docker stopped!
echo ========================================
echo.
echo Containers stopped. Volumes were kept.
echo To wipe volumes (DANGER): scripts\docker\host\clean.DANGER.bat
echo.
echo Log: %LOG_FILE%
echo ========================================
echo.
echo Press any key to close this window...
pause > nul
exit /b 0
