@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Regenerates Prüftool fixtures. See scripts\generate-dep-export-fixtures.ps1

cd /d "%~dp0.."
set "PS_SCRIPT=%~dp0generate-dep-export-fixtures.ps1"

echo Generating DEP Export Fixtures...
echo.

if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -OutputDir "%~1"
)
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
    pause
    exit /b %EXIT_CODE%
)

echo Done! See backend/Tests/fixtures/
pause
exit /b 0
