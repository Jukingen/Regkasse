#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Dry-run / structural test plan for Regkasse Windows .bat scripts.

.DESCRIPTION
  Does NOT start long-running servers (start-dev, deploy, docker-up, etc.).
  Validates that expected scripts exist, look like proper batch wrappers, and that
  their targets (.ps1 / .mjs / npm / curl) are present. Optionally runs verify-bat-ps1-pairing.

  For interactive manual testing, see docs/SCRIPTS_TEST_PLAN.md.

.PARAMETER Strict
  Treat warnings as failures (exit 1).

.PARAMETER SkipPairing
  Do not invoke node scripts/verify-bat-ps1-pairing.mjs.

.PARAMETER Json
  Emit a JSON summary to stdout.

.EXAMPLE
  .\scripts\test\test-scripts.ps1
  .\scripts\test\test-scripts.ps1 -Strict
  npm run test:scripts
#>
[CmdletBinding()]
param(
    [switch]$Strict,
    [switch]$SkipPairing,
    [switch]$Json
)

$ErrorActionPreference = 'Continue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$pass = New-Object System.Collections.Generic.List[string]
$fail = New-Object System.Collections.Generic.List[string]
$warn = New-Object System.Collections.Generic.List[string]

function Add-Pass([string]$Name, [string]$Detail = '') {
    $msg = if ($Detail) { '{0} - {1}' -f $Name, $Detail } else { $Name }
    [void]$pass.Add($msg)
    if (-not $Json) {
        Write-Host ('  [PASS] {0}' -f $msg) -ForegroundColor Green
    }
}

function Add-Fail([string]$Name, [string]$Detail) {
    $msg = '{0} - {1}' -f $Name, $Detail
    [void]$fail.Add($msg)
    if (-not $Json) {
        Write-Host ('  [FAIL] {0}' -f $msg) -ForegroundColor Red
    }
}

function Add-Warn([string]$Name, [string]$Detail) {
    $msg = '{0} - {1}' -f $Name, $Detail
    [void]$warn.Add($msg)
    if (-not $Json) {
        Write-Host ('  [WARN] {0}' -f $msg) -ForegroundColor Yellow
    }
}

function Test-BatFile {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [string]$ExpectTargetPattern = '',
        [string[]]$RequiredSnippets = @('@echo off'),
        [switch]$ExpectPause
    )

    $full = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $full)) {
        Add-Fail $RelativePath 'file missing'
        return
    }

    $content = Get-Content -LiteralPath $full -Raw -ErrorAction Stop
    $lines = Get-Content -LiteralPath $full -TotalCount 5

    if (-not $Json) {
        Write-Host ('Testing: {0}' -f $RelativePath) -ForegroundColor Cyan
        $preview = ($lines | ForEach-Object { $_.TrimEnd() }) -join ' | '
        Write-Host ('  preview: {0}' -f $preview) -ForegroundColor DarkGray
    }

    foreach ($snip in $RequiredSnippets) {
        if ($content -notlike ('*{0}*' -f $snip)) {
            Add-Fail $RelativePath ('missing expected snippet: {0}' -f $snip)
            return
        }
    }

    if ($ExpectPause -and ($content -notmatch '(?im)^\s*pause\b')) {
        Add-Warn $RelativePath 'no pause (OK for helpers called from other bats)'
    }

    if ($ExpectTargetPattern -and ($content -notmatch $ExpectTargetPattern)) {
        Add-Fail $RelativePath ("does not reference expected target /$ExpectTargetPattern/")
        return
    }

    Add-Pass $RelativePath 'exists + structure OK'
}

if (-not $Json) {
    Write-Host ''
    Write-Host '=== Regkasse Scripts Test Plan (dry-run) ===' -ForegroundColor Cyan
    Write-Host ("Repo: {0}" -f $repoRoot)
    Write-Host 'Note: Does not execute long-running start/deploy/docker-up commands.'
    Write-Host ''
}

# --- Root convenience bats ---
if (-not $Json) { Write-Host '## Root convenience' -ForegroundColor Yellow }

