#!/usr/bin/env pwsh
# Creates sibling .bat wrappers for every .ps1 in the repo (excludes node_modules / .git / build dirs).
# Existing .bat files are left untouched. Use -Force to overwrite auto-generated wrappers only.
#
# Usage (from repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\create-bat-wrappers.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\create-bat-wrappers.ps1 -Force

param(
    [switch]$Force
)

$ErrorActionPreference = 'Continue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Split-Path -Parent $scriptDir)).Path

function Test-ExcludedPath {
    param([string]$FullName)
    $n = $FullName.Replace('/', '\')
    if ($n -match '\\node_modules\\') { return $true }
    if ($n -match '\\\.git\\') { return $true }
    if ($n -match '\\bin\\') { return $true }
    if ($n -match '\\obj\\') { return $true }
    if ($n -match '\\_test') { return $true }
    if ($n -match '\\publish\\') { return $true }
    if ($n -match '\\\.next\\') { return $true }
    if ($n -match '\\dist\\') { return $true }
    if ($n -match '\\coverage\\') { return $true }
    return $false
}

# Prefer known script roots to avoid deep/corrupt build trees under backend bin/
$searchRoots = @(
    (Join-Path $rootDir 'scripts'),
    (Join-Path $rootDir 'tools'),
    $rootDir
) | Where-Object { Test-Path $_ } | Select-Object -Unique

$ps1Files = New-Object System.Collections.Generic.List[System.IO.FileInfo]

foreach ($root in $searchRoots) {
    # Root: only top-level .ps1; scripts/tools: recurse
    if ($root -eq $rootDir) {
        Get-ChildItem -Path $root -Filter '*.ps1' -File -ErrorAction SilentlyContinue |
            ForEach-Object { [void]$ps1Files.Add($_) }
    } else {
        Get-ChildItem -Path $root -Recurse -Filter '*.ps1' -File -ErrorAction SilentlyContinue |
            Where-Object { -not (Test-ExcludedPath $_.FullName) } |
            ForEach-Object { [void]$ps1Files.Add($_) }
    }
}

# Dot-sourced libraries (not meant to be double-clicked)
$skipNames = @(
    'dev-mail-config.ps1'
)

$unique = $ps1Files | Sort-Object FullName -Unique
$created = 0
$skipped = 0

foreach ($ps1File in $unique) {
    if (Test-ExcludedPath $ps1File.FullName) { continue }
    if ($skipNames -contains $ps1File.Name) {
        Write-Host "Skipped (library): $($ps1File.Name)" -ForegroundColor DarkGray
        $skipped++
        continue
    }

    $batPath = [System.IO.Path]::ChangeExtension($ps1File.FullName, '.bat')
    $rel = $ps1File.FullName.Substring($rootDir.Length).TrimStart('\', '/')
    $ps1Name = $ps1File.Name

    # Never overwrite hand-crafted wrappers unless -Force
    if ((Test-Path -LiteralPath $batPath) -and -not $Force) {
        Write-Host "Skipped (already exists): $([System.IO.Path]::ChangeExtension($rel, '.bat'))" -ForegroundColor Yellow
        $skipped++
        continue
    }

    $batContent = @"
@echo off
setlocal EnableExtensions
chcp 65001 >nul

cd /d "%~dp0"
echo Running $ps1Name...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0$ps1Name" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% neq 0 (
    echo [FAILED] Exit code: %EXIT_CODE%
) else (
    echo [OK] Done.
)
echo.
pause
exit /b %EXIT_CODE%
"@

    Set-Content -Path $batPath -Value $batContent -Encoding ASCII
    Write-Host "Created: $([System.IO.Path]::ChangeExtension($rel, '.bat'))" -ForegroundColor Green
    $created++
}

Write-Host ""
Write-Host "All .bat wrappers processed. Created=$created Skipped=$skipped" -ForegroundColor Green
