@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Ensures scripts\dev\dev-mail.local.env exists, then runs the interactive forgot-username mail test.

cd /d "%~dp0..\.."

echo Configuring Dev Mail...
echo.

set "ENV_FILE=%~dp0dev-mail.local.env"
set "ENV_EXAMPLE=%~dp0dev-mail.local.env.example"

if not exist "%ENV_FILE%" (
    if exist "%ENV_EXAMPLE%" (
        copy /Y "%ENV_EXAMPLE%" "%ENV_FILE%" >nul
        echo Created scripts\dev\dev-mail.local.env from example.
        echo Edit that file to set DEFAULT_TEST_EMAIL / BASE_URL.
        echo.
    ) else (
        echo [WARN] No example env found at scripts\dev\dev-mail.local.env.example
        echo.
    )
) else (
    echo Using existing scripts\dev\dev-mail.local.env
    echo.
)

call "%~dp0dev-mail-test.bat"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
    pause
    exit /b %EXIT_CODE%
)

echo Done!
pause
exit /b %EXIT_CODE%
