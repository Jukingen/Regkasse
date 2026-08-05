@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Running All Tests
echo ========================================
echo.

cd /d "%~dp0"

echo [1/3] Backend tests...
dotnet test backend/KasseAPI_Final.sln
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Backend tests failed!
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Backend tests passed!
echo.

echo [2/3] Admin tests...
cd /d "%~dp0frontend-admin"
npm run test
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Admin tests failed!
    pause
    exit /b %ERRORLEVEL%
)
cd /d "%~dp0"
echo [OK] Admin tests passed!
echo.

echo [3/3] POS tests...
cd /d "%~dp0frontend"
npm run test
if %ERRORLEVEL% neq 0 (
    echo [ERROR] POS tests failed!
    pause
    exit /b %ERRORLEVEL%
)
cd /d "%~dp0"
echo [OK] POS tests passed!
echo.

echo ========================================
echo  All tests passed!
echo ========================================
pause
exit /b 0
