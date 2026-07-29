@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse Redis
color 0B
call "%~dp0_repo.bat"

set "LOG_FILE=%LOG_DIR%\redis.log"
set "REDIS_PATH=%REPO_ROOT%\tools\redis"
set "REDIS_SERVER=%REDIS_PATH%\redis-server.exe"
set "REDIS_CLI=%REDIS_PATH%\redis-cli.exe"
set "REDIS_CONF=%REDIS_PATH%\redis.windows.conf"
set "START_PS1=%REPO_ROOT%\scripts\start-redis-dev.ps1"

echo ======================================== > "%LOG_FILE%"
echo Regkasse Redis Start - %date% %time% >> "%LOG_FILE%"
echo ======================================== >> "%LOG_FILE%"

if not exist "%REDIS_SERVER%" (
    echo [BILGI] Redis binary bulunamadi, indiriliyor...
    echo [BILGI] Redis binary bulunamadi, indiriliyor... >> "%LOG_FILE%"
    if not exist "%START_PS1%" (
        echo [HATA] Script bulunamadi: %START_PS1%
        echo [HATA] Script bulunamadi: %START_PS1% >> "%LOG_FILE%"
        echo.
        echo Bir hata olustu. Detaylar icin: %LOG_FILE%
        echo Bu pencereyi kapatmak icin bir tusa basin...
        pause > nul
        exit /b 1
    )
    powershell -NoProfile -ExecutionPolicy Bypass -File "%START_PS1%" >> "%LOG_FILE%" 2>&1
    if not exist "%REDIS_SERVER%" (
        echo [HATA] Redis kurulamadi: %REDIS_SERVER%
        echo [HATA] Redis kurulamadi: %REDIS_SERVER% >> "%LOG_FILE%"
        echo.
        echo Bir hata olustu. Detaylar icin: %LOG_FILE%
        echo Bu pencereyi kapatmak icin bir tusa basin...
        pause > nul
        exit /b 1
    )
)

if not exist "%REDIS_CONF%" (
    echo [HATA] Config bulunamadi: %REDIS_CONF%
    echo [HATA] Config bulunamadi: %REDIS_CONF% >> "%LOG_FILE%"
    echo.
    echo Bir hata olustu. Detaylar icin: %LOG_FILE%
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b 1
)

cd /d "%REDIS_PATH%"

echo.
echo ========================================
echo    Regkasse Redis Baslatiliyor...
echo ========================================
echo.
echo Redis yolu: %REDIS_PATH%
echo Log dosyasi: %LOG_FILE%
echo Port: 6379
echo.
echo ========================================
echo.
echo Pencereyi kapatmak Redis'i durdurur.
echo.

"%REDIS_CLI%" ping >nul 2>&1
if %errorlevel% equ 0 (
    echo [BILGI] Redis zaten localhost:6379 dinliyor.
    echo %date% %time% - Redis already running on :6379 >> "%LOG_FILE%"
    echo.
    echo Bu pencereyi acik tutabilir veya kapatabilirsiniz.
    echo ^(Bu pencere Redis process'ini baslatmadi; ayri process calisiyor.^)
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
    exit /b 0
)

echo %date% %time% - Starting redis-server... >> "%LOG_FILE%"
"%REDIS_SERVER%" "%REDIS_CONF%" >> "%LOG_FILE%" 2>&1

if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo [HATA] Redis baslatilamadi! Hata kodu: %errorlevel%
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
    echo Redis durduruldu.
    echo ========================================
    echo.
    echo %date% %time% - Redis stopped >> "%LOG_FILE%"
    echo.
    echo Bu pencereyi kapatmak icin bir tusa basin...
    pause > nul
)
