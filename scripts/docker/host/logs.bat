@echo off
title Regkasse Docker Logs
color 0B

:: Log directory
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_logs.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Logs - %date% %time% >> "%LOG_FILE%"
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
echo    Regkasse Docker Logs
echo ========================================
echo.
echo Project path: %PROJECT_ROOT%
echo Log file: %LOG_FILE%
echo.
echo Following live logs. Press Ctrl+C to stop.
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

echo %date% %time% - Following Docker logs... >> "%LOG_FILE%"

:: Optional: pass service names, e.g. docker-logs.bat backend
if not "%~1"=="" (
    echo Servis(ler): %*
    echo Servis(ler): %* >> "%LOG_FILE%"
    docker compose logs -f --tail=200 %*
) else (
    docker compose --profile pos --profile sites logs -f --tail=200
)

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Docker logs failed! Exit code: %errorlevel%
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo See log for details: %LOG_FILE%
    echo.
    echo Press any key to close this window...
    pause > nul
    exit /b %errorlevel%
)

echo.
echo %date% %time% - Logs follow ended >> "%LOG_FILE%"
echo.
echo ========================================
echo    Log izleme bitti
echo ========================================
echo.
echo Log kaydi: %LOG_FILE%
echo ========================================
echo.
echo Press any key to close this window...
pause > nul
exit /b 0
