<#
.SYNOPSIS
  Start the full production-oriented Docker stack locally (test before cloud).

.DESCRIPTION
  Uses docker-compose.prod.yml + .env.production (never merges Soft TSE override).
  Defaults to profiles admin + sites + pos so you can exercise the whole stack on
  localhost. Requires Docker Desktop and filled Fiskaly secrets for API uptime.

.PARAMETER ApiOnly
  Start only Postgres, Redis, backend (no frontend profiles).

.PARAMETER Profile
  Override profiles (default: admin, sites, pos). Ignored when -ApiOnly.

.PARAMETER NoBuild
  Skip image rebuild (up -d only).

.PARAMETER SkipConfirm
  Do not prompt.

.PARAMETER SkipBootstrap
  Do not auto-copy .env.production from local/example templates.

.EXAMPLE
  .\scripts\docker-up-prod.ps1
  .\scripts\docker-up-prod.ps1 -ApiOnly
  .\scripts\docker-up-prod.ps1 -NoBuild -SkipConfirm
#>
[CmdletBinding()]
param(
    [switch]$ApiOnly,
    [string[]]$Profile = @('admin', 'sites', 'pos'),
    [switch]$NoBuild,
    [switch]$SkipConfirm,
    [switch]$SkipBootstrap
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$envFile = Join-Path $repoRoot '.env.production'
$localExample = Join-Path $repoRoot '.env.production.local.example'
$cloudExample = Join-Path $repoRoot '.env.production.example'

function Assert-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI not found. Run .\scripts\docker\ensure-docker-desktop.ps1' }
    & docker info 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker engine not reachable. Start Docker Desktop, then .\scripts\docker\docker-diagnose.ps1"
    }
}

Assert-Docker

if (-not (Test-Path $envFile) -and -not $SkipBootstrap) {
    $src = $null
    if (Test-Path $localExample) { $src = $localExample }
    elseif (Test-Path $cloudExample) { $src = $cloudExample }
    else { throw 'Missing .env.production.example / .env.production.local.example' }

    Copy-Item -Path $src -Destination $envFile
    Write-Host "[bootstrap] Created .env.production from $(Split-Path $src -Leaf)" -ForegroundColor Yellow
    Write-Host "            Edit JWT/DB/Fiskaly secrets, then re-run docker-up-prod.bat" -ForegroundColor Yellow
    Write-Host ""
}

if (-not (Test-Path $envFile)) {
    throw @"
Missing .env.production.

  copy .env.production.local.example .env.production   # local localhost URLs
  # or
  copy .env.production.example .env.production         # cloud-style URLs

Then set POSTGRES_PASSWORD, JWT_SECRET_KEY (≥32), Fiskaly keys.
"@
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Regkasse — Production Docker (local)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  compose: docker-compose.prod.yml"
Write-Host "  env:     .env.production"
Write-Host "  Soft TSE: NOT loaded (Device/Real fail-closed)"
Write-Host ""

if (-not $SkipConfirm) {
    $answer = Read-Host "Start production-oriented stack on this machine? [y/N]"
    if ($answer -notmatch '^(y|yes)$') {
        Write-Host 'Aborted.'
        exit 0
    }
}

$profiles = @()
if (-not $ApiOnly) {
    foreach ($p in $Profile) {
        if ($p) { $profiles += $p.Trim().ToLowerInvariant() }
    }
    $profiles = $profiles | Select-Object -Unique
}

$base = @('compose', '-f', 'docker-compose.prod.yml', '--env-file', '.env.production')
foreach ($p in $profiles) {
    $base += @('--profile', $p)
}

if (-not $NoBuild) {
    Write-Host '=== Building images ===' -ForegroundColor Cyan
    & docker @($base + @('build'))
    if ($LASTEXITCODE -ne 0) { throw "Production build failed (exit $LASTEXITCODE)" }
}

Write-Host '=== Starting stack (-d) ===' -ForegroundColor Cyan
& docker @($base + @('up', '-d'))
if ($LASTEXITCODE -ne 0) { throw "Production up failed (exit $LASTEXITCODE)" }

Write-Host ''
Write-Host 'Waiting for API health...' -ForegroundColor Cyan
$ok = $false
for ($i = 1; $i -le 36; $i++) {
    try {
        $r = Invoke-WebRequest -Uri 'http://127.0.0.1:5184/api/health/live' -UseBasicParsing -TimeoutSec 3
        if ($r.StatusCode -eq 200) { $ok = $true; break }
    }
    catch {
        Start-Sleep -Seconds 5
    }
}

& docker @($base + @('ps'))

Write-Host ''
if ($ok) {
    Write-Host '[OK] API liveness is healthy.' -ForegroundColor Green
}
else {
    Write-Host '[WARN] API /api/health/live not ready yet.' -ForegroundColor Yellow
    Write-Host '       Often missing Fiskaly secrets or TSE lock — check:' -ForegroundColor Yellow
    Write-Host '       docker-logs-prod.bat backend' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Local URLs (loopback):' -ForegroundColor Cyan
Write-Host '  API:   http://127.0.0.1:5184/api/health/live'
Write-Host '  Admin: http://127.0.0.1:3000/health   (profile admin)'
Write-Host '  Sites: http://127.0.0.1:3001/health   (profile sites)'
Write-Host '  POS:   http://127.0.0.1:8081/healthz  (profile pos)'
Write-Host ''
Write-Host 'Stop:  docker-down-prod.bat'
Write-Host 'Logs:  docker-logs-prod.bat'
Write-Host 'Docs:  docs/DOCKER_PRODUCTION.md'
