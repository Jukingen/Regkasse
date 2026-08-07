@echo off
:: Shared Docker CLI/engine gate for scripts\docker\host\*.bat
:: Usage: call "%~dp0_require-docker.bat"
:: Exit 0 = OK, 1 = not installed / not on PATH, 2 = engine not running

setlocal EnableExtensions

where docker >nul 2>&1
if errorlevel 1 (
    echo.
    echo ========================================
    echo [ERROR] Docker Desktop is not installed ^(or not on PATH^).
    echo ========================================
    echo.
    echo The `docker` command was not found. Compose stacks cannot start
    echo until Docker Desktop is installed on this machine.
    echo.
    echo Install ^(Admin PowerShell^):
    echo   1^) wsl --install
    echo   2^) Reboot
    echo   3^) winget install --id Docker.DockerDesktop -e
    echo   4^) Start Docker Desktop and wait for Engine running
    echo   5^) Open a new terminal, then:
    echo      scripts\docker\host\up.bat
    echo.
    echo Diagnose:  powershell -File scripts\docker\docker-diagnose.ps1
    echo Guide:     docs\DOCKER_WINDOWS_SETUP.md
    echo.
    echo Without Docker: scripts\dev\start.bat -^> [1] Legacy
    echo   or: scripts\dev\start-dev.bat
    echo.
    endlocal & exit /b 1
)

docker info >nul 2>&1
if errorlevel 1 (
    echo.
    echo ========================================
    echo [ERROR] Docker Desktop is installed but the engine is not running.
    echo ========================================
    echo.
    echo Start Docker Desktop and wait until Engine is running,
    echo then run this script again.
    echo.
    echo If stuck: wsl --shutdown  ^(then restart Docker Desktop^)
    echo Diagnose: powershell -File scripts\docker\docker-diagnose.ps1
    echo.
    endlocal & exit /b 2
)

endlocal & exit /b 0
