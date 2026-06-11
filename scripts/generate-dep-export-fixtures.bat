@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Regenerates backend\Tests\fixtures\prueftool\dep-export.json and crypto-material.json

cd /d "%~dp0.."
set "PS_SCRIPT=%~dp0generate-dep-export-fixtures.ps1"
set "LOG=%TEMP%\regkasse-generate-dep-fixtures-%RANDOM%.log"

echo ========================================
echo  Generate DEP Prueftool Fixtures
echo ========================================
echo  Log: %LOG%
echo.

if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" > "%LOG%" 2>&1
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -OutputDir "%~1" > "%LOG%" 2>&1
)

set "EXIT_CODE=%ERRORLEVEL%"

type "%LOG%"
echo.
echo ----------------------------------------
if %EXIT_CODE% equ 0 (
    echo [OK] Fixture generation finished. Exit code: 0
) else (
    echo [FAILED] Fixture generation failed. Exit code: %EXIT_CODE%
    echo Full log saved to: %LOG%
)
echo ----------------------------------------
echo.

pause
exit /b %EXIT_CODE%
