# =============================================================================
# Wrapper for scripts/sql/fiscal_go_live_validation.sql — CI/release gate (Windows).
# - Runs SQL via psql; FAIL -> exit 1 (pipeline fail); WARN -> exit 0, report saved as artifact.
# - Does not modify validation semantics; only enforces execution discipline.
# =============================================================================
$ErrorActionPreference = "Stop"

$RepoRoot = if ($env:REPO_ROOT) { $env:REPO_ROOT } else { (Get-Item $PSScriptRoot).Parent.FullName }
$SqlFile = Join-Path $RepoRoot "scripts\sql\fiscal_go_live_validation.sql"
$ReportPath = if ($env:FISCAL_VALIDATION_REPORT_PATH) { $env:FISCAL_VALIDATION_REPORT_PATH } else { Join-Path $RepoRoot "fiscal_validation_report.txt" }

if (-not $env:DATABASE_URL) {
    Write-Error "DATABASE_URL is not set. Set it to a PostgreSQL connection URL (e.g. postgresql://user:pass@host:5432/dbname)."
    exit 1
}

if (-not (Test-Path -LiteralPath $SqlFile)) {
    Write-Error "SQL file not found: $SqlFile"
    exit 1
}

Write-Host "Running fiscal go-live validation (read-only)..."
Write-Host "  SQL: $SqlFile"
Write-Host "  Report: $ReportPath"

$output = & psql $env:DATABASE_URL -v ON_ERROR_STOP=1 -f $SqlFile 2>&1
$output | Set-Content -Path $ReportPath -Encoding utf8
$output | Write-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "Fiscal validation: psql exited with error. Pipeline failed."
    exit 1
}

$content = Get-Content -Path $ReportPath -Raw
if ($content -match "RESULT:\s*FAIL") {
    Write-Host ""
    Write-Host "Fiscal validation: FAIL — do not go-live until resolved. Pipeline failed."
    exit 1
}

if ($content -match "RESULT:\s*WARN") {
    Write-Host ""
    Write-Host "Fiscal validation: WARN — review before go-live. Full report saved to: $ReportPath"
    Write-Host "  (In CI, upload this file as an artifact.)"
    exit 0
}

Write-Host ""
Write-Host "Fiscal validation: OK — no FAIL/WARN."
exit 0
