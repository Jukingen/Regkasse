@echo off
setlocal EnableExtensions
chcp 65001 >nul

:: Run an arbitrary command and append output to logs\run_YYYYMMDD_HHMMSS.log
:: Usage:
::   scripts\run-with-log.bat npm run test
::   scripts\run-with-log.bat powershell -File scripts\run-comprehensive-smoke.ps1

cd /d "%~dp0.."

if "%~1"=="" (
    echo Usage: %~nx0 ^<command^> [args...]
    echo Example: %~nx0 npm run test
    pause
    exit /b 1
)

set "LOG_DIR=%~dp0..\logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

:: Sanitize date/time for filename (locale-independent-ish)
set "TS=%date%_%time%"
set "TS=%TS:/=-%"
set "TS=%TS:\=-%"
set "TS=%TS::=-%"
set "TS=%TS: =0%"
set "TS=%TS:.=-%"
set "LOG_FILE=%LOG_DIR%\run_%TS%.log"

echo Running: %*
echo Log: %LOG_FILE%
echo.
echo Running at %date% %time% > "%LOG_FILE%"
echo Command: %* >> "%LOG_FILE%"
echo. >> "%LOG_FILE%"

%* >> "%LOG_FILE%" 2>&1
set "EXIT_CODE=%ERRORLEVEL%"

type "%LOG_FILE%"
echo.
if %EXIT_CODE% neq 0 (
    echo [ERROR] See log: %LOG_FILE%
    pause
    exit /b %EXIT_CODE%
)

echo [SUCCESS] See log: %LOG_FILE%
pause
exit /b 0
