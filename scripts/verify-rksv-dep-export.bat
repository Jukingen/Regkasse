@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM BMF Prueftool DEP verification wrapper.
REM Usage:
REM   verify-rksv-dep-export.bat
REM       -> uses committed fixtures (-UseFixtures)
REM   verify-rksv-dep-export.bat "path\to\dep-export.json" "path\to\crypto-material.json"
REM   verify-rksv-dep-export.bat -DetailedOutput
REM       -> fixtures + detailed Java output

cd /d "%~dp0.."
set "PS_SCRIPT=%~dp0verify-rksv-dep-export.ps1"
set "LOG=%TEMP%\regkasse-verify-dep-%RANDOM%.log"
set "EXTRA_ARGS="

if /I "%~1"=="-DetailedOutput" (
    set "EXTRA_ARGS=-DetailedOutput"
    goto :run_fixtures
)
if /I "%~1"=="/DetailedOutput" (
    set "EXTRA_ARGS=-DetailedOutput"
    goto :run_fixtures
)

if "%~1"=="" goto :run_fixtures

if "%~2"=="" (
    echo [ERROR] Provide both DEP and crypto-material paths, or no arguments for -UseFixtures.
    echo.
    echo Examples:
    echo   %~nx0
    echo   %~nx0 ".\dep-export.json" ".\crypto-material.json"
    echo   %~nx0 -DetailedOutput
    pause
    exit /b 1
)

echo ========================================
echo  BMF DEP Prueftool Verification
echo ========================================
echo  DEP:    %~1
echo  Crypto: %~2
echo  Log:    %LOG%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -DepExportPath "%~1" -CryptoMaterialPath "%~2" %EXTRA_ARGS% > "%LOG%" 2>&1
goto :show_result

:run_fixtures
echo ========================================
echo  BMF DEP Prueftool Verification
echo  Mode: committed fixtures (-UseFixtures)
echo ========================================
echo  Log: %LOG%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -UseFixtures %EXTRA_ARGS% > "%LOG%" 2>&1

:show_result
set "EXIT_CODE=%ERRORLEVEL%"

type "%LOG%"
echo.
echo ----------------------------------------
if %EXIT_CODE% equ 0 (
    echo [OK] DEP verification finished successfully. Exit code: 0
) else (
    echo [FAILED] DEP verification failed. Exit code: %EXIT_CODE%
    echo Full log saved to: %LOG%
)
echo ----------------------------------------
echo.

pause
exit /b %EXIT_CODE%
