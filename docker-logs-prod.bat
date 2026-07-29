@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse — Production Docker logs
echo ========================================
echo.
echo  Usage: docker-logs-prod.bat [service]
echo  Services: postgres redis backend frontend-admin frontend-sites frontend
echo  Ctrl+C to stop following.
echo.

cd /d "%~dp0"

set "SERVICE_ARG="
if not "%~1"=="" set "SERVICE_ARG=-Service %~1"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\docker-logs-prod.ps1" %SERVICE_ARG%
set "EXIT_CODE=%ERRORLEVEL%"

echo.
exit /b %EXIT_CODE%
