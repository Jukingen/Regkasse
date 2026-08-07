#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Validates Windows script pairing and documentation coverage.

.DESCRIPTION
  1) Summarizes scripts/<category> .bat / .ps1 inventory.
  2) Asserts repo root has zero .bat files.
  3) Runs node scripts/verify-bat-ps1-pairing.mjs (allowlisted .bat/.ps1 pairs).
  4) Ensures required scripts appear in docs/SCRIPTS_REFERENCE.md.
  5) Optionally fails if docs/SCRIPTS_TEST_PLAN.md is missing.

.PARAMETER SkipPairing
  Skip the Node pairing check.

.PARAMETER SkipDocs
  Skip documentation coverage check.

.PARAMETER Json
  Emit JSON summary.

.EXAMPLE
  .\scripts\lib\validate-scripts.ps1
  npm run validate:scripts
#>
[CmdletBinding()]
param(
    [switch]$SkipPairing,
    [switch]$SkipDocs,
    [switch]$Json
)

$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
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

$categoryDirs = @(
    'dev', 'docker', 'docker\host', 'legacy', 'ci', 'rksv', 'test', 'ops', 'lib'
)

$categoryBatFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
$categoryPs1Files = New-Object System.Collections.Generic.List[System.IO.FileInfo]
foreach ($rel in $categoryDirs) {
    $dir = Join-Path $repoRoot (Join-Path 'scripts' $rel)
    if (-not (Test-Path -LiteralPath $dir)) { continue }
    Get-ChildItem -LiteralPath $dir -Filter '*.bat' -File -ErrorAction SilentlyContinue |
        ForEach-Object { [void]$categoryBatFiles.Add($_) }
    Get-ChildItem -LiteralPath $dir -Filter '*.ps1' -File -ErrorAction SilentlyContinue |
        ForEach-Object { [void]$categoryPs1Files.Add($_) }
}

if (-not $Json) {
    Write-Host ''
    Write-Host '=== Regkasse validate-scripts ===' -ForegroundColor Cyan
    Write-Host ("Repo: {0}" -f $repoRoot)
    Write-Host ''
    Write-Host 'Validating script ecosystem...' -ForegroundColor Cyan
    Write-Host ''
    Write-Host ("Root .bat files: {0} (must be 0)" -f $rootBatFiles.Count) -ForegroundColor Yellow
    foreach ($bat in $rootBatFiles) {
        Write-Host ('  [ERR] {0}' -f $bat.Name) -ForegroundColor Red
        Add-Err ("Root .bat is forbidden: {0} (move under scripts/<category>/)" -f $bat.Name)
    }
    Write-Host ("Category .bat: {0}  .ps1: {1}" -f $categoryBatFiles.Count, $categoryPs1Files.Count) -ForegroundColor Yellow
    Write-Host ''
}

# Soft syntax / style checks on category entry bats
if (-not $Json) { Write-Host '## Style checks (category .bat)' -ForegroundColor Yellow }
foreach ($bat in $categoryBatFiles) {
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

        $mustDocument = @(
            'start.bat',
            'start-dev.bat',
            'start-backend.bat',
            'start-admin.bat',
            'start-pos.bat',
            'start-sites.bat',
            'clean-all.DANGER.bat',
            'test-all.bat',
            'deploy.DANGER.bat',
            'rollback.DANGER.bat',
            'docker-up.ps1',
            'docker-down.ps1',
            'docker-build.ps1',
            'docker-deploy.ps1',
            'docker-diagnose.ps1',
            'docker/host/up.bat',
            'docker/host/clean.DANGER.bat',
            'smoke-test.bat',
            'validate-scripts.ps1',
            'test-scripts.ps1',
            'verify-bat-ps1-pairing.mjs',
            'clean-backend.bat',
            'dev-purge-tenant.DANGER.bat',
            'generate-dep-export.bat',
            'ensure-bmf-prueftool.bat',
            'fix-antd.bat',
            'dev-mail.bat',
            'run-with-log.bat',
            '_common.bat'
        )
        foreach ($name in $mustDocument) {
            if ($refText -notlike ('*{0}*' -f $name)) {
                Add-Err ("Not documented in SCRIPTS_REFERENCE.md: {0}" -f $name)
            }
            elseif (-not $Json) {
                Write-Host ('  [OK] documented: {0}' -f $name) -ForegroundColor Green
            }
        }

        foreach ($ps1 in $categoryPs1Files) {
            $name = $ps1.Name
            if ($name -in @('GameMode.ps1', 'WorkMode.ps1', 'dev-mail-config.ps1')) { continue }
            if ($refText -notlike ('*{0}*' -f $name)) {
                Add-Err ("scripts/**/{0} not documented in SCRIPTS_REFERENCE.md" -f $name)
            }
            elseif (-not $Json) {
                Write-Host ('  [OK] documented: {0}' -f $name) -ForegroundColor Green
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
    ok               = ($errors.Count -eq 0)
    rootBatCount     = $rootBatFiles.Count
    categoryBatCount = $categoryBatFiles.Count
    categoryPs1Count = $categoryPs1Files.Count
    errorCount       = $errors.Count
    warnCount        = $warnings.Count
    errors           = @($errors)
    warnings         = @($warnings)
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
