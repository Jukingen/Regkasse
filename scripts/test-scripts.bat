@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Dry-run structural tests for Windows .bat scripts (does not start servers).
REM See docs\SCRIPTS_TEST_PLAN.md

cd /d "%~dp0.."

echo ========================================
echo  Regkasse Scripts Test Plan (dry-run)
echo ========================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0test-scripts.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Script tests failed. Exit code: %EXIT_CODE%
) else (
    echo [OK] Script tests finished.
)
pause
exit /b %EXIT_CODE%
