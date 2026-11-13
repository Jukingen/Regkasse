<#
.SYNOPSIS
  Deploy (build + start) the production-oriented Regkasse Compose stack.

.DESCRIPTION
  Requires .env.production (from .env.production.example).
  Does NOT load docker-compose.override.yml (Soft TSE stays off).
  Puts TLS reverse proxy in front of 127.0.0.1 binds — see DEPLOYMENT.md.

.PARAMETER Profile
  Optional profiles: admin, sites, pos.

.PARAMETER NoBuild
  Skip image build (up -d only).

.PARAMETER SkipConfirm
  Do not prompt before starting Production fiscal stack.

.EXAMPLE
  .\scripts\docker-deploy.ps1
  .\scripts\docker-deploy.ps1 -Profile admin,sites
  .\scripts\docker-deploy.ps1 -NoBuild -SkipConfirm
#>
[CmdletBinding()]
param(
    [string[]]$Profile = @(),
    [switch]$NoBuild,
    [switch]$SkipConfirm
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$envFile = Join-Path $repoRoot '.env.production'
$example = Join-Path $repoRoot '.env.production.example'

if (-not (Test-Path $envFile)) {
    throw @"
Missing .env.production.

  copy .env.production.example .env.production
  # Fill POSTGRES_*, JWT_SECRET_KEY (≥32), ADMIN_API_URL, Fiskaly secrets

See docs/DOCKER_SETUP.md and DEPLOYMENT.md § Docker Compose (production-oriented).
"@
}

& docker info 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Docker engine not reachable. Run .\scripts\docker\docker-diagnose.ps1"
}

Write-Host "Production deploy using:" -ForegroundColor Cyan
Write-Host "  compose: docker-compose.prod.yml"
Write-Host "  env:     .env.production"
Write-Host "  Soft TSE override: NOT loaded (correct for Production)"
Write-Host ""

if (-not $SkipConfirm) {
    $answer = Read-Host "Continue with Production-oriented stack? Soft TSE is forbidden. [y/N]"
    if ($answer -notmatch '^(y|yes)$') {
        Write-Host "Aborted."
        exit 0
    }
}

$base = @('compose', '-f', 'docker-compose.prod.yml', '--env-file', '.env.production')
foreach ($p in $Profile) {
    if ($p) { $base += @('--profile', $p) }
}

if (-not $NoBuild) {
    Write-Host "=== Building images ===" -ForegroundColor Cyan
    & docker @($base + @('build'))
    if ($LASTEXITCODE -ne 0) { throw "Production build failed" }
}

Write-Host "=== Starting stack (-d) ===" -ForegroundColor Cyan
& docker @($base + @('up', '-d'))
if ($LASTEXITCODE -ne 0) { throw "Production up failed" }

Write-Host ""
Write-Host "[OK] Production-oriented stack is up." -ForegroundColor Green
& docker @($base + @('ps'))

Write-Host ""
Write-Host "Smoke:" -ForegroundColor Cyan
Write-Host "  curl -fsS http://127.0.0.1:5184/api/health/live"
Write-Host "Docs: docs/DOCKER_PRODUCTION.md · docs/DOCKER_ENV_VARS.md · DEPLOYMENT.md · docs/TSE_PRODUCTION_CONFIG_LOCK.md"
Write-Host "Logs:  docker-logs-prod.bat   or   .\scripts\docker-logs-prod.ps1"
Write-Host "Stop:  .\scripts\docker-down.ps1 -Prod"
