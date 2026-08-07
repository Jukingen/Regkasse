@echo off
REM =============================================================================
REM DANGER: This script can destroy data, wipe volumes, or rewrite git history.
REM Read the warnings below carefully before confirming.
REM =============================================================================
setlocal EnableExtensions
chcp 65001 >nul

REM Development-only: hard-delete all products/categories for one tenant before FA demo import.
REM Requires backend in Development with products.manage (or SuperAdmin).
REM
REM Usage:
REM   dev-purge-tenant-catalog.DANGER.bat
REM   dev-purge-tenant-catalog.DANGER.bat -TenantSlug test_cafe -LoginIdentifier admin -Password "secret"
REM   dev-purge-tenant-catalog.DANGER.bat -Token "eyJ..." -TenantSlug test_cafe
REM Endpoint: POST /api/admin/products/dev/purge-catalog (Development only)
REM   dev-purge-tenant-catalog.DANGER.bat -KeepCategories -TenantSlug dev -LoginIdentifier admin -Password "secret"
REM   dev-purge-tenant-catalog.DANGER.bat -WithFiscalOverride -TenantSlug dev -LoginIdentifier admin -Password "secret"

cd /d "%~dp0..\.."
set "PS_SCRIPT=%~dp0dev-purge-tenant-catalog.DANGER.ps1"
set "LOG=%TEMP%\regkasse-dev-purge-catalog-%RANDOM%.log"

echo ========================================
echo  Dev Tenant Catalog Purge
echo  WARNING: Hard-deletes products ^(and categories^) — Development only
echo ========================================
echo  Log: %LOG%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %* > "%LOG%" 2>&1
set "EXIT_CODE=%ERRORLEVEL%"

type "%LOG%"
echo.
echo ----------------------------------------
if %EXIT_CODE% equ 0 (
    echo [OK] Tenant catalog purge finished. Exit code: 0
) else (
    echo [FAILED] Tenant catalog purge failed. Exit code: %EXIT_CODE%
    echo Full log saved to: %LOG%
)
echo ----------------------------------------
echo.

pause
exit /b %EXIT_CODE%
