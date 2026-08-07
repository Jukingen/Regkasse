@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse Backend
color 0A
call "%~dp0_repo.bat"

set "LOG_FILE=%LOG_DIR%\backend_BE.log"
set "PROJECT_PATH=%REPO_ROOT%\backend"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Backend Start - %date% %time% >> "%LOG_FILE%"
echo ======================================== >> "%LOG_FILE%"

if not exist "%PROJECT_PATH%" (
    echo [ERROR] Folder not found: %PROJECT_PATH%
    echo [ERROR] Folder not found: %PROJECT_PATH% >> "%LOG_FILE%"
    echo.
    echo An error occurred. Details: %LOG_FILE%
    echo Press any key to close this window...
    pause > nul
    exit /b 1
)

cd /d "%PROJECT_PATH%"

echo.
echo ========================================
echo    Regkasse Backend Starting...
echo ========================================
echo.
echo Project path: %PROJECT_PATH%
echo Log file: %LOG_FILE%
echo.
echo ========================================
echo.

echo %date% %time% - Starting backend... >> "%LOG_FILE%"
dotnet run >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo [ERROR] Backend failed to start! Exit code: %errorlevel%
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
    echo Backend durduruldu.
    echo ========================================
    echo.
    echo %date% %time% - Backend stopped >> "%LOG_FILE%"
    echo.
    echo Press any key to close this window...
    pause > nul
)
