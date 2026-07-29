@echo off
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0_repo.bat"

echo ========================================
echo  Regkasse Legacy Mode — starting all
echo ========================================
echo.
echo   Logs: %LOG_DIR%
echo   Repo: %REPO_ROOT%
echo.
echo Opens separate windows for Redis, Backend, POS, Admin.
echo ========================================
echo.

start "Regkasse Redis" cmd /k "call \"%~dp0start-redis.bat\""
timeout /t 2 /nobreak >nul
start "Regkasse Backend" cmd /k "call \"%~dp0start-backend.bat\""
timeout /t 1 /nobreak >nul
start "Regkasse Frontend POS" cmd /k "call \"%~dp0start-frontend.bat\""
timeout /t 1 /nobreak >nul
start "Regkasse Frontend Admin" cmd /k "call \"%~dp0start-frontend-admin.bat\""

echo.
echo [OK] Launched Redis, Backend, POS, Admin windows.
echo Close each window (or use kill-ports.bat) to stop.
echo.
pause
exit /b 0
