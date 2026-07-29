#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Validates Windows script pairing and documentation coverage.

.DESCRIPTION
  1) Lists root .bat convenience helpers.
  2) Runs node scripts/verify-bat-ps1-pairing.mjs (allowlisted .bat/.ps1 pairs).
  3) Ensures required scripts appear in docs/SCRIPTS_REFERENCE.md.
  4) Soft-warns on excessive blank lines in root .bat files.
  5) Optionally fails if docs/SCRIPTS_TEST_PLAN.md is missing.

  Naive "every .bat needs same-name .ps1" is NOT used for root npm wrappers
  (start-dev.bat, docker-up.bat, ...) or aliases (smoke-test.bat, fix-antd.bat).

.PARAMETER SkipPairing
  Skip the Node pairing check.

.PARAMETER SkipDocs
  Skip documentation coverage check.

.PARAMETER Json
  Emit JSON summary.

.EXAMPLE
  .\scripts\validate-scripts.ps1
  .\scripts\validate-scripts.ps1 -SkipPairing
  npm run validate:scripts
#>
[CmdletBinding()]
param(
    [switch]$SkipPairing,
    [switch]$SkipDocs,
    [switch]$Json
)

$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Add-Err([string]$Message) {
    [void]$errors.Add($Message)
    if (-not $Json) { Write-Host ('  [ERROR] {0}' -f $Message) -ForegroundColor Red }
}

function Add-Warning([string]$Message) {
    [void]$warnings.Add($Message)
    if (-not $Json) { Write-Host ('  [WARN] {0}' -f $Message) -ForegroundColor Yellow }
}

$rootBatFiles = Get-ChildItem -LiteralPath $repoRoot -Filter '*.bat' -File |
    Where-Object { $_.DirectoryName -eq $repoRoot } |
    Sort-Object Name

if (-not $Json) {
    Write-Host ''
    Write-Host '=== Regkasse validate-scripts ===' -ForegroundColor Cyan
    Write-Host ("Repo: {0}" -f $repoRoot)
    Write-Host ''
    Write-Host 'Validating script ecosystem...' -ForegroundColor Cyan
    Write-Host ''
    Write-Host ("Found {0} root .bat files:" -f $rootBatFiles.Count) -ForegroundColor Yellow
    foreach ($bat in $rootBatFiles) {
        Write-Host ('  [OK] {0}' -f $bat.Name) -ForegroundColor Green
    }
    Write-Host ''
}

# Soft syntax / style checks on root bats
if (-not $Json) { Write-Host '## Style checks (root .bat)' -ForegroundColor Yellow }
foreach ($bat in $rootBatFiles) {
    $content = Get-Content -LiteralPath $bat.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) {
        Add-Err ("Cannot read {0}" -f $bat.Name)
        continue
    }
    if ($content -notmatch '(?im)^\s*@echo\s+off\b') {
        Add-Warning ("{0}: missing @echo off" -f $bat.Name)
    }
    if ($content -match "`r`n`r`n`r`n") {
        Add-Warning ("{0}: has extra blank lines" -f $bat.Name)
    }
}

# --- 1) Pairing (source of truth: verify-bat-ps1-pairing.mjs) ---
if (-not $SkipPairing) {
    if (-not $Json) {
        Write-Host ''
        Write-Host '## Pairing (.bat / .ps1)' -ForegroundColor Yellow
    }
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) {
        Add-Err 'node is not on PATH (required for verify-bat-ps1-pairing.mjs)'
    }
    else {
        & node (Join-Path $repoRoot 'scripts\verify-bat-ps1-pairing.mjs')
        if ($LASTEXITCODE -ne 0) {
            Add-Err 'verify-bat-ps1-pairing.mjs failed (see output above)'
        }
        elseif (-not $Json) {
            Write-Host '  [OK] Pairing check passed' -ForegroundColor Green
        }
    }
}

# --- 2) Documentation coverage ---
if (-not $SkipDocs) {
    if (-not $Json) {
        Write-Host ''
        Write-Host '## Documentation (docs/SCRIPTS_REFERENCE.md)' -ForegroundColor Yellow
    }

    $refPath = Join-Path $repoRoot 'docs\SCRIPTS_REFERENCE.md'
    $planPath = Join-Path $repoRoot 'docs\SCRIPTS_TEST_PLAN.md'

    if (-not (Test-Path -LiteralPath $refPath)) {
        Add-Err 'docs/SCRIPTS_REFERENCE.md is missing'
    }
    else {
        if (-not $Json) {
            Write-Host '  [OK] Documentation file exists' -ForegroundColor Green
        }
        $refText = Get-Content -LiteralPath $refPath -Raw

        foreach ($bat in $rootBatFiles) {
            $name = $bat.Name
            if ($refText -notlike ('*{0}*' -f $name)) {
                Add-Err ("Root .bat not documented in SCRIPTS_REFERENCE.md: {0}" -f $name)
            }
            elseif (-not $Json) {
                Write-Host ('  [OK] documented: {0}' -f $name) -ForegroundColor Green
            }
        }

        $ps1Files = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'scripts') -Filter '*.ps1' -File |
            Select-Object -ExpandProperty Name

        foreach ($name in $ps1Files) {
            if ($refText -notlike ('*{0}*' -f $name)) {
                Add-Err ("scripts/{0} not documented in SCRIPTS_REFERENCE.md" -f $name)
            }
            elseif (-not $Json) {
                Write-Host ('  [OK] documented: scripts/{0}' -f $name) -ForegroundColor Green
            }
        }

        $aliasBats = @(
            'clean-backend.bat',
            'dev-purge-tenant.bat',
            'generate-dep-export.bat',
            'ensure-bmf-prueftool.bat',
            'fix-antd.bat',
            'dev-mail.bat',
            'smoke-test.bat',
            'run-with-log.bat',
            '_common.bat'
        )
        foreach ($name in $aliasBats) {
            if ($refText -notlike ('*{0}*' -f $name)) {
                Add-Err ("Alias .bat not documented in SCRIPTS_REFERENCE.md: {0}" -f $name)
            }
        }
    }

    if (-not (Test-Path -LiteralPath $planPath)) {
        Add-Err 'docs/SCRIPTS_TEST_PLAN.md is missing'
    }
    elseif (-not $Json) {
        Write-Host '  [OK] docs/SCRIPTS_TEST_PLAN.md present' -ForegroundColor Green
    }
}

# --- Summary ---
$result = [pscustomobject]@{
    ok           = ($errors.Count -eq 0)
    rootBatCount = $rootBatFiles.Count
    errorCount   = $errors.Count
    warnCount    = $warnings.Count
    errors       = @($errors)
    warnings     = @($warnings)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 4
}
else {
    Write-Host ''
    if ($errors.Count -gt 0) {
        Write-Host 'ERROR: Script validation failed:' -ForegroundColor Red
        $errors | ForEach-Object { Write-Host ('  {0}' -f $_) -ForegroundColor Red }
        Write-Host ''
        Write-Host 'Fix: document in docs/SCRIPTS_REFERENCE.md, or adjust pairing allowlists in verify-bat-ps1-pairing.mjs' -ForegroundColor Yellow
    }
    else {
        Write-Host 'Validation complete!' -ForegroundColor Green
        Write-Host 'All script pairing and documentation checks passed!' -ForegroundColor Green
        if ($warnings.Count -gt 0) {
            Write-Host ("({0} warning(s) - non-fatal)" -f $warnings.Count) -ForegroundColor Yellow
        }
    }
}

exit $(if ($errors.Count -gt 0) { 1 } else { 0 })
