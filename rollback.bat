@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Production Rollback
echo ========================================
echo.
echo WARNING: This will rollback to the previous git commit!
echo          Runs "git reset --hard HEAD~1" then rebuilds
echo          docker-compose.prod.yml. Uncommitted and last-commit
echo          changes will be LOST. Prefer "git revert" on shared branches.
echo.
set /p confirm="Are you sure? (y/N): "
if /i not "%confirm%"=="y" (
    echo Cancelled.
    pause
    exit /b 0
)

cd /d "%~dp0"

echo.
echo Rolling back...
docker compose -f docker-compose.prod.yml down
git reset --hard HEAD~1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Git rollback failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Rebuilding previous version...
docker compose -f docker-compose.prod.yml build
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed!
    pause
    exit /b %ERRORLEVEL%
)

docker compose -f docker-compose.prod.yml up -d
if %ERRORLEVEL% neq 0 (
    echo [ERROR] docker compose up failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================
echo  Rollback Complete!
echo ========================================
pause
exit /b 0
