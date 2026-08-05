@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Full DEP export validation: backend build, unit tests, Prueftool, FA build.

cd /d "%~dp0.."
set "PS_SCRIPT=%~dp0run-verify-dep-export-complete.ps1"
set "LOG=%TEMP%\regkasse-dep-complete-%RANDOM%.log"

echo ========================================
echo  DEP Export Complete Validation
echo ========================================
echo  Log: %LOG%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" > "%LOG%" 2>&1
set "EXIT_CODE=%ERRORLEVEL%"

type "%LOG%"
echo.
echo ----------------------------------------
if %EXIT_CODE% equ 0 (
    echo [OK] All automated checks passed. Exit code: 0
) else (
    echo [FAILED] One or more checks failed. Exit code: %EXIT_CODE%
    echo Full log saved to: %LOG%
)
echo ----------------------------------------
echo.

pause
exit /b %EXIT_CODE%
