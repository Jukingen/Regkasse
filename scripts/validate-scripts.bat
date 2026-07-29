@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Validates .bat/.ps1 pairing + docs/SCRIPTS_REFERENCE.md coverage.
REM See docs\SCRIPTS_TEST_PLAN.md

cd /d "%~dp0.."

echo ========================================
echo  Regkasse validate-scripts
echo ========================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0validate-scripts.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Validation failed. Exit code: %EXIT_CODE%
) else (
    echo [OK] Validation passed.
)
pause
exit /b %EXIT_CODE%
