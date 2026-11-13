@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Forgot-username simulation (localhost)
REM   scripts\dev\dev-mail-test.bat
REM
REM Prompts for email; if registered, shows the username.
REM Mail is written to a file (not sent to a real inbox).

cd /d "%~dp0..\.."

echo.
echo ========================================
echo  Forgot Username - Local Test
echo ========================================
echo.
echo  On localhost, mail is NOT sent to a real inbox.
echo  If the account exists, the username is shown below.
echo  Mail is also written to: backend\App_Data\dev-mail\
echo.
echo  Backend must be running: cd backend ^&^& dotnet run
echo.

set "EMAIL="
set /p "EMAIL=Enter email address: "

if "%EMAIL%"=="" (
    echo.
    echo [ERROR] Email address cannot be empty.
    echo.
    pause
    exit /b 1
)

echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\test\test-forgot-username-email.ps1" -Email "%EMAIL%"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
pause
exit /b %EXIT_CODE%