$rootBats = @(
    @{ Path = 'scripts\dev\start.bat'; Pattern = 'Legacy Mode|Docker Mode' },
    @{ Path = 'scripts\dev\start-dev.bat'; Pattern = 'npm run dev' },
    @{ Path = 'scripts\dev\start-backend.bat'; Pattern = 'npm run dev:backend' },
    @{ Path = 'scripts\dev\start-admin.bat'; Pattern = 'npm run dev:admin' },
    @{ Path = 'scripts\dev\start-pos.bat'; Pattern = 'npm run dev:pos' },
    @{ Path = 'scripts\dev\start-sites.bat'; Pattern = 'npm run dev:sites' },
    @{ Path = 'scripts\test\test-all.bat'; Pattern = 'dotnet test' },
    @{ Path = 'scripts\dev\clean-all.DANGER.bat'; Pattern = 'dotnet clean|rmdir' },
    @{ Path = 'scripts\docker\host\up.bat'; Pattern = 'docker compose' },
    @{ Path = 'scripts\docker\host\down.bat'; Pattern = 'docker compose' },
    @{ Path = 'scripts\docker\host\clean.DANGER.bat'; Pattern = 'docker compose' },
    @{ Path = 'scripts\docker\host\status.bat'; Pattern = 'docker' },
    @{ Path = 'scripts\ops\deploy.DANGER.bat'; Pattern = 'docker-compose\.prod\.yml' },
    @{ Path = 'scripts\ops\rollback.DANGER.bat'; Pattern = 'git reset --hard' }
)

foreach ($item in $rootBats) {
    $snippets = @('@echo off')
    $base = [IO.Path]::GetFileName($item.Path)
    if ($base -ne 'clean-all.DANGER.bat' -and $base -ne 'start.bat') {
        $snippets += 'ERRORLEVEL'
    }
    Test-BatFile -RelativePath $item.Path -ExpectTargetPattern $item.Pattern -ExpectPause `
        -RequiredSnippets $snippets
}

# PowerShell Compose entry (not a .bat)
$dockerUpPs1 = Join-Path $repoRoot 'scripts\docker\docker-up.ps1'
if (Test-Path -LiteralPath $dockerUpPs1) {
    $psContent = Get-Content -LiteralPath $dockerUpPs1 -Raw
    if ($psContent -match 'docker compose') {
        Add-Pass 'scripts\docker\docker-up.ps1' 'exists + docker compose'
    }
    else {
        Add-Fail 'scripts\docker\docker-up.ps1' 'missing docker compose reference'
    }
}
else {
    Add-Fail 'scripts\docker\docker-up.ps1' 'file missing'
}

# --- scripts/ maintenance & helpers ---
if (-not $Json) { Write-Host ''; Write-Host '## scripts/ helpers' -ForegroundColor Yellow }

$scriptBats = @(
    @{ Path = 'scripts\dev\clean-backend.bat'; Pattern = 'clean-backend-build\.ps1'; Target = 'scripts\dev\clean-backend-build.ps1' },
    @{ Path = 'scripts\dev\dev-purge-tenant.DANGER.bat'; Pattern = 'dev-purge-tenant-catalog\.DANGER\.ps1'; Target = 'scripts\dev\dev-purge-tenant-catalog.DANGER.ps1' },
    @{ Path = 'scripts\rksv\generate-dep-export.bat'; Pattern = 'generate-dep-export-fixtures\.ps1'; Target = 'scripts\rksv\generate-dep-export-fixtures.ps1' },
    @{ Path = 'scripts\rksv\ensure-bmf-prueftool.bat'; Pattern = 'ensure-bmf-prueftool\.ps1'; Target = 'scripts\rksv\ensure-bmf-prueftool.ps1' },
    @{ Path = 'scripts\dev\fix-antd.bat'; Pattern = 'fix-antd-deprecations\.mjs'; Target = 'scripts\fix-antd-deprecations.mjs' },
    @{ Path = 'scripts\dev\dev-mail.bat'; Pattern = 'dev-mail-test\.bat'; Target = 'scripts\dev\dev-mail-test.bat' },
    @{ Path = 'scripts\test\smoke-test.bat'; Pattern = 'curl'; Target = $null },
    @{ Path = 'scripts\lib\run-with-log.bat'; Pattern = 'LOG_FILE'; Target = $null },
    @{ Path = 'scripts\lib\validate-scripts.bat'; Pattern = 'validate-scripts\.ps1'; Target = 'scripts\lib\validate-scripts.ps1' },
    @{ Path = 'scripts\test\test-scripts.bat'; Pattern = 'test-scripts\.ps1'; Target = 'scripts\test\test-scripts.ps1' }
)

foreach ($item in $scriptBats) {
    Test-BatFile -RelativePath $item.Path -ExpectTargetPattern $item.Pattern -ExpectPause `
        -RequiredSnippets @('@echo off')
    if ($item.Target) {
        $t = Join-Path $repoRoot $item.Target
        if (Test-Path -LiteralPath $t) {
            Add-Pass $item.Target 'target present'
        }
        else {
            Add-Fail $item.Path ('target missing: {0}' -f $item.Target)
        }
    }
}

