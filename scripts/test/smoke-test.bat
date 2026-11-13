@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Lightweight HTTP smoke (API + Admin + POS). For the full suite use:
REM   scripts\run-comprehensive-smoke.bat

cd /d "%~dp0..\.."

echo ========================================
echo  Smoke Tests
echo ========================================
echo.

echo Testing API...
curl -sS http://localhost:5184/api/health
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] API health check failed!
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo [OK] API health check passed!
echo.

echo Testing Admin...
curl -sS -o nul http://localhost:3000/login
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Admin check failed!
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Admin check passed!
echo.

echo Testing POS...
curl -sS -o nul http://localhost:8081/
if %ERRORLEVEL% neq 0 (
    echo [ERROR] POS check failed!
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] POS check passed!
echo.

echo ========================================
echo  All smoke tests passed!
echo ========================================
pause
exit /b 0
