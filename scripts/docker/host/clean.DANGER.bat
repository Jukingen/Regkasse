@echo off
title Regkasse Docker Clean
color 0C

:: Log klasoru
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_clean.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Clean - %date% %time% >> "%LOG_FILE%"
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
echo    Regkasse Docker Clean (Full Reset)
echo ========================================
echo.
echo Proje yolu: %PROJECT_ROOT%
echo Log dosyasi: %LOG_FILE%
echo.
echo UYARI: Tum container'lar, volume'lar ve kullanilmayan image'lar silinecek!
echo Bu Postgres/Redis verisini de siler!
echo.

set /p confirm="Emin misiniz? (E/H): "
if /i not "%confirm%"=="E" (
    echo Iptal edildi.
    echo %date% %time% - Clean cancelled by user >> "%LOG_FILE%"
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b 0
)

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

echo %date% %time% - Cleaning Docker stack... >> "%LOG_FILE%"

echo.
echo Container'lar ve volume'lar kaldiriliyor...
docker compose --profile pos --profile sites down -v >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [UYARI] Bazi container'lar zaten durmus olabilir.
    echo %date% %time% - WARNING: compose down -v exit %errorlevel% >> "%LOG_FILE%"
)

echo.
echo Kullanilmayan image'lar temizleniyor...
docker system prune -f >> "%LOG_FILE%" 2>&1
if %errorlevel% neq 0 (
    echo [HATA] docker system prune basarisiz!
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
echo    Docker Clean Tamamlandi!
echo ========================================
echo.
echo Container'lar, volume'lar ve kullanilmayan image'lar temizlendi.
echo.
echo Log: %LOG_FILE%
echo ========================================
echo.
echo %date% %time% - Clean completed >> "%LOG_FILE%"
echo Bu pencereyi kapatmak icin bir tusa basin...
pause > nul
exit /b 0
