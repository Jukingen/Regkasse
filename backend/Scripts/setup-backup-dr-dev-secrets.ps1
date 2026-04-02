#Requires -Version 5.0
# Türkçe: Yerel geliştirme için Backup kullanıcı sırlarını yazar; repo varsayılanı Fake kalır, admin UI üzerinden RealPgDump seçilir.
#
# Ne yapar:
# - Örnek staging / harici arşiv dizinlerini oluşturur (yoksa).
# - User Secrets içine Backup:ArtifactStagingRoot ve Backup:ExternalArchiveRoot yazar.
# - Backup:ExecutionAdapterKind değiştirmez (Fake kalır).
#
# Kullanım (backend klasöründen):
#   .\scripts\setup-backup-dr-dev-secrets.ps1
# Özel yollar:
#   .\scripts\setup-backup-dr-dev-secrets.ps1 -StagingRoot "D:\bk\staging" -ArchiveRoot "D:\bk\archive"

param(
    [string]$StagingRoot = 'C:\data\regkasse-backup-staging',
    [string]$ArchiveRoot = 'C:\data\regkasse-backup-archive'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $projectRoot 'KasseAPI_Final.csproj'

if (-not (Test-Path -LiteralPath $csproj)) {
    Write-Error "KasseAPI_Final.csproj not found at $csproj"
    exit 1
}

New-Item -ItemType Directory -Force -Path $StagingRoot | Out-Null
New-Item -ItemType Directory -Force -Path $ArchiveRoot | Out-Null

Push-Location $projectRoot
try {
    dotnet user-secrets set 'Backup:ArtifactStagingRoot' $StagingRoot --project $csproj
    dotnet user-secrets set 'Backup:ExternalArchiveRoot' $ArchiveRoot --project $csproj
}
finally {
    Pop-Location
}

Write-Host 'User secrets updated. Restart the API, then GET /api/admin/backup/execution-mode: RealPgDump row should be selectable when hypothetical health is not Unhealthy.'
