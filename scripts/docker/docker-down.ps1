<#
.SYNOPSIS
  Stop Regkasse Docker Compose stacks (keep volumes by default).

.PARAMETER Dev
  Stop default Dev project (docker compose down). Default if neither switch set.

.PARAMETER Prod
  Stop production-oriented project.

.PARAMETER All
  Stop both Dev and Prod projects.

.PARAMETER Volumes
  Also remove named volumes (-v). DATA LOSS for Postgres/Redis.

.EXAMPLE
  .\scripts\docker-down.ps1
  .\scripts\docker-down.ps1 -Prod
  .\scripts\docker-down.ps1 -All
  .\scripts\docker-down.ps1 -Volumes   # destructive
#>
[CmdletBinding()]
param(
    [switch]$Dev,
    [switch]$Prod,
    [switch]$All,
    [switch]$Volumes
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if ($All) {
    $Dev = $true
    $Prod = $true
}
elseif (-not $Dev -and -not $Prod) {
    $Dev = $true
}

function Invoke-Down {
    param([string[]]$ComposeArgs, [string]$Label)
    $a = @('compose') + $ComposeArgs + @('down')
    if ($Volumes) { $a += '-v' }
    Write-Host "=== Down: $Label ===" -ForegroundColor Cyan
    Write-Host ("docker " + ($a -join ' ')) -ForegroundColor DarkGray
    & docker @a
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] $Label" -ForegroundColor Green
    }
    else {
        Write-Warning "down returned $LASTEXITCODE for $Label (stack may already be stopped)"
    }
}

if ($Dev) {
    Invoke-Down -ComposeArgs @() -Label 'development'
    Invoke-Down -ComposeArgs @('-f', 'docker-compose.dev.yml') -Label 'infra-only (docker-compose.dev.yml)'
}

if ($Prod) {
    $composeArgs = @('-f', 'docker-compose.prod.yml')
    $envFile = Join-Path $repoRoot '.env.production'
    if (Test-Path $envFile) {
        $composeArgs += @('--env-file', '.env.production')
    }
    Invoke-Down -ComposeArgs $composeArgs -Label 'production'
}

if ($Volumes) {
    Write-Warning "Volumes were removed (-Volumes). Database/cache data is gone for those stacks."
}

Write-Host "Done." -ForegroundColor Green
