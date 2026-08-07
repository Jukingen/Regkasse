@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Starting Backend API...
echo ========================================
echo.
echo   API:     http://localhost:5184
echo   Swagger: http://localhost:5184/swagger
echo.
echo Press Ctrl+C to stop
echo ========================================
echo.

cd /d "%~dp0..\.."
npm run dev:backend
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
