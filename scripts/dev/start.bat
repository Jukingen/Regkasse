@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse - Choose Mode
echo ========================================
echo.
echo [1] Legacy Mode (without Docker)
echo [2] Docker Mode
echo [3] Exit
echo.
choice /c 123 /n /m "Select option: "
if errorlevel 3 exit /b 0
if errorlevel 2 goto docker
if errorlevel 1 goto legacy

:legacy
echo Starting Legacy Mode...
call "%~dp0..\legacy\start-all.bat"
goto end

:docker
echo Starting Docker Mode...
call "%~dp0..\docker\host\up.bat"
goto end

:end
