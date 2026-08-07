@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse Frontend Admin
color 0A
call "%~dp0_repo.bat"

set "LOG_FILE=%LOG_DIR%\frontend-FA.log"
set "PROJECT_PATH=%REPO_ROOT%\frontend-admin"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Frontend Admin Start - %date% %time% >> "%LOG_FILE%"
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
echo    Regkasse Frontend Admin Starting...
echo ========================================
echo.
echo Project path: %PROJECT_PATH%
echo Log file: %LOG_FILE%
echo.
echo ========================================
echo.

if not exist "node_modules" (
    echo [WARN] node_modules not found!
    echo %date% %time% - WARNING: node_modules not found >> "%LOG_FILE%"
)

echo %date% %time% - Starting admin... >> "%LOG_FILE%"
npm run dev >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo [ERROR] Frontend Admin failed to start! Exit code: %errorlevel%
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
    echo Frontend Admin durduruldu.
    echo ========================================
    echo.
    echo %date% %time% - Admin stopped >> "%LOG_FILE%"
    echo.
    echo Press any key to close this window...
    pause > nul
)
