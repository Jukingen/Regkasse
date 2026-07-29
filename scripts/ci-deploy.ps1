<#
.SYNOPSIS
  CI/CD deploy helper — invoke stage webhook + optional smoke + rollback hook.

.DESCRIPTION
  Mirrors the core of deploy-backend-stage.yml for local/ops use.
  Does not replace GitHub Environments / compliance gates.

.PARAMETER Stage
  staging | canary | production

.PARAMETER Image
  Fully-qualified image reference (required for deploy webhook).

.PARAMETER ApiBase
  Smoke API base URL (no trailing slash).

.PARAMETER DeployWebhookUrl
  Override; else env BACKEND_<STAGE>_DEPLOY_WEBHOOK_URL or DEPLOY_WEBHOOK_URL.

.PARAMETER RollbackWebhookUrl
  Override; else env BACKEND_<STAGE>_ROLLBACK_WEBHOOK_URL or ROLLBACK_WEBHOOK_URL.

.PARAMETER SkipSmoke
  Skip scripts/smoke-test.sh after deploy.

.PARAMETER AutoRollback
  On smoke failure, call rollback webhook (default: true for staging/canary).

.PARAMETER DryRun
  Print actions without calling webhooks or smoke.

.EXAMPLE
  .\scripts\ci-deploy.ps1 -Stage staging -Image ghcr.io/org/regkasse-api:sha-abc1234 `
    -ApiBase https://api.staging.regkasse.at
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('staging', 'canary', 'production')]
    [string]$Stage,

    [Parameter(Mandatory = $true)]
    [string]$Image,

    [string]$ApiBase = '',

    [string]$DeployWebhookUrl = '',
    [string]$RollbackWebhookUrl = '',

    [string]$GitSha = '',
    [string]$GitRef = '',
    [string]$TenantIds = 'smoke',
    [string]$ReleaseStage = '',

    [switch]$SkipSmoke,
    [bool]$AutoRollback = $true,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $ReleaseStage) { $ReleaseStage = $Stage }
if (-not $GitSha) { $GitSha = (git rev-parse HEAD 2>$null); if (-not $GitSha) { $GitSha = 'local' } }
if (-not $GitRef) { $GitRef = (git rev-parse --abbrev-ref HEAD 2>$null); if (-not $GitRef) { $GitRef = 'local' } }

$stageUpper = $Stage.ToUpperInvariant()
if (-not $DeployWebhookUrl) {
    $DeployWebhookUrl = [Environment]::GetEnvironmentVariable("BACKEND_${stageUpper}_DEPLOY_WEBHOOK_URL")
    if (-not $DeployWebhookUrl) { $DeployWebhookUrl = $env:DEPLOY_WEBHOOK_URL }
}
if (-not $RollbackWebhookUrl) {
    $RollbackWebhookUrl = [Environment]::GetEnvironmentVariable("BACKEND_${stageUpper}_ROLLBACK_WEBHOOK_URL")
    if (-not $RollbackWebhookUrl) { $RollbackWebhookUrl = $env:ROLLBACK_WEBHOOK_URL }
}

if (-not $ApiBase) {
    switch ($Stage) {
        'staging' { $ApiBase = if ($env:BACKEND_STAGING_API_BASE_URL) { $env:BACKEND_STAGING_API_BASE_URL } else { 'https://api.staging.regkasse.at' } }
        'canary' { $ApiBase = if ($env:BACKEND_CANARY_API_BASE_URL) { $env:BACKEND_CANARY_API_BASE_URL } else { 'https://api.regkasse.at' } }
        'production' { $ApiBase = if ($env:BACKEND_PRODUCTION_API_BASE_URL) { $env:BACKEND_PRODUCTION_API_BASE_URL } else { 'https://api.regkasse.at' } }
    }
}
$ApiBase = $ApiBase.TrimEnd('/')

if ($Stage -eq 'production') {
    $AutoRollback = $false
    Write-Host 'Production: auto-rollback disabled (manual / FA rollback).' -ForegroundColor Yellow
}

$payload = @{
    image         = $Image
    stage         = $Stage
    releaseStage  = $ReleaseStage
    sha           = $GitSha
    ref           = $GitRef
    tenantIds     = $TenantIds
} | ConvertTo-Json -Compress

Write-Host "=== CI deploy: stage=$Stage image=$Image ===" -ForegroundColor Cyan

if (-not $DeployWebhookUrl) {
    Write-Warning 'DEPLOY_WEBHOOK_URL not set — skipping webhook (image must be pulled manually).'
}
elseif ($DryRun) {
    Write-Host "[dry-run] POST deploy webhook: $payload" -ForegroundColor DarkGray
}
else {
    Invoke-RestMethod -Method Post -Uri $DeployWebhookUrl -ContentType 'application/json' -Body $payload | Out-Null
    Write-Host '[OK] Deploy webhook invoked' -ForegroundColor Green
    Start-Sleep -Seconds 20
}

if ($SkipSmoke) {
    Write-Host 'Smoke skipped (-SkipSmoke).'
    exit 0
}

$smokeSh = Join-Path $repoRoot 'scripts\smoke-test.sh'
if (-not (Test-Path $smokeSh)) { throw "Missing $smokeSh" }

Write-Host "=== Smoke: API_BASE=$ApiBase ===" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host '[dry-run] would run scripts/smoke-test.sh' -ForegroundColor DarkGray
    exit 0
}

$env:API_BASE = $ApiBase
$env:TENANT_ID = ($TenantIds -split ',')[0].Trim()
$env:REQUIRE_READY = '1'
$env:REQUIRE_MIGRATIONS = '1'
$env:REQUIRE_DEP_EXPORT = '1'
$env:SMOKE_POS_PAYMENT = '0'

$bash = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bash) {
    throw 'bash not found (Git Bash / WSL required to run scripts/smoke-test.sh on Windows)'
}

& bash $smokeSh
$smokeCode = $LASTEXITCODE

if ($smokeCode -eq 0) {
    Write-Host '[OK] Smoke passed' -ForegroundColor Green
    exit 0
}

Write-Host '[FAIL] Smoke failed' -ForegroundColor Red
if ($AutoRollback -and $RollbackWebhookUrl) {
    $rb = @{
        action        = 'rollback'
        stage         = $Stage
        failedImage   = $Image
        previousImage = ''
        sha           = $GitSha
    } | ConvertTo-Json -Compress
    Write-Host 'Invoking rollback webhook...' -ForegroundColor Yellow
    try {
        Invoke-RestMethod -Method Post -Uri $RollbackWebhookUrl -ContentType 'application/json' -Body $rb | Out-Null
        Write-Host '[OK] Rollback webhook invoked' -ForegroundColor Green
    }
    catch {
        Write-Warning "Rollback webhook failed: $_"
    }
}
elseif ($Stage -eq 'production') {
    Write-Host 'Manual rollback required. See docs/CI_CD.md and rollback.bat / FA /admin/deployments.' -ForegroundColor Yellow
}
elseif (-not $RollbackWebhookUrl) {
    Write-Warning 'ROLLBACK_WEBHOOK_URL not set — manual rollback required.'
}

exit $smokeCode
