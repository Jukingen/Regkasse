#Requires -Version 5.1
<#
.SYNOPSIS
  Revert local Development backup secrets to Fake adapter mode.

.DESCRIPTION
  Removes Backup:ExecutionAdapterKind and Backup:PgDumpExecutablePath from user-secrets
  so appsettings.Development.json (Fake) applies again.

  Staging/Archive path secrets are left intact (harmless under Fake).
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'backend\KasseAPI_Final.csproj'
if (-not (Test-Path $project)) {
    throw "Backend project not found: $project"
}

Write-Host 'Reverting to Fake backup adapter...' -ForegroundColor Yellow

Push-Location (Join-Path $repoRoot 'backend')
try {
    # remove fails non-zero if key missing — treat as OK
    foreach ($key in @('Backup:ExecutionAdapterKind', 'Backup:PgDumpExecutablePath')) {
        & dotnet user-secrets remove $key --project $project 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Removed $key" -ForegroundColor DarkGray
        }
        else {
            Write-Host "  $key was not set (ok)" -ForegroundColor DarkGray
        }
    }
}
finally {
    Pop-Location
}

Write-Host 'Done. Restart the backend. Backup is Fake again (no real pg_dump).' -ForegroundColor Green
