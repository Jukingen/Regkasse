@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse Production Deployment
echo ========================================
echo.
echo WARNING: This will deploy using docker-compose.prod.yml!
echo          Confirm backup and that you intend a production-style deploy.
echo.
set /p confirm="Are you sure? (y/N): "
if /i not "%confirm%"=="y" (
    echo Cancelled.
    pause
    exit /b 0
)

cd /d "%~dp0"

if not exist ".env.production" (
    echo [ERROR] Missing .env.production
    echo   copy .env.production.example .env.production
    pause
    exit /b 1
)

docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker is not running!
    echo Please start Docker Desktop first.
    pause
    exit /b 1
)

echo.
echo [1/5] Running pre-deploy checks...
REM Same suite as scripts\smoke-test.bat, without its interactive pause.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-comprehensive-smoke.ps1"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Smoke tests failed! Aborting deployment.
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Pre-deploy checks passed!
echo.

echo [2/5] Creating backup...
echo Please trigger backup via API or FA
echo.
set /p backup_done="Backup completed? (y/N): "
if /i not "%backup_done%"=="y" (
    echo Backup not confirmed! Aborting.
    pause
    exit /b 1
)
echo [OK] Backup confirmed!
echo.

echo [3/5] Building images...
docker compose -f docker-compose.prod.yml --env-file .env.production build
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed!
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Build complete!
echo.

echo [4/5] Deploying...
docker compose -f docker-compose.prod.yml --env-file .env.production up -d
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Deployment failed!
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Deployment complete!
echo.

echo [5/5] Verifying deployment...
timeout /t 10 /nobreak >nul
curl -sS http://127.0.0.1:5184/api/health/live >nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Health check failed!
    echo Rolling back...
    call "%~dp0rollback.bat"
    pause
    exit /b 1
)
echo [OK] All systems healthy!
echo.

echo ========================================
echo  Deployment Successful!
echo ========================================
echo   API:   https://api.regkasse.at
echo   Admin: https://admin.regkasse.at
echo   POS:   https://pos.regkasse.at
echo ========================================
pause
exit /b 0
