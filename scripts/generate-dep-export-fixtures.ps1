param(
    [Parameter(Mandatory = $false)]
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "backend\Tests\fixtures\prueftool"
}

Write-Host "Generating BMF Prüftool fixtures..." -ForegroundColor Cyan
Write-Host "  Output: $OutputDir"

Push-Location (Join-Path $repoRoot "backend")
try {
    dotnet test --filter "RksvDepPrueftoolFixtureTests" --no-restore 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture generation tests failed (exit code $LASTEXITCODE)."
    }
}
finally {
    Pop-Location
}

$dep = Join-Path $OutputDir "dep-export.json"
$crypto = Join-Path $OutputDir "crypto-material.json"

if (-not (Test-Path $dep) -or -not (Test-Path $crypto)) {
    Write-Error "Expected fixture files were not created: $dep , $crypto"
}

Write-Host "Fixtures ready:" -ForegroundColor Green
Write-Host "  $dep"
Write-Host "  $crypto"
Write-Host ""
Write-Host "Verify (requires JDK 17+):" -ForegroundColor Yellow
Write-Host "  .\scripts\verify-rksv-dep-export.ps1 -UseFixtures"
