<#
.SYNOPSIS
  CI build helper — .NET Release build and/or production Docker images.

.DESCRIPTION
  Used by GitHub Actions (ci.yml / deploy.yml) and local pre-flight.
  Does not start containers. Prefer docker-build-prod.ps1 for interactive host builds.

.PARAMETER Dotnet
  Restore + build backend solution (Release).

.PARAMETER Docker
  Build Docker images via docker-compose.prod.yml (profiles optional).

.PARAMETER Profiles
  Compose profiles to include (admin, sites, pos).

.PARAMETER NoPush
  Build only (default when -Push is not set).

.PARAMETER Push
  After build, tag and push images to -Registry (requires docker login).

.PARAMETER Registry
  Registry prefix (e.g. ghcr.io/myorg). Defaults to DOCKER_REGISTRY env.

.PARAMETER Tag
  Image tag. Defaults to IMAGE_TAG env or "ci".

.PARAMETER NoCache
  Pass --no-cache to compose build.

.EXAMPLE
  .\scripts\ci-build.ps1 -Dotnet
  .\scripts\ci-build.ps1 -Docker -Profiles admin -NoPush
  .\scripts\ci-build.ps1 -Docker -Profiles admin,sites,pos -Push -Registry ghcr.io/org -Tag sha-abc1234
#>
[CmdletBinding()]
param(
    [switch]$Dotnet,
    [switch]$Docker,
    [string[]]$Profiles = @(),
    [switch]$NoPush,
    [switch]$Push,
    [string]$Registry = '',
    [string]$Tag = '',
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $Dotnet -and -not $Docker) {
    $Dotnet = $true
    $Docker = $true
}

if (-not $Tag) {
    $Tag = if ($env:IMAGE_TAG) { $env:IMAGE_TAG } else { 'ci' }
}
if (-not $Registry) {
    $Registry = if ($env:DOCKER_REGISTRY) { $env:DOCKER_REGISTRY.TrimEnd('/') } else { '' }
}

function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

if ($Dotnet) {
    if (-not (Test-Command 'dotnet')) { throw 'dotnet SDK not found on PATH' }
    Write-Host '=== CI: dotnet restore/build ===' -ForegroundColor Cyan
    Push-Location (Join-Path $repoRoot 'backend')
    try {
        & dotnet restore KasseAPI_Final.sln
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)" }
        & dotnet build KasseAPI_Final.sln --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
    }
    finally { Pop-Location }
    Write-Host '[OK] Backend Release build' -ForegroundColor Green
}

if ($Docker) {
    if (-not (Test-Command 'docker')) { throw 'docker not found on PATH' }
    & docker info 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Docker engine not reachable' }

    # Ensure compose build-args resolve (CI often has no .env.production)
    if (-not $env:ADMIN_API_URL) { $env:ADMIN_API_URL = 'http://127.0.0.1:5184' }
    if (-not $env:POS_API_URL) { $env:POS_API_URL = 'http://127.0.0.1:5184/api' }
    if (-not $env:NEXT_PUBLIC_RKSV_ENVIRONMENT) { $env:NEXT_PUBLIC_RKSV_ENVIRONMENT = 'TEST' }
    if (-not $env:IMAGE_TAG) { $env:IMAGE_TAG = $Tag }

    $composeArgs = @('compose', '-f', 'docker-compose.prod.yml')
    $prodEnv = Join-Path $repoRoot '.env.production'
    if (Test-Path $prodEnv) {
        $composeArgs += @('--env-file', '.env.production')
    }
    foreach ($p in $Profiles) {
        if ($p) { $composeArgs += @('--profile', $p) }
    }
    $composeArgs += 'build'
    if ($NoCache) { $composeArgs += '--no-cache' }

    Write-Host '=== CI: docker compose build (prod) ===' -ForegroundColor Cyan
    Write-Host ("docker " + ($composeArgs -join ' ')) -ForegroundColor DarkGray
    & docker @composeArgs
    if ($LASTEXITCODE -ne 0) { throw "docker compose build failed ($LASTEXITCODE)" }
    Write-Host '[OK] Docker images built' -ForegroundColor Green

    $doPush = $Push -and -not $NoPush
    if ($doPush) {
        if (-not $Registry) {
            throw 'Push requested but Registry / DOCKER_REGISTRY is empty'
        }
        Write-Host "=== CI: push to $Registry (tag=$Tag) ===" -ForegroundColor Cyan
        & (Join-Path $PSScriptRoot 'docker-push-prod.ps1') -Registry $Registry -Tag $Tag -Profile $Profiles
        if ($LASTEXITCODE -ne 0) { throw "docker-push-prod failed ($LASTEXITCODE)" }
    }
    elseif ($Push -and $NoPush) {
        Write-Warning 'Both -Push and -NoPush set; skipping push.'
    }
}

Write-Host '[OK] ci-build finished' -ForegroundColor Green
