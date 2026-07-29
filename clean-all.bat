@echo off
setlocal EnableExtensions
chcp 65001 >nul

echo ========================================
echo  Cleaning All Build Artifacts
echo ========================================
echo.
echo WARNING: This will remove all build artifacts!
echo.
set /p confirm="Are you sure? (y/N): "
if /i not "%confirm%"=="y" (
    echo Cancelled.
    pause
    exit /b 0
)

cd /d "%~dp0"

echo.
echo [1/4] Cleaning backend...
dotnet clean backend/KasseAPI_Final.csproj
rmdir /s /q backend\bin 2>nul
rmdir /s /q backend\obj 2>nul
rmdir /s /q backend\KasseAPI_Final.Tests\bin 2>nul
rmdir /s /q backend\KasseAPI_Final.Tests\obj 2>nul
echo [OK] Backend cleaned!
echo.

echo [2/4] Cleaning admin...
rmdir /s /q frontend-admin\.next 2>nul
rmdir /s /q frontend-admin\node_modules\.cache 2>nul
echo [OK] Admin cleaned!
echo.

echo [3/4] Cleaning POS...
rmdir /s /q frontend\.expo 2>nul
rmdir /s /q frontend\dist 2>nul
rmdir /s /q frontend\node_modules\.cache 2>nul
echo [OK] POS cleaned!
echo.

echo [4/4] Cleaning sites...
rmdir /s /q frontend-sites\.next 2>nul
echo [OK] Sites cleaned!
echo.

echo ========================================
echo  Clean complete!
echo ========================================
pause
exit /b 0
