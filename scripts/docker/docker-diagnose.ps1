<#
.SYNOPSIS
  Diagnose Docker Desktop + WSL 2 readiness for Regkasse on Windows.

.DESCRIPTION
  Checks Docker CLI, Compose v2, WSL distros, engine connectivity, and
  common Regkasse host ports (API, Postgres, Redis, Admin, POS, Sites).

.EXAMPLE
  .\scripts\docker-diagnose.ps1
  .\scripts\docker-diagnose.ps1 -SkipPull

.NOTES
  Docs: docs/DOCKER_WINDOWS_TROUBLESHOOTING.md · docs/DOCKER_WINDOWS_SETUP.md
#>
[CmdletBinding()]
param(
    [switch]$SkipPull
)

$ErrorActionPreference = 'Continue'
$failed = 0

function Write-Section([string]$Title) {
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

function Write-Ok([string]$Message) {
    Write-Host "[OK]  $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Fail([string]$Message) {
    Write-Host "[FAIL] $Message" -ForegroundColor Red
    $script:failed++
}

Write-Host "Regkasse Docker diagnose (Windows)" -ForegroundColor White
Write-Host "Repo root: $(Split-Path -Parent $PSScriptRoot)"

# --- Docker CLI ---
Write-Section "Checking Docker..."
try {
    $dockerVersion = & docker --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw $dockerVersion }
    Write-Ok $dockerVersion
}
catch {
    Write-Fail "docker --version failed. Is Docker Desktop installed and on PATH?"
    Write-Host "  Fix: install Desktop (docs/DOCKER_WINDOWS_SETUP.md), start it, open a new terminal."
}

# --- Compose ---
Write-Section "Checking Docker Compose..."
try {
    $composeVersion = & docker compose version 2>&1
    if ($LASTEXITCODE -ne 0) { throw $composeVersion }
    Write-Ok $composeVersion
}
catch {
    Write-Fail "docker compose version failed. Compose V2 plugin required."
}

# --- WSL ---
Write-Section "Checking WSL..."
try {
    $wslList = & wsl --list --verbose 2>&1
    if ($LASTEXITCODE -ne 0) { throw $wslList }
    Write-Host ($wslList | Out-String).TrimEnd()
    $wslText = ($wslList | Out-String)
    if ($wslText -match '\s2\s') {
        Write-Ok "At least one WSL 2 distro detected."
    }
    else {
        Write-Warn "No VERSION 2 distro visible. Run: wsl --install / wsl --set-default-version 2"
    }
}
catch {
    Write-Fail "wsl --list --verbose failed. Run Admin: wsl --install (then reboot)."
}

# --- Engine ---
Write-Section "Checking Docker engine..."
try {
    $null = & docker info 2>&1
    if ($LASTEXITCODE -ne 0) { throw "docker info exit $LASTEXITCODE" }
    Write-Ok "docker info succeeded (engine reachable)."
}
catch {
    Write-Fail "Docker engine not reachable (Desktop not running or stuck)."
    Write-Host "  Fix: start Docker Desktop; if stuck: wsl --shutdown then restart Desktop."
}

# --- Optional pull ---
if (-not $SkipPull) {
    Write-Section "Checking image pull (hello-world)..."
    try {
        $null = & docker pull hello-world 2>&1
        if ($LASTEXITCODE -ne 0) { throw "pull failed" }
        Write-Ok "docker pull hello-world succeeded."
    }
    catch {
        Write-Warn "Could not pull hello-world (network/proxy/Hub). See docs/DOCKER_WINDOWS_TROUBLESHOOTING.md § Network."
    }
}
else {
    Write-Section "Skipping image pull (-SkipPull)"
}

# --- Ports ---
Write-Section "Checking ports..."
$ports = @(5184, 5432, 6379, 3000, 8081, 3001)
Write-Host "Listening sockets matching Regkasse defaults (5184 5432 6379 3000 8081 3001):"
$any = $false
foreach ($port in $ports) {
    $lines = netstat -ano | Select-String ":$port\s"
    if ($lines) {
        $any = $true
        Write-Host "--- :$port ---" -ForegroundColor DarkGray
        $lines | ForEach-Object { Write-Host $_.Line }
    }
}
if (-not $any) {
    Write-Host "(none of these ports are listening — OK if Compose is not running)"
}
else {
    Write-Warn "If Compose fails with 'port is already allocated', stop the PID above or change .env ports."
}

# --- Compose project (optional) ---
Write-Section "Checking Compose project (if present)..."
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'docker-compose.yml'
if (Test-Path $composeFile) {
    Push-Location $repoRoot
    try {
        $ps = & docker compose ps 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host ($ps | Out-String).TrimEnd()
            Write-Ok "docker compose ps OK"
        }
        else {
            Write-Warn "docker compose ps returned non-zero (stack may be stopped)."
        }
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Warn "docker-compose.yml not found at repo root."
}

Write-Host ""
if ($failed -eq 0) {
    Write-Host "Diagnose finished: no hard failures ($failed)." -ForegroundColor Green
    Write-Host "Next: docs/DOCKER_WINDOWS_TROUBLESHOOTING.md if something still misbehaves."
    exit 0
}
else {
    Write-Host "Diagnose finished: $failed hard failure(s)." -ForegroundColor Red
    Write-Host "See docs/DOCKER_WINDOWS_TROUBLESHOOTING.md"
    exit 1
}
