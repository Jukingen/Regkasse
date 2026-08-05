@echo off
title Regkasse Docker Logs
color 0B

:: Log klasoru
set LOG_DIR=C:\Scripts\logs
set LOG_FILE=%LOG_DIR%\docker_logs.log

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Docker Logs - %date% %time% >> "%LOG_FILE%"
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
echo    Regkasse Docker Loglari
echo ========================================
echo.
echo Proje yolu: %PROJECT_ROOT%
echo Log dosyasi: %LOG_FILE%
echo.
echo Canli loglari izliyorsunuz. Durdurmak icin Ctrl+C.
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

echo %date% %time% - Following Docker logs... >> "%LOG_FILE%"

:: Optional: pass service names, e.g. docker-logs.bat backend
if not "%~1"=="" (
    echo Servis(ler): %*
    echo Servis(ler): %* >> "%LOG_FILE%"
    docker compose logs -f --tail=200 %*
) else (
    docker compose --profile pos --profile sites logs -f --tail=200
)

if %errorlevel% neq 0 (
    echo.
    echo [HATA] Docker logs basarisiz! Hata kodu: %errorlevel%
    echo %date% %time% - ERROR! Exit code: %errorlevel% >> "%LOG_FILE%"
    echo.
    echo Detaylar icin log dosyasina bakin: %LOG_FILE%
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b %errorlevel%
)

echo.
echo %date% %time% - Logs follow ended >> "%LOG_FILE%"
echo.
echo ========================================
echo    Log izleme bitti
echo ========================================
echo.
echo Log kaydi: %LOG_FILE%
echo ========================================
echo.
echo Bu pencereyi kapatmak icin bir tusa basin...
pause > nul
exit /b 0
