<#
.SYNOPSIS
  Tag and push production images to a container registry.

.DESCRIPTION
  Reads DOCKER_REGISTRY and IMAGE_TAG from .env.production (or parameters).
  Requires prior `docker login` to the registry.

.PARAMETER Registry
  Override DOCKER_REGISTRY (e.g. ghcr.io/your-org).

.PARAMETER Tag
  Override IMAGE_TAG (default: prod).

.PARAMETER Profile
  Also push profile images: admin, sites, pos.

.EXAMPLE
  .\scripts\docker-push-prod.ps1 -Registry ghcr.io/myorg -Profile admin
#>
[CmdletBinding()]
param(
    [string]$Registry = '',
    [string]$Tag = '',
    [string[]]$Profile = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

function Get-DotEnvValue {
    param([string]$Path, [string]$Key)
    if (-not (Test-Path $Path)) { return $null }
    foreach ($line in Get-Content $Path) {
        if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
        $parts = $line.Split('=', 2)
        if ($parts[0].Trim() -eq $Key) {
            return $parts[1].Trim().Trim('"').Trim("'")
        }
    }
    return $null
}

$envFile = Join-Path $repoRoot '.env.production'
if (-not $Registry) {
    $Registry = Get-DotEnvValue -Path $envFile -Key 'DOCKER_REGISTRY'
}
if (-not $Tag) {
    $Tag = Get-DotEnvValue -Path $envFile -Key 'IMAGE_TAG'
}
if (-not $Tag) { $Tag = 'prod' }

if (-not $Registry) {
    throw @"
DOCKER_REGISTRY is not set.

  Set DOCKER_REGISTRY=ghcr.io/your-org in .env.production
  or pass -Registry ghcr.io/your-org

Then: docker login $Registry
"@
}

$Registry = $Registry.TrimEnd('/')

$images = @(
    @{ Local = "regkasse-api:$Tag"; Remote = "$Registry/regkasse-api:$Tag" }
)

$profileSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]($Profile | ForEach-Object { $_.ToLowerInvariant() })
)
if ($profileSet.Contains('admin')) {
    $images += @{ Local = "regkasse-frontend-admin:$Tag"; Remote = "$Registry/regkasse-frontend-admin:$Tag" }
}
if ($profileSet.Contains('sites')) {
    $images += @{ Local = "regkasse-frontend-sites:$Tag"; Remote = "$Registry/regkasse-frontend-sites:$Tag" }
}
if ($profileSet.Contains('pos')) {
    $images += @{ Local = "regkasse-frontend-pos-web:$Tag"; Remote = "$Registry/regkasse-frontend-pos-web:$Tag" }
}

& docker info 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Docker engine not reachable." }

foreach ($img in $images) {
    Write-Host "Tag  $($img.Local) -> $($img.Remote)" -ForegroundColor Cyan
    & docker tag $img.Local $img.Remote
    if ($LASTEXITCODE -ne 0) { throw "docker tag failed for $($img.Local)" }

    Write-Host "Push $($img.Remote)" -ForegroundColor Cyan
    & docker push $img.Remote
    if ($LASTEXITCODE -ne 0) { throw "docker push failed for $($img.Remote)" }
}

Write-Host "[OK] Pushed $($images.Count) image(s) to $Registry" -ForegroundColor Green
