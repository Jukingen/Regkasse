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
echo Dev mode: webpack ^(cpus=2, heap=4096, avoids Turbopack monorepo RAM leak^)
echo ========================================
echo.

REM Only leaked Turbopack workers — do not kill a live POS / Expo session.
echo [1/2] Cleanup leaked .next/dev/build workers ^(orphans-only^)...
echo %date% %time% - Cleanup orphans-only Next workers... >> "%LOG_FILE%"
pushd "%REPO_ROOT%"
call npm run dev:cleanup:orphans
popd

REM Next.js 16 webpack persists its filesystem cache under .next\dev.
REM Do not delete it on every start — that forces a full recompile and HMR
REM chunk timeouts ("network errors"). Use `npm run dev:clean` when needed.

if not exist "node_modules" (
    echo [WARN] node_modules not found!
    echo %date% %time% - WARNING: node_modules not found >> "%LOG_FILE%"
)

echo [2/2] Starting admin ^(next dev --webpack^)...
echo %date% %time% - Starting admin (webpack)... >> "%LOG_FILE%"
set "NODE_OPTIONS=--max-old-space-size=4096"
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
