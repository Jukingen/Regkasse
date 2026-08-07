<#
.SYNOPSIS
  Stop the production-oriented Compose stack (keeps volumes by default).

.PARAMETER Volumes
  Also remove named volumes (destructive — wipes Postgres/Redis data).

.EXAMPLE
  .\scripts\docker-down-prod.ps1
  .\scripts\docker-down-prod.ps1 -Volumes
#>
[CmdletBinding()]
param(
    [switch]$Volumes
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$args = @('compose', '-f', 'docker-compose.prod.yml')
$envFile = Join-Path $repoRoot '.env.production'
if (Test-Path $envFile) {
    $args += @('--env-file', '.env.production')
}
# Include profiles so all services are torn down
$args += @('--profile', 'admin', '--profile', 'sites', '--profile', 'pos', 'down')
if ($Volumes) {
    $args += '-v'
    Write-Host 'WARNING: Removing volumes (DB/Redis data will be deleted).' -ForegroundColor Yellow
}

Write-Host ("docker " + ($args -join ' ')) -ForegroundColor DarkGray
& docker @args
if ($LASTEXITCODE -ne 0) { throw "docker compose down failed (exit $LASTEXITCODE)" }
Write-Host '[OK] Production-oriented stack stopped.' -ForegroundColor Green
