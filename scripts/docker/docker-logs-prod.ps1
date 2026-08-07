<#
.SYNOPSIS
  Tail production Compose logs (docker-compose.prod.yml).

.PARAMETER Service
  Optional service name: postgres, redis, backend, frontend-admin, frontend-sites, frontend.

.PARAMETER Follow
  Follow log output (default: true). Pass -Follow:$false for a snapshot.

.PARAMETER Tail
  Number of lines (default: 200).

.EXAMPLE
  .\scripts\docker-logs-prod.ps1
  .\scripts\docker-logs-prod.ps1 -Service backend
  .\scripts\docker-logs-prod.ps1 -Service frontend-admin -Tail 500
#>
[CmdletBinding()]
param(
    [string]$Service = '',
    [switch]$Follow = $true,
    [int]$Tail = 200
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$envFile = Join-Path $repoRoot '.env.production'
$args = @('compose', '-f', 'docker-compose.prod.yml')
if (Test-Path $envFile) {
    $args += @('--env-file', '.env.production')
}
# Include optional profiles so named services resolve when running
$args += @('--profile', 'admin', '--profile', 'sites', '--profile', 'pos')
$args += @('logs', '--tail', "$Tail")
if ($Follow) { $args += '-f' }
if ($Service) { $args += $Service }

Write-Host ("docker " + ($args -join ' ')) -ForegroundColor DarkGray
& docker @args
exit $LASTEXITCODE
