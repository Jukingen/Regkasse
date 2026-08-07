<#
.SYNOPSIS
  Build Regkasse Docker images (development and/or production Compose files).

.DESCRIPTION
  Runs `docker compose build` for the Dev stack and/or production-oriented stack.
  Does not start containers. Use docker-up.ps1 / docker-deploy.ps1 to run.

.PARAMETER Dev
  Build docker-compose.yml (+ auto override). Default if neither -Dev nor -Prod.

.PARAMETER Prod
  Build docker-compose.prod.yml (requires .env.production for some build-args when profiles used).

.PARAMETER Profile
  Optional Compose profiles to include (e.g. pos, sites, admin). Repeatable.

.PARAMETER NoCache
  Pass --no-cache to compose build.

.EXAMPLE
  .\scripts\docker-build.ps1
  .\scripts\docker-build.ps1 -Dev -Prod
  .\scripts\docker-build.ps1 -Prod -Profile admin,pos -NoCache
#>
[CmdletBinding()]
param(
    [switch]$Dev,
    [switch]$Prod,
    [string[]]$Profile = @(),
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

if (-not $Dev -and -not $Prod) {
    $Dev = $true
    $Prod = $true
}

function Test-Docker {
    & docker version --format '{{.Server.Version}}' 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker engine not available. Start Docker Desktop and retry. See docs/DOCKER_WINDOWS_TROUBLESHOOTING.md"
    }
}

function Invoke-ComposeBuild {
    param(
        [string[]]$ComposeArgs,
        [string]$Label
    )
    $args = @('compose') + $ComposeArgs
    foreach ($p in $Profile) {
        if ($p) { $args += @('--profile', $p) }
    }
    $args += 'build'
    if ($NoCache) { $args += '--no-cache' }

    Write-Host ""
    Write-Host "=== Build: $Label ===" -ForegroundColor Cyan
    Write-Host ("docker " + ($args -join ' ')) -ForegroundColor DarkGray
    & docker @args
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $Label (exit $LASTEXITCODE)"
    }
    Write-Host "[OK] $Label" -ForegroundColor Green
}

Test-Docker

if ($Dev) {
    Invoke-ComposeBuild -ComposeArgs @() -Label 'development (docker-compose.yml + override)'
}

if ($Prod) {
    $prodEnv = Join-Path $repoRoot '.env.production'
    $composeArgs = @('-f', 'docker-compose.prod.yml')
    if (Test-Path $prodEnv) {
        $composeArgs += @('--env-file', '.env.production')
    }
    else {
        Write-Warning ".env.production missing — building with Compose defaults / empty secrets. Copy .env.production.example first for real prod args."
    }
    Invoke-ComposeBuild -ComposeArgs $composeArgs -Label 'production (docker-compose.prod.yml)'
}

Write-Host ""
Write-Host "Build finished. Start Dev: .\scripts\docker-up.ps1   Prod: .\scripts\docker-deploy.ps1" -ForegroundColor Green
