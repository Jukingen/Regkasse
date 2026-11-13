#Requires -Version 5.1
<#
.SYNOPSIS
  Temporarily enable real PostgreSQL pg_dump for local Development testing.

.DESCRIPTION
  Sets user-secrets so Backup:ExecutionAdapterKind=PgDump and points at a local pg_dump.exe.
  Staging/Archive roots should already exist (or set them separately).

  Revert with:  .\scripts\revert-backup-fake.ps1

.NOTES
  Does not change ASPNETCORE_ENVIRONMENT. Restart the backend after running.
  See: backend/docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'backend\KasseAPI_Final.csproj'
if (-not (Test-Path $project)) {
    throw "Backend project not found: $project"
}

$defaultPgDump = 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe'
$pgDumpPath = $env:REGKASSE_PG_DUMP_PATH
if ([string]::IsNullOrWhiteSpace($pgDumpPath)) {
    $pgDumpPath = $defaultPgDump
}

if (-not (Test-Path -LiteralPath $pgDumpPath)) {
    Write-Host "pg_dump not found at: $pgDumpPath" -ForegroundColor Red
    Write-Host "Install PostgreSQL or set REGKASSE_PG_DUMP_PATH to your pg_dump.exe." -ForegroundColor Yellow
    exit 1
}

$version = & $pgDumpPath --version 2>&1
Write-Host "Enabling real PgDump for local testing..." -ForegroundColor Yellow
Write-Host "  pg_dump: $pgDumpPath ($version)"

Push-Location (Join-Path $repoRoot 'backend')
try {
    dotnet user-secrets set 'Backup:ExecutionAdapterKind' 'PgDump' --project $project | Out-Host
    dotnet user-secrets set 'Backup:PgDumpExecutablePath' $pgDumpPath --project $project | Out-Host

    # Ensure staging/archive are set if missing (safe defaults under C:\data).
    $secrets = dotnet user-secrets list --project $project 2>&1 | Out-String
    if ($secrets -notmatch 'Backup:ArtifactStagingRoot') {
        $staging = 'C:\data\regkasse-backup-staging'
        New-Item -ItemType Directory -Force -Path $staging | Out-Null
        dotnet user-secrets set 'Backup:ArtifactStagingRoot' $staging --project $project | Out-Host
        Write-Host "  Set Backup:ArtifactStagingRoot=$staging" -ForegroundColor DarkGray
    }
    if ($secrets -notmatch 'Backup:ExternalArchiveRoot') {
        $archive = 'C:\data\regkasse-backup-archive'
        New-Item -ItemType Directory -Force -Path $archive | Out-Null
        dotnet user-secrets set 'Backup:ExternalArchiveRoot' $archive --project $project | Out-Host
        Write-Host "  Set Backup:ExternalArchiveRoot=$archive" -ForegroundColor DarkGray
    }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host 'Done. Restart the backend, then trigger a backup from FA or:' -ForegroundColor Green
Write-Host '  POST /api/admin/backup/trigger' -ForegroundColor Green
Write-Host 'Revert with: .\scripts\revert-backup-fake.ps1' -ForegroundColor Yellow
Write-Host 'Docs: docs/BACKUP_SYSTEM.md (section Understanding "no real pg_dump")' -ForegroundColor DarkGray
