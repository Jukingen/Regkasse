@echo off
title Regkasse Docker
color 0B

:: Log klasoru
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Start - %date% %time% >> "%LOG_FILE%"
echo ======================================== >> "%LOG_FILE%"

:: Project root
set PROJECT_ROOT=C:\Users\Juke\local-projects\Regkasse

if not exist "%PROJECT_ROOT%" (
    echo [HATA] Klasor bulunamadi: %PROJECT_ROOT%
    echo [HATA] Klasor bulunamadi: %PROJECT_ROOT% >> "%LOG_FILE%"
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b 1
)

cd /d "%PROJECT_ROOT%"

echo.
echo ========================================
echo    Regkasse Docker Baslatiliyor...
echo ========================================
echo.
echo Proje yolu: %PROJECT_ROOT%
echo Log dosyasi: %LOG_FILE%
echo.

:: Check Docker
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [HATA] Docker calismiyor!
    echo Lutfen Docker Desktop'i baslatin.
    echo %date% %time% - ERROR: Docker not running >> "%LOG_FILE%"
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b 1
)

echo [OK] Docker calisiyor!
echo.

:: Log
echo %date% %time% - Starting Docker containers... >> "%LOG_FILE%"

:: Docker compose up (POS + Sites profiles so advertised URLs exist)
docker compose --profile pos --profile sites up -d >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo [HATA] Docker compose baslatilamadi!
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo Detaylar icin log dosyasina bakin: %LOG_FILE%
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b %errorlevel%
)

echo.
echo ========================================
echo    Docker Baslatildi!
echo ========================================
echo   API:   http://localhost:5184
echo   Admin: http://localhost:3000
echo   POS:   http://localhost:8081
echo   Sites: http://localhost:3001
echo.
echo Log: %LOG_FILE%
echo.
echo Durumu kontrol et: docker-status.bat
echo Durdurmak icin: docker-down.bat
echo ========================================
echo.
echo Bu pencereyi kapatmak icin bir tusa basin...
pause > nul
exit /b 0
