@echo off
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0_repo.bat"

echo ========================================
echo  Regkasse Legacy Mode — start
echo ========================================
echo.
echo   Logs: %LOG_DIR%
echo   Repo: %REPO_ROOT%
echo.
echo Default: Redis + Backend + Admin (RAM-safe).
echo Full:    start-all.bat all   ^(also POS^)
echo Cleanup: kills orphan Next.js workers first.
echo ========================================
echo.

REM Kill orphan Next/Expo node workers from previous half-stopped runs
echo [cleanup] orphan Next/Expo workers...
pushd "%REPO_ROOT%"
call npm run dev:cleanup
popd
echo.

start "Regkasse Redis" cmd /k "call \"%~dp0start-redis.bat\""
timeout /t 2 /nobreak >nul
start "Regkasse Backend" cmd /k "call \"%~dp0start-backend.bat\""
timeout /t 1 /nobreak >nul
start "Regkasse Frontend Admin" cmd /k "call \"%~dp0start-frontend-admin.bat\""

if /I "%~1"=="all" (
    timeout /t 1 /nobreak >nul
    start "Regkasse Frontend POS" cmd /k "call \"%~dp0start-frontend.bat\""
    echo.
    echo [OK] Launched Redis, Backend, Admin, POS.
) else (
    echo.
    echo [OK] Launched Redis, Backend, Admin.
    echo      POS icin: start-all.bat all
)

echo Close each window ^(or kill-ports.bat / npm run dev:cleanup^) to stop.
echo.
pause
exit /b 0
