@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse - Port + Redis Temizleyici
color 0A

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%kill-ports.ps1"

echo ========================================
echo    Regkasse Port + Redis Temizleyici
echo ========================================
echo.
echo Portlar: 5184 (API), 8081 (POS), 3000 (FA), 6379 (Redis)
echo.

if exist "%PS_SCRIPT%" (
    echo PowerShell scripti calistiriliyor...
    echo.
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
) else (
    echo [HATA] kill-ports.ps1 bulunamadi!
    echo Aranan konum: %PS_SCRIPT%
    echo.
    echo Lutfen scripti batch dosyasi ile ayni klasore koyun.
)

echo.
pause
