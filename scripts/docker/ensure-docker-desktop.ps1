#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Guide / attempt Docker Desktop + WSL 2 install for Regkasse on Windows.

.DESCRIPTION
  Detects whether Docker CLI exists and whether the engine is reachable.
  Prints Admin steps (WSL + winget). Optionally launches winget install when
  -Install is passed (requires elevation for a clean install).

.PARAMETER Install
  Run: winget install --id Docker.DockerDesktop -e

.PARAMETER SkipWslHint
  Do not print WSL install commands.

.EXAMPLE
  .\scripts\docker\ensure-docker-desktop.ps1
  .\scripts\docker\ensure-docker-desktop.ps1 -Install
#>
[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$SkipWslHint
)

$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

Write-Host ''
Write-Host '=== Regkasse: ensure Docker Desktop ===' -ForegroundColor Cyan
Write-Host ("Repo: {0}" -f $repoRoot)
Write-Host ''

$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerCmd) {
    Write-Host '[FAIL] docker CLI not found (Docker Desktop not installed or not on PATH).' -ForegroundColor Red
    Write-Host ''
    Write-Host 'This is why scripts\docker\host\up.bat and frontend/admin Compose cannot start.' -ForegroundColor Yellow
    Write-Host 'Compose files in the repo are fine - the host engine is missing.' -ForegroundColor Yellow
    Write-Host ''

    if (-not $SkipWslHint) {
        Write-Host 'Step A - WSL 2 (Admin PowerShell):' -ForegroundColor Cyan
        Write-Host '  wsl --install'
        Write-Host '  # reboot if prompted, then:'
        Write-Host '  wsl --set-default-version 2'
        Write-Host ''
    }

    Write-Host 'Step B - Docker Desktop:' -ForegroundColor Cyan
    Write-Host '  winget install --id Docker.DockerDesktop -e --accept-package-agreements --accept-source-agreements'
    Write-Host '  # or download: https://docs.docker.com/desktop/setup/install/windows-install/'
    Write-Host ''
    Write-Host 'Step C - After install:' -ForegroundColor Cyan
    Write-Host '  1) Start Docker Desktop, wait until Engine is running'
    Write-Host '  2) Open a NEW terminal'
    Write-Host '  3) docker version'
    Write-Host '  4) scripts\docker\host\up.bat'
    Write-Host '     or: .\scripts\docker\docker-up.ps1 -Build'
    Write-Host ''
    Write-Host 'Docs: docs\DOCKER_WINDOWS_SETUP.md' -ForegroundColor DarkGray
    Write-Host 'Diagnose: .\scripts\docker\docker-diagnose.ps1' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host 'Without Docker (works today): scripts\dev\start.bat -> [1] Legacy' -ForegroundColor Green
    Write-Host '  or scripts\dev\start-dev.bat' -ForegroundColor Green
    Write-Host ''

    if ($Install) {
        if (-not (Test-IsAdmin)) {
            Write-Host '[WARN] -Install requested but shell is not Admin. Elevating winget may still prompt UAC.' -ForegroundColor Yellow
        }
        Write-Host 'Launching winget install Docker.DockerDesktop ...' -ForegroundColor Cyan
        & winget install --id Docker.DockerDesktop -e --accept-package-agreements --accept-source-agreements
        exit $LASTEXITCODE
    }

    exit 1
}

Write-Host ('[OK] docker CLI: {0}' -f $dockerCmd.Source) -ForegroundColor Green

& docker info 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host '[FAIL] Docker CLI found but engine is not running.' -ForegroundColor Red
    Write-Host '  Start Docker Desktop and wait for Engine running.' -ForegroundColor Yellow
    Write-Host '  If stuck: wsl --shutdown  then restart Docker Desktop.' -ForegroundColor Yellow
    exit 2
}

Write-Host '[OK] Docker engine reachable.' -ForegroundColor Green
& docker compose version 2>$null
Write-Host ''
Write-Host 'Ready. Next:' -ForegroundColor Green
Write-Host '  scripts\docker\host\up.bat'
Write-Host '  .\scripts\docker\docker-up.ps1 -Build'
exit 0
