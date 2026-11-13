<#
.SYNOPSIS
  CI test helper — backend, Admin, and/or POS quality gates.

.DESCRIPTION
  Used by GitHub Actions (ci.yml) and local verification.
  Does not start Docker or deploy.

.PARAMETER Backend
  Run backend unit tests (exclude Category=PostgreSql).

.PARAMETER Admin
  Run frontend-admin lint + typecheck + test (no E2E by default).

.PARAMETER Pos
  Run frontend (POS) lint + typecheck + test.

.PARAMETER SkipBackend / SkipAdmin / SkipPos
  Exclude a package when running the default "all" mode.

.PARAMETER IncludeAdminBuild
  Also run `npm run build` in frontend-admin (slower).

.EXAMPLE
  .\scripts\ci-test.ps1
  .\scripts\ci-test.ps1 -Backend -SkipAdmin -SkipPos
  .\scripts\ci-test.ps1 -Admin -IncludeAdminBuild
#>
[CmdletBinding()]
param(
    [switch]$Backend,
    [switch]$Admin,
    [switch]$Pos,
    [switch]$SkipBackend,
    [switch]$SkipAdmin,
    [switch]$SkipPos,
    [switch]$IncludeAdminBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$anyExplicit = $Backend -or $Admin -or $Pos
if (-not $anyExplicit) {
    $Backend = -not $SkipBackend
    $Admin = -not $SkipAdmin
    $Pos = -not $SkipPos
}
else {
    if ($SkipBackend) { $Backend = $false }
    if ($SkipAdmin) { $Admin = $false }
    if ($SkipPos) { $Pos = $false }
}

function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

$failed = @()

if ($Backend) {
    if (-not (Test-Command 'dotnet')) { throw 'dotnet SDK not found on PATH' }
    Write-Host '=== CI: backend tests ===' -ForegroundColor Cyan
    Push-Location (Join-Path $repoRoot 'backend')
    try {
        & dotnet restore KasseAPI_Final.sln
        if ($LASTEXITCODE -ne 0) { $failed += 'backend-restore'; }
        else {
            & dotnet build KasseAPI_Final.sln --configuration Release --no-restore
            if ($LASTEXITCODE -ne 0) { $failed += 'backend-build' }
            else {
                & dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj `
                    --configuration Release --no-build `
                    --filter 'Category!=PostgreSql' --verbosity minimal
                if ($LASTEXITCODE -ne 0) { $failed += 'backend-test' }
            }
        }
    }
    finally { Pop-Location }
}

if ($Admin) {
    if (-not (Test-Command 'npm')) { throw 'npm not found on PATH' }
    Write-Host '=== CI: frontend-admin tests ===' -ForegroundColor Cyan
    Push-Location (Join-Path $repoRoot 'frontend-admin')
    try {
        if (-not (Test-Path 'node_modules')) {
            & npm ci
            if ($LASTEXITCODE -ne 0) { $failed += 'admin-install'; }
        }
        if ($failed -notcontains 'admin-install') {
            $env:NEXT_PUBLIC_RKSV_ENVIRONMENT = if ($env:NEXT_PUBLIC_RKSV_ENVIRONMENT) { $env:NEXT_PUBLIC_RKSV_ENVIRONMENT } else { 'TEST' }
            $env:NEXT_PUBLIC_API_BASE_URL = if ($env:NEXT_PUBLIC_API_BASE_URL) { $env:NEXT_PUBLIC_API_BASE_URL } else { 'http://127.0.0.1:5184' }
            & npm run lint
            if ($LASTEXITCODE -ne 0) { $failed += 'admin-lint' }
            & npm run typecheck
            if ($LASTEXITCODE -ne 0) { $failed += 'admin-typecheck' }
            & npm run test
            if ($LASTEXITCODE -ne 0) { $failed += 'admin-test' }
            if ($IncludeAdminBuild) {
                & npm run build
                if ($LASTEXITCODE -ne 0) { $failed += 'admin-build' }
            }
        }
    }
    finally { Pop-Location }
}

if ($Pos) {
    if (-not (Test-Command 'npm')) { throw 'npm not found on PATH' }
    Write-Host '=== CI: frontend (POS) tests ===' -ForegroundColor Cyan
    Push-Location (Join-Path $repoRoot 'frontend')
    try {
        if (-not (Test-Path 'node_modules')) {
            & npm ci --legacy-peer-deps
            if ($LASTEXITCODE -ne 0) { $failed += 'pos-install' }
        }
        if ($failed -notcontains 'pos-install') {
            & npm run lint
            if ($LASTEXITCODE -ne 0) { $failed += 'pos-lint' }
            & npm run typecheck
            if ($LASTEXITCODE -ne 0) { $failed += 'pos-typecheck' }
            & npm run test
            if ($LASTEXITCODE -ne 0) { $failed += 'pos-test' }
        }
    }
    finally { Pop-Location }
}

if ($failed.Count -gt 0) {
    Write-Host ("[FAIL] " + ($failed -join ', ')) -ForegroundColor Red
    exit 1
}

Write-Host '[OK] ci-test finished' -ForegroundColor Green
