@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse - Port + Redis Cleaner
color 0A

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%kill-ports.ps1"

echo ========================================
echo    Regkasse Port + Redis Cleaner
echo ========================================
echo.
echo Portlar: 5184 (API), 8081 (POS), 3000 (FA), 6379 (Redis)
echo.

if exist "%PS_SCRIPT%" (
    echo PowerShell scripti calistiriliyor...
    echo.
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
) else (
    echo [ERROR] kill-ports.ps1 not found!
    echo Aranan konum: %PS_SCRIPT%
    echo.
    echo Place the script in the same folder as this batch file.
)

echo.
pause
