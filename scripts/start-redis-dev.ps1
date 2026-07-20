<#
.SYNOPSIS
  Start local portable Redis for Regkasse development (Windows).

.DESCRIPTION
  Uses tools/redis (tporadowski Redis 5.x). Downloads the zip on first run.
  Default bind: localhost:6379 — matches Redis:ConnectionString in appsettings.

.EXAMPLE
  .\scripts\start-redis-dev.ps1
  .\scripts\start-redis-dev.ps1 -PingOnly
#>
[CmdletBinding()]
param(
    [switch]$PingOnly,
    [switch]$Stop
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$redisDir = Join-Path $repoRoot "tools\redis"
$serverExe = Join-Path $redisDir "redis-server.exe"
$cliExe = Join-Path $redisDir "redis-cli.exe"
$conf = Join-Path $redisDir "redis.windows.conf"
$zipPath = Join-Path $redisDir "Redis-x64-5.0.14.1.zip"
$zipUrl = "https://github.com/tporadowski/redis/releases/download/v5.0.14.1/Redis-x64-5.0.14.1.zip"

function Test-RedisPort {
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $iar = $tcp.BeginConnect("127.0.0.1", 6379, $null, $null)
        $ok = $iar.AsyncWaitHandle.WaitOne(500) -and $tcp.Connected
        $tcp.Close()
        return $ok
    } catch {
        return $false
    }
}

function Ensure-RedisBinaries {
    if (Test-Path $serverExe) { return }
    New-Item -ItemType Directory -Force -Path $redisDir | Out-Null
    Write-Host "Downloading portable Redis to $redisDir ..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
    Expand-Archive -Path $zipPath -DestinationPath $redisDir -Force
    if (-not (Test-Path $serverExe)) {
        throw "redis-server.exe missing after extract"
    }
}

Ensure-RedisBinaries

if ($Stop) {
    Get-Process redis-server -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "Stopped redis-server (if running)."
    exit 0
}

if ($PingOnly) {
    if (-not (Test-RedisPort)) {
        Write-Error "Redis is not listening on 127.0.0.1:6379"
        exit 1
    }
    & $cliExe ping
    exit $LASTEXITCODE
}

if (-not (Test-RedisPort)) {
    Write-Host "Starting redis-server on localhost:6379 ..."
    Start-Process -FilePath $serverExe -ArgumentList "`"$conf`"" -WorkingDirectory $redisDir -WindowStyle Hidden
    $deadline = (Get-Date).AddSeconds(10)
    while (-not (Test-RedisPort) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300
    }
    if (-not (Test-RedisPort)) {
        throw "Redis failed to start on port 6379"
    }
} else {
    Write-Host "Redis already listening on localhost:6379"
}

$result = & $cliExe ping
Write-Host "redis-cli ping => $result"
if ($result -ne "PONG") {
    exit 1
}
