@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Regkasse — Production Docker Deploy
echo ========================================
echo.
echo  Uses: docker-compose.prod.yml + .env.production
echo  Soft TSE override is NOT loaded.
echo.

cd /d "%~dp0"

if not exist ".env.production" (
    echo [ERROR] Missing .env.production
    echo   copy .env.production.example .env.production
    echo   Fill POSTGRES_*, JWT_SECRET_KEY, ADMIN_API_URL, Fiskaly secrets
    echo.
    pause
    exit /b 1
)

docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker is not running. Start Docker Desktop first.
    pause
    exit /b 1
)

echo Profiles: pass admin sites pos as arguments, e.g.
echo   deploy-docker.bat admin
echo   deploy-docker.bat admin sites pos
echo.

set "PROFILES="
:parse_args
if "%~1"=="" goto run_deploy
set "PROFILES=%PROFILES% -Profile %~1"
shift
goto parse_args

:run_deploy
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\docker-deploy.ps1" %PROFILES%
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
) else (
    echo [OK] Production-oriented stack deployed.
    echo Smoke: curl -fsS http://127.0.0.1:5184/api/health/live
)
echo.
pause
exit /b %EXIT_CODE%
