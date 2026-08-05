<#
.SYNOPSIS
  Start the optional Regkasse monitoring Compose stack.

.EXAMPLE
  .\scripts\start-monitoring.ps1
  .\scripts\start-monitoring.ps1 -Down
#>
[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$compose = Join-Path $repoRoot 'monitoring\docker-compose.monitoring.yml'
if (-not (Test-Path $compose)) { throw "Missing $compose" }

& docker info 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Docker engine not reachable' }

$args = @('compose', '-f', 'monitoring/docker-compose.monitoring.yml')
if ($Down) {
    & docker @($args + @('down'))
    exit $LASTEXITCODE
}

$up = @('up', '-d')
if ($Build) { $up = @('up', '-d', '--build') }
& docker @($args + $up)
if ($LASTEXITCODE -ne 0) { throw 'monitoring compose up failed' }

Write-Host '[OK] Monitoring stack up' -ForegroundColor Green
Write-Host '  Grafana:      http://127.0.0.1:3002'
Write-Host '  Prometheus:   http://127.0.0.1:9090'
Write-Host '  Alertmanager: http://127.0.0.1:9093'
Write-Host 'Docs: docs/MONITORING.md · docs/ALERTING.md'
