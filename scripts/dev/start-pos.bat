@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Starting POS...
echo ========================================
echo.
echo   POS: http://localhost:8081
echo.

echo Press Ctrl+C to stop
echo ========================================
echo.

cd /d "%~dp0..\.."
npm run dev:pos
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
