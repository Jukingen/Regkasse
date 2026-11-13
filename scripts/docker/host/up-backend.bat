@echo off
title Regkasse Docker Backend
color 0B

:: Log directory
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_backend.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Backend Start - %date% %time% >> "%LOG_FILE%"
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
echo    Docker Backend Starting...
echo ========================================
echo.
echo Project path: %PROJECT_ROOT%
echo Log file: %LOG_FILE%
echo   API: http://localhost:5184
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

:: Warn if host already uses Compose ports (Legacy stack / local Postgres)
for %%P in (5432 6379 5184 3000 8081 3001) do (
    netstat -ano | findstr /R /C:":%%P .*LISTENING" >nul 2>&1
    if not errorlevel 1 (
        echo [WARN] Port %%P is already listening - Docker Compose "port is already allocated" error.
        echo          Stop the Legacy/npm stack first, or change ports in .env.
        echo %date% %time% - WARN: port %%P already listening >> "%LOG_FILE%"
    )
)
echo.

echo %date% %time% - Starting postgres redis backend... >> "%LOG_FILE%"

docker compose up -d postgres redis backend >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo [ERROR] Docker backend failed to start!
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
echo    Docker Backend started!
echo ========================================
echo   API:      http://localhost:5184
echo   Postgres: localhost:5432
echo   Redis:    localhost:6379
echo.
echo Log: %LOG_FILE%
echo ========================================
echo.
echo Press any key to close this window...
pause > nul
exit /b 0
