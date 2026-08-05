@echo off
title Regkasse Docker Status
color 0B

:: Log klasoru
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_status.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Status - %date% %time% >> "%LOG_FILE%"
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
echo    Regkasse Docker Durumu
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

echo %date% %time% - Checking container status... >> "%LOG_FILE%"

echo --- docker compose ps ---
echo --- docker compose ps --- >> "%LOG_FILE%"
docker compose ps
docker compose ps >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [HATA] docker compose ps basarisiz!
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo Detaylar icin log dosyasina bakin: %LOG_FILE%
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b %errorlevel%
)

echo.
echo --- regkasse containers ---
echo --- regkasse containers --- >> "%LOG_FILE%"
docker ps --filter "name=regkasse" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
docker ps --filter "name=regkasse" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" >> "%LOG_FILE%" 2>&1

echo.
echo ========================================
echo    Durum kontrolu tamamlandi
echo ========================================
echo.
echo Log: %LOG_FILE%
echo ========================================
echo.
echo Bu pencereyi kapatmak icin bir tusa basin...
pause > nul
exit /b 0
