@echo off
setlocal EnableExtensions
chcp 65001 >nul

REM Ant Design 6 deprecation fixer (Node). See scripts\fix-antd-deprecations.mjs

cd /d "%~dp0..\.."

echo Fixing Ant Design Deprecations...
echo.

node "scripts\fix-antd-deprecations.mjs" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
    pause
    exit /b %EXIT_CODE%
)

echo Done!
pause
exit /b 0
