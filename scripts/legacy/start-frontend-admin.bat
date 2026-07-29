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
    echo [HATA] Klasor bulunamadi: %PROJECT_PATH%
    echo [HATA] Klasor bulunamadi: %PROJECT_PATH% >> "%LOG_FILE%"
    echo.
    echo Bir hata olustu. Detaylar icin: %LOG_FILE%
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b 1
)

cd /d "%PROJECT_PATH%"

echo.
echo ========================================
echo    Regkasse Frontend Admin Baslatiliyor...
echo ========================================
echo.
echo Proje yolu: %PROJECT_PATH%
echo Log dosyasi: %LOG_FILE%
echo.
echo ========================================
echo.

if not exist "node_modules" (
    echo [UYARI] node_modules bulunamadi!
    echo %date% %time% - WARNING: node_modules not found >> "%LOG_FILE%"
)

echo %date% %time% - Starting admin... >> "%LOG_FILE%"
npm run dev >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo [HATA] Frontend Admin baslatilamadi! Hata kodu: %errorlevel%
    echo ========================================
    echo.
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo Detaylar icin log dosyasina bakin: %LOG_FILE%
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
) else (
    echo.
    echo ========================================
    echo Frontend Admin durduruldu.
    echo ========================================
    echo.
    echo %date% %time% - Admin stopped >> "%LOG_FILE%"
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
)
