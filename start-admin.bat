@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Starting Frontend Admin...
echo ========================================
echo.
echo   Admin: http://localhost:3000
echo.
echo Press Ctrl+C to stop
echo ========================================
echo.

cd /d "%~dp0"
npm run dev:admin
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
