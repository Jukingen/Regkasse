# run-verify-dep-export-complete.ps1
# Automated DEP export validation (build, unit tests, Prueftool fixtures, FA build).
$ErrorActionPreference = "Continue"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$passCount = 0
$failCount = 0
$failedSteps = @()

function Write-StepHeader {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Yellow
}

function Write-StepOutput {
    param([object[]]$Lines)
    foreach ($line in $Lines) {
        if ($null -eq $line) { continue }
        Write-Host $line
    }
}

function Invoke-ValidationStep {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-StepHeader $Name
    $output = & $Action 2>&1
    $exitCode = $LASTEXITCODE
    Write-StepOutput $output

    if ($exitCode -eq 0) {
        Write-Host "  PASS" -ForegroundColor Green
        $script:passCount++
    }
    else {
        Write-Host "  FAIL (exit code $exitCode)" -ForegroundColor Red
        $script:failCount++
        $script:failedSteps += $Name
    }

    Write-Host ""
    return $exitCode
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   DEP Export Tamamlama Dogrulama" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Repo: $repoRoot"
Write-Host ""

Invoke-ValidationStep "[1/4] Backend build" {
    Push-Location (Join-Path $repoRoot "backend")
    try {
        dotnet build --no-restore
    }
    finally {
        Pop-Location
    }
} | Out-Null

Invoke-ValidationStep "[2/4] Unit tests (RksvDepExportServiceTests)" {
    Push-Location (Join-Path $repoRoot "backend")
    try {
        dotnet test --filter "RksvDepExportServiceTests" --no-build
    }
    finally {
        Pop-Location
    }
} | Out-Null

Invoke-ValidationStep "[3/4] Prueftool fixture verification" {
    Push-Location $repoRoot
    try {
        & (Join-Path $PSScriptRoot "verify-rksv-dep-export.ps1") -UseFixtures
    }
    finally {
        Pop-Location
    }
} | Out-Null

Invoke-ValidationStep "[4/4] Frontend-Admin build" {
    Push-Location (Join-Path $repoRoot "frontend-admin")
    try {
        npm run build
    }
    finally {
        Pop-Location
    }
} | Out-Null

Write-Host "[5/5] Manual checks (not automated)" -ForegroundColor Yellow
Write-Host "  - Backend API: http://localhost:5184/swagger (GET /api/admin/rksv/dep-export)"
Write-Host "  - Admin UI:    http://localhost:3000/admin/rksv/dep-export"
Write-Host "  - CI:          GitHub Actions workflow status"
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   SONUC" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PASS: $passCount / 4" -ForegroundColor Green
Write-Host "FAIL: $failCount / 4" -ForegroundColor Red

if ($failedSteps.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed steps:" -ForegroundColor Red
    foreach ($step in $failedSteps) {
        Write-Host "  - $step" -ForegroundColor Red
    }
}

Write-Host ""

if ($failCount -eq 0) {
    Write-Host "TUM OTOMATIK TESTLER GECTI. DEP Export hazir." -ForegroundColor Green
    exit 0
}

Write-Host "$failCount otomatik test basarisiz. Yukaridaki hata ciktisini inceleyin." -ForegroundColor Yellow
exit 1
