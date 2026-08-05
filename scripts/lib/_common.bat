@echo off
:: Shared helpers for Regkasse batch scripts.
:: Usage:
::   call "%~dp0_common.bat" check_error
::   call "%~dp0_common.bat" success "Operation completed"
::   call "%~dp0_common.bat" fail "Build failed" 1
::
:: ANSI colors work in Windows Terminal / modern consoles; ignored elsewhere.

if /i "%~1"=="check_error" goto :check_error
if /i "%~1"=="success" goto :success
if /i "%~1"=="fail" goto :fail
if /i "%~1"=="info" goto :info
if /i "%~1"=="warn" goto :warn
goto :eof

:check_error
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Command failed with error code %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)
goto :eof

:success
echo [SUCCESS] %~2
goto :eof

:fail
echo [ERROR] %~2
if not "%~3"=="" (
    pause
    exit /b %~3
)
pause
exit /b 1

:info
echo [INFO] %~2
goto :eof

:warn
echo [WARN] %~2
goto :eof
