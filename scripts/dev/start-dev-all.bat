@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse Development Environment (ALL)
echo ========================================
echo.
echo Starting: API + Admin + POS + Sites
echo Warning: high RAM — prefer start-dev.bat when possible.
echo.
echo   API:   http://localhost:5184
echo   Admin: http://localhost:3000
echo   POS:   http://localhost:8081
echo   Sites: http://localhost:3001
echo.
echo Press Ctrl+C to stop all services
echo ========================================
echo.

cd /d "%~dp0..\.."
npm run dev:all
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] npm run dev:all exited with code %EXIT_CODE%
) else (
    echo [OK] Dev processes stopped.
)
pause
exit /b %EXIT_CODE%
