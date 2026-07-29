@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0docker-down-prod.ps1" %*
exit /b %ERRORLEVEL%
