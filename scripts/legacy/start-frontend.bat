@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse Frontend POS
color 0A
call "%~dp0_repo.bat"

set "LOG_FILE=%LOG_DIR%\frontend_FE.log"
set "PROJECT_PATH=%REPO_ROOT%\frontend"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Frontend POS Start - %date% %time% >> "%LOG_FILE%"
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
echo    Regkasse Frontend POS Starting...
echo ========================================
echo.
echo Project path: %PROJECT_PATH%
echo Log file: %LOG_FILE%
echo.
echo ========================================
echo.

echo %date% %time% - Cleanup orphan Expo/Next workers... >> "%LOG_FILE%"
pushd "%REPO_ROOT%"
call npm run dev:cleanup
popd

if not exist "node_modules" (
    echo [WARN] node_modules not found!
    echo.
    echo %date% %time% - WARNING: node_modules not found >> "%LOG_FILE%"
)

echo %date% %time% - Starting frontend... >> "%LOG_FILE%"
npm start -- --clear >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo [ERROR] Frontend failed to start! Exit code: %errorlevel%
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
    echo Frontend durduruldu.
    echo ========================================
    echo.
    echo %date% %time% - Frontend stopped >> "%LOG_FILE%"
    echo.
    echo Press any key to close this window...
    pause > nul
)
