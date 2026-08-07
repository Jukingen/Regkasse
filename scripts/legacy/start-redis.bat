@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse Redis
color 0B
call "%~dp0_repo.bat"

set "LOG_FILE=%LOG_DIR%\redis.log"
set "REDIS_PATH=%REPO_ROOT%\tools\redis"
set "REDIS_SERVER=%REDIS_PATH%\redis-server.exe"
set "REDIS_CLI=%REDIS_PATH%\redis-cli.exe"
set "REDIS_CONF=%REDIS_PATH%\redis.windows.conf"
set "START_PS1=%REPO_ROOT%\scripts\dev\start-redis-dev.ps1"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Redis Start - %date% %time% >> "%LOG_FILE%"
echo ======================================== >> "%LOG_FILE%"

if not exist "%REDIS_SERVER%" (
    echo [INFO] Redis binary not found, downloading...
    echo [INFO] Redis binary not found, downloading... >> "%LOG_FILE%"
    if not exist "%START_PS1%" (
        echo [ERROR] Script not found: %START_PS1%
        echo [ERROR] Script not found: %START_PS1% >> "%LOG_FILE%"
        echo.
        echo An error occurred. Details: %LOG_FILE%
        echo Press any key to close this window...
        pause > nul
        exit /b 1
    )
    powershell -NoProfile -ExecutionPolicy Bypass -File "%START_PS1%" >> "%LOG_FILE%" 2>&1
    if not exist "%REDIS_SERVER%" (
        echo [ERROR] Redis could not be installed: %REDIS_SERVER%
        echo [ERROR] Redis could not be installed: %REDIS_SERVER% >> "%LOG_FILE%"
        echo.
        echo An error occurred. Details: %LOG_FILE%
        echo Press any key to close this window...
        pause > nul
        exit /b 1
    )
)

if not exist "%REDIS_CONF%" (
    echo [ERROR] Config not found: %REDIS_CONF%
    echo [ERROR] Config not found: %REDIS_CONF% >> "%LOG_FILE%"
    echo.
    echo An error occurred. Details: %LOG_FILE%
    echo Press any key to close this window...
    pause > nul
    exit /b 1
)

cd /d "%REDIS_PATH%"

echo.
echo ========================================
echo    Regkasse Redis Starting...
echo ========================================
echo.
echo Redis yolu: %REDIS_PATH%
echo Log file: %LOG_FILE%
echo Port: 6379
echo.
echo ========================================
echo.
echo Closing this window stops Redis.
echo.

"%REDIS_CLI%" ping >nul 2>&1
if %errorlevel% equ 0 (
    echo [INFO] Redis is already listening on localhost:6379.
    echo %date% %time% - Redis already running on :6379 >> "%LOG_FILE%"
    echo.
    echo You may keep this window open or close it.
    echo ^(This window did not start Redis; another process is running.^)
    echo.
    echo Press any key to close this window...
    pause > nul
    exit /b 0
)

echo %date% %time% - Starting redis-server... >> "%LOG_FILE%"
"%REDIS_SERVER%" "%REDIS_CONF%" >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo [ERROR] Redis failed to start! Exit code: %errorlevel%
    echo ========================================
    echo.
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo See log for details: %LOG_FILE%
    echo.
    echo Press any key to close this window...
    pause > nul
) else (
    echo.
    echo ========================================
    echo Redis stopped.
    echo ========================================
    echo.
    echo %date% %time% - Redis stopped >> "%LOG_FILE%"
    echo.
    echo Press any key to close this window...
    pause > nul
)
