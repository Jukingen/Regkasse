<#
.SYNOPSIS
  Start Regkasse Docker Compose stacks (detached).

.PARAMETER Dev
  Start development stack (default). Merges docker-compose.override.yml (Soft TSE).

.PARAMETER Prod
  Start production-oriented stack (requires .env.production). Alias path for deploy is docker-deploy.ps1.

.PARAMETER Profile
  Compose profiles (pos, sites, admin for prod).

.PARAMETER Build
  Pass --build before up.

.EXAMPLE
  .\scripts\docker-up.ps1
  .\scripts\docker-up.ps1 -Profile pos,sites -Build
  .\scripts\docker-up.ps1 -Prod -Profile admin
#>
[CmdletBinding()]
param(
    [switch]$Dev,
    [switch]$Prod,
    [string[]]$Profile = @(),
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $Dev -and -not $Prod) { $Dev = $true }
if ($Dev -and $Prod) {
    throw "Specify either -Dev or -Prod (not both). For prod prefer .\scripts\docker-deploy.ps1"
}

function Assert-Docker {
    & docker info 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker engine not reachable. Run .\scripts\docker-diagnose.ps1"
    }
}

Assert-Docker

$args = @('compose')
if ($Prod) {
    $envFile = Join-Path $repoRoot '.env.production'
    if (-not (Test-Path $envFile)) {
        throw "Missing .env.production. Copy .env.production.example and fill secrets."
    }
    $args += @('-f', 'docker-compose.prod.yml', '--env-file', '.env.production')
}

foreach ($p in $Profile) {
    if ($p) { $args += @('--profile', $p) }
}

$args += 'up'
if ($Build) { $args += '--build' }
$args += '-d'

Write-Host ("docker " + ($args -join ' ')) -ForegroundColor DarkGray
& docker @args
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed (exit $LASTEXITCODE)" }

Write-Host "[OK] Stack started (detached)." -ForegroundColor Green
if ($Prod) {
    & docker compose -f docker-compose.prod.yml --env-file .env.production ps
}
else {
    & docker compose ps
}
