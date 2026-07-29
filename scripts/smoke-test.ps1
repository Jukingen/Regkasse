# Regkasse deployment smoke test (PowerShell).
# Prefer scripts/smoke-test.sh on Linux CI; this wrapper is for Windows operators.
#
# Usage:
#   $env:API_BASE='https://api.staging.regkasse.at'; $env:TENANT_ID='smoke'
#   .\scripts\smoke-test.ps1
#
# Docs: docs/DEPLOYMENT_SMOKE_TEST.md

[CmdletBinding()]
param(
    [string]$ApiBase = $env:API_BASE,
    [string]$TenantId = $(if ($env:TENANT_ID) { $env:TENANT_ID } else { 'smoke' }),
    [string]$LoginIdentifier = $(if ($env:LOGIN_IDENTIFIER) { $env:LOGIN_IDENTIFIER } else { 'admin@admin.com' }),
    [string]$LoginPassword = $(if ($env:LOGIN_PASSWORD) { $env:LOGIN_PASSWORD } else { 'Admin123!' }),
    [string]$FaBase = $env:FA_BASE,
    [string]$PosBase = $env:POS_BASE,
    [switch]$PosPayment,
    [string]$ProductId = $env:SMOKE_PRODUCT_ID,
    [string]$CashRegisterId = $env:SMOKE_CASH_REGISTER_ID
)

$ErrorActionPreference = 'Continue'

if ([string]::IsNullOrWhiteSpace($ApiBase)) {
    Write-Error 'API_BASE is required'
    exit 2
}

$ApiBase = $ApiBase.TrimEnd('/')
if ($PosPayment) { $env:SMOKE_POS_PAYMENT = '1' }

# Prefer bash script when available (Git Bash / WSL)
$bash = Get-Command bash -ErrorAction SilentlyContinue
$sh = Join-Path $PSScriptRoot 'smoke-test.sh'
if ($bash -and (Test-Path $sh)) {
    $env:API_BASE = $ApiBase
    $env:TENANT_ID = $TenantId
    $env:LOGIN_IDENTIFIER = $LoginIdentifier
    $env:LOGIN_PASSWORD = $LoginPassword
    if ($FaBase) { $env:FA_BASE = $FaBase }
    if ($PosBase) { $env:POS_BASE = $PosBase }
    if ($ProductId) { $env:SMOKE_PRODUCT_ID = $ProductId }
    if ($CashRegisterId) { $env:SMOKE_CASH_REGISTER_ID = $CashRegisterId }
    & bash $sh
    exit $LASTEXITCODE
}

Write-Host 'bash not found — running native PowerShell smoke subset' -ForegroundColor Yellow

function Invoke-Check {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action
        Write-Host "OK  $Name"
        return $true
    }
    catch {
        Write-Host "FAIL $Name — $($_.Exception.Message)"
        return $false
    }
}

$ok = $true
$ok = (Invoke-Check 'api.health.live' {
    $r = Invoke-WebRequest -Uri "$ApiBase/api/health/live" -UseBasicParsing -TimeoutSec 30
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
}) -and $ok

$loginBody = @{
    loginIdentifier = $LoginIdentifier
    password        = $LoginPassword
    clientApp       = 'admin'
} | ConvertTo-Json

$token = $null
$ok = (Invoke-Check 'fa.login' {
    $r = Invoke-WebRequest -Uri "$ApiBase/api/Auth/login" -Method POST -Body $loginBody `
        -ContentType 'application/json' -Headers @{ 'X-Tenant-Id' = $TenantId } `
        -UseBasicParsing -TimeoutSec 30
    $j = $r.Content | ConvertFrom-Json
    $script:token = $j.accessToken
    if (-not $script:token) { $script:token = $j.token }
    if (-not $script:token) { throw 'no token' }
}) -and $ok

if ($token) {
    $ok = (Invoke-Check 'rksv.environment' {
        $r = Invoke-WebRequest -Uri "$ApiBase/api/rksv/environment" `
            -Headers @{ Authorization = "Bearer $token"; 'X-Tenant-Id' = $TenantId } `
            -UseBasicParsing -TimeoutSec 30
        if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    }) -and $ok
}

if ($ok) {
    Write-Host 'Smoke PASSED'
    exit 0
}
Write-Host 'Smoke FAILED'
exit 1
