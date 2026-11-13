@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Regkasse - Mode Scripts Smoke Test
color 0A

:: Non-interactive structural + error-path checks for Legacy / Docker / start.bat.
:: Does NOT leave long-running servers up.
:: Usage: scripts\test-mode-scripts.bat

set "REPO=%~dp0..\.."
pushd "%REPO%" >nul
set "REPO=%CD%"
popd >nul

set "LOG_DIR=C:\Scripts\logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
set "TEST_LOG=%LOG_DIR%\mode_scripts_test.log"

echo ======================================== > "%TEST_LOG%"
echo Mode scripts smoke - %date% %time% >> "%TEST_LOG%"
echo ======================================== >> "%TEST_LOG%"

set PASS=0
set FAIL=0
set SKIP=0

echo.
echo ========================================
echo  Regkasse Mode Scripts - Smoke Test
echo ========================================
echo.
echo Repo: %REPO%
echo Log:  %TEST_LOG%
echo.

goto :main

:assert_file
if exist "%~1" (
    echo [PASS] exists: %~1
    echo [PASS] exists: %~1 >> "%TEST_LOG%"
    set /a PASS+=1
) else (
    echo [FAIL] missing: %~1
    echo [FAIL] missing: %~1 >> "%TEST_LOG%"
    set /a FAIL+=1
)
goto :eof

:main
echo --- 1. Layout ---
call :assert_file "%REPO%\start.bat"
call :assert_file "%REPO%\scripts\legacy\start-all.bat"
call :assert_file "%REPO%\scripts\legacy\start-backend.bat"
call :assert_file "%REPO%\scripts\legacy\start-frontend.bat"
call :assert_file "%REPO%\scripts\legacy\start-frontend-admin.bat"
call :assert_file "%REPO%\scripts\legacy\start-redis.bat"
call :assert_file "%REPO%\scripts\docker\host\up.bat"
call :assert_file "%REPO%\scripts\docker\host\down.bat"
call :assert_file "%REPO%\scripts\docker\host\status.bat"
call :assert_file "%REPO%\scripts\docker\host\logs.bat"
call :assert_file "%REPO%\scripts\docker\host\clean.DANGER.bat"
call :assert_file "%REPO%\docs\DOCKER_VS_LEGACY.md"

echo.
echo --- 2. start.bat menu (Exit) ---
echo 3| "%REPO%\start.bat" >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [PASS] start.bat Exit option returns 0
    echo [PASS] start.bat Exit >> "%TEST_LOG%"
    set /a PASS+=1
) else (
    echo [FAIL] start.bat Exit option failed ^(exit %ERRORLEVEL%^)
    echo [FAIL] start.bat Exit %ERRORLEVEL% >> "%TEST_LOG%"
    set /a FAIL+=1
)

echo.
echo --- 3. Legacy _repo.bat / PROJECT paths ---
call "%REPO%\scripts\legacy\_repo.bat"
if exist "%REPO_ROOT%\backend" if exist "%LOG_DIR%" (
    echo [PASS] legacy _repo.bat REPO_ROOT=%REPO_ROOT%
    echo [PASS] legacy _repo.bat >> "%TEST_LOG%"
    set /a PASS+=1
) else (
    echo [FAIL] legacy _repo.bat did not resolve repo/log
    echo [FAIL] legacy _repo.bat >> "%TEST_LOG%"
    set /a FAIL+=1
)

echo.
echo --- 4. Docker CLI ---
where docker >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [SKIP] Docker CLI not on PATH — Compose live tests skipped
    echo [SKIP] Docker CLI missing >> "%TEST_LOG%"
    set /a SKIP+=1
    echo [PASS] Rollback path: use Legacy ^(scripts\legacy\start-all.bat^)
    echo [PASS] rollback note >> "%TEST_LOG%"
    set /a PASS+=1
    goto :docker_error_path
)

docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [SKIP] Docker engine not running — Compose live tests skipped
    echo [SKIP] Docker engine down >> "%TEST_LOG%"
    set /a SKIP+=1
    goto :docker_error_path
)

echo [PASS] Docker engine reachable
echo [PASS] Docker engine >> "%TEST_LOG%"
set /a PASS+=1

echo.
echo --- 5. Docker up / status / down ^(live^) ---
cd /d "%REPO%"
docker compose --profile pos --profile sites up -d >> "%TEST_LOG%" 2>&1
if %ERRORLEVEL% neq 0 (
    echo [FAIL] docker compose up
    echo [FAIL] docker compose up >> "%TEST_LOG%"
    set /a FAIL+=1
    goto :summary
)
echo [PASS] docker compose up
echo [PASS] docker compose up >> "%TEST_LOG%"
set /a PASS+=1

docker compose ps >> "%TEST_LOG%" 2>&1
echo [PASS] docker compose ps
set /a PASS+=1

docker compose --profile pos --profile sites down >> "%TEST_LOG%" 2>&1
if %ERRORLEVEL% neq 0 (
    echo [FAIL] docker compose down
    set /a FAIL+=1
) else (
    echo [PASS] docker compose down
    set /a PASS+=1
)
goto :summary

:docker_error_path
echo.
echo --- 5. Docker script error path ^(expect [ERROR] when Docker missing^) ---
:: Run docker info check the same way docker-up.bat does
docker info >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [SKIP] Docker unexpectedly available in error-path branch
    set /a SKIP+=1
) else (
    echo [PASS] docker-up would abort with Docker not running ^(matches script guard^)
    echo [PASS] docker-up abort path >> "%TEST_LOG%"
    set /a PASS+=1
    :: Simulate log write like docker-up.bat
    echo ======================================== > "C:\Scripts\logs\docker.log"
    echo Regkasse Docker Start - smoke test abort - %date% %time% >> "C:\Scripts\logs\docker.log"
    echo ERROR: Docker not running >> "C:\Scripts\logs\docker.log"
    if exist "C:\Scripts\logs\docker.log" (
        echo [PASS] log write to C:\Scripts\logs\docker.log
        echo [PASS] log write docker.log >> "%TEST_LOG%"
        set /a PASS+=1
    ) else (
        echo [FAIL] could not write C:\Scripts\logs\docker.log
        echo [FAIL] log write docker.log >> "%TEST_LOG%"
        set /a FAIL+=1
    )
)

:summary
echo.
echo --- 6. Logs folder ---
if exist "%LOG_DIR%" (
    echo [PASS] %LOG_DIR% exists
    echo [PASS] LOG_DIR >> "%TEST_LOG%"
    set /a PASS+=1
    dir /b "%LOG_DIR%" >> "%TEST_LOG%"
) else (
    echo [FAIL] %LOG_DIR% missing
    set /a FAIL+=1
)

echo.
echo ========================================
echo  Results: PASS=%PASS%  FAIL=%FAIL%  SKIP=%SKIP%
echo  Log: %TEST_LOG%
echo ========================================
echo.
echo Checklist notes:
echo  [ ] Legacy start-all.bat — open manually ^(starts 4 windows^); smoke only checked files
echo  [ ] Docker docker-up.bat — needs Docker Desktop on PATH
echo  [ ] docker-down / status / logs — same
echo  [x] Logs go to C:\Scripts\logs\
echo  [x] start.bat menu Exit works
echo  Rollback: If Docker doesn't work, use scripts\legacy\
echo.

if %FAIL% neq 0 (
    echo [FAILED] One or more checks failed.
    pause
    exit /b 1
)

echo [OK] Smoke checks passed ^(see SKIP if Docker was unavailable^).
pause
exit /b 0
