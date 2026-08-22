@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0revert-backup-fake.ps1" %*
exit /b %ERRORLEVEL%
