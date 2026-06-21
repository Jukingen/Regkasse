@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Kullanici adi unuttum simulasyonu (localhost)
REM   scripts\dev-mail-test.bat
REM
REM E-posta sorar, kayitliysa kullanici adini gosterir.
REM Mail dosyaya yazilir (gercek kutuya gitmez).

cd /d "%~dp0.."

echo.
echo ========================================
echo  Kullanici Adi Unuttum - Yerel Test
echo ========================================
echo.
echo  Localhost'ta gercek e-posta kutusuna mail GITMEZ.
echo  Kayit varsa kullanici adi asagida gosterilir.
echo  Mail ayrica: backend\App_Data\dev-mail\
echo.
echo  Backend acik olmali: cd backend ^&^& dotnet run
echo.

set "EMAIL="
set /p "EMAIL=E-posta adresini girin: "

if "%EMAIL%"=="" (
    echo.
    echo [HATA] E-posta adresi bos birakilamaz.
    echo.
    pause
    exit /b 1
)

echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0test-forgot-username-email.ps1" -Email "%EMAIL%"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
pause
exit /b %EXIT_CODE%