# --- Helpers without pause requirement ---
if (-not $Json) { Write-Host ''; Write-Host '## Shared helpers' -ForegroundColor Yellow }
Test-BatFile -RelativePath 'scripts\lib\_common.bat' -ExpectTargetPattern 'check_error' -RequiredSnippets @('check_error', 'success')

# --- Docs presence ---
if (-not $Json) { Write-Host ''; Write-Host '## Documentation files' -ForegroundColor Yellow }
$docs = @(
    'docs\SCRIPTS_REFERENCE.md',
    'docs\SCRIPTS_QUICK_REF.md',
    'docs\SCRIPTS_ECOSYSTEM.md',
    'docs\SCRIPTS_TEST_PLAN.md',
    'docs\SCRIPTS_COMPLETION_SUMMARY.md',
    'docs\BATCH_FILES.md',
    'scripts\README.md'
)
foreach ($doc in $docs) {
    $p = Join-Path $repoRoot $doc
    if (Test-Path -LiteralPath $p) {
        Add-Pass $doc 'present'
    }
    else {
        Add-Fail $doc 'missing'
    }
}

# --- Pairing gate ---
if (-not $SkipPairing) {
    if (-not $Json) { Write-Host ''; Write-Host '## Pairing (verify-bat-ps1-pairing.mjs)' -ForegroundColor Yellow }
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) {
        Add-Warn 'verify-bat-ps1' 'node not on PATH - skipped'
    }
    else {
        & node (Join-Path $repoRoot 'scripts\verify-bat-ps1-pairing.mjs') | Out-Host
        if ($LASTEXITCODE -eq 0) {
            Add-Pass 'verify-bat-ps1-pairing.mjs' 'exit 0'
        }
        else {
            Add-Fail 'verify-bat-ps1-pairing.mjs' ("exit {0}" -f $LASTEXITCODE)
        }
    }
}

# --- Manual follow-ups (documented only) ---
if (-not $Json) {
    Write-Host ''
    Write-Host '## Manual / interactive (not auto-run)' -ForegroundColor Yellow
    Write-Host '  1. scripts\dev\start-dev.bat / start-*.bat   -> start stack, Ctrl+C, expect clean pause'
    Write-Host '  2. scripts\docker\host\up.bat -> status.bat -> down.bat'
    Write-Host '  3. scripts\test\smoke-test.bat         -> with API/Admin/POS up'
    Write-Host '  4. scripts\test\run-comprehensive-smoke.bat -> full suite'
    Write-Host '  5. deploy.DANGER.bat / rollback.DANGER.bat      -> only on intentional prod Compose host'
    Write-Host '  See docs/SCRIPTS_TEST_PLAN.md'
    Write-Host ''
}

$summary = [pscustomobject]@{
    passCount = $pass.Count
    failCount = $fail.Count
    warnCount = $warn.Count
    pass      = @($pass)
    fail      = @($fail)
    warn      = @($warn)
    strict    = [bool]$Strict
}

if ($Json) {
    $summary | ConvertTo-Json -Depth 4
}
else {
    Write-Host '=== SUMMARY ===' -ForegroundColor Cyan
    Write-Host ('PASS={0} FAIL={1} WARN={2}' -f $pass.Count, $fail.Count, $warn.Count)
    if ($fail.Count -gt 0) {
        Write-Host 'Failures:' -ForegroundColor Red
        $fail | ForEach-Object { Write-Host ('  - {0}' -f $_) }
    }
    if ($warn.Count -gt 0) {
        Write-Host 'Warnings:' -ForegroundColor Yellow
        $warn | ForEach-Object { Write-Host ('  - {0}' -f $_) }
    }
    Write-Host ''
    if ($fail.Count -eq 0) {
        Write-Host 'OK (dry-run structural checks finished).' -ForegroundColor Green
    }
    else {
        Write-Host 'FAILED ÔÇö fix missing/outdated .bat wrappers before merge.' -ForegroundColor Red
    }
}

$exit = 0
if ($fail.Count -gt 0) { $exit = 1 }
elseif ($Strict -and $warn.Count -gt 0) { $exit = 1 }
exit $exit
