<#
.SYNOPSIS
  Build production-oriented Regkasse images (docker-compose.prod.yml).

.PARAMETER Profile
  Optional Compose profiles: admin, sites, pos.

.PARAMETER NoCache
  Pass --no-cache to compose build.

.EXAMPLE
  .\scripts\docker-build-prod.ps1
  .\scripts\docker-build-prod.ps1 -Profile admin,sites,pos
  .\scripts\docker-build-prod.ps1 -NoCache
#>
[CmdletBinding()]
param(
    [string[]]$Profile = @(),
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

& docker info 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Docker engine not reachable. Run .\scripts\docker-diagnose.ps1"
}

$envFile = Join-Path $repoRoot '.env.production'
$composeArgs = @('compose', '-f', 'docker-compose.prod.yml')
if (Test-Path $envFile) {
    $composeArgs += @('--env-file', '.env.production')
}
else {
    Write-Warning ".env.production missing — copy .env.production.example first for real build-args."
}

foreach ($p in $Profile) {
    if ($p) { $composeArgs += @('--profile', $p) }
}

$composeArgs += 'build'
if ($NoCache) { $composeArgs += '--no-cache' }

Write-Host "=== Production build ===" -ForegroundColor Cyan
Write-Host ("docker " + ($composeArgs -join ' ')) -ForegroundColor DarkGray
& docker @composeArgs
if ($LASTEXITCODE -ne 0) { throw "Production build failed (exit $LASTEXITCODE)" }

Write-Host "[OK] Production images built." -ForegroundColor Green
Write-Host "Deploy: .\scripts\docker-deploy.ps1   or   deploy-docker.bat"
