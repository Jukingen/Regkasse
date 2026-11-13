@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse Development Environment
echo ========================================
echo.
echo Default (RAM-safe): API + Admin
echo Full stack:         use start-dev-all.bat or npm run dev:all
echo.
echo   API:   http://localhost:5184
echo   Admin: http://localhost:3000
echo.
echo Press Ctrl+C to stop all services
echo ========================================
echo.

cd /d "%~dp0..\.."
npm run dev
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] npm run dev exited with code %EXIT_CODE%
) else (
    echo [OK] Dev processes stopped.
)
pause
exit /b %EXIT_CODE%
