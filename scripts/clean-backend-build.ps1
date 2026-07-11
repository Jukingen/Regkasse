# Removes corrupted backend build outputs (nested verify paths, long-path copy errors).
# Run from repo root: .\scripts\clean-backend-build.ps1

$ErrorActionPreference = 'Stop'
$backend = Join-Path $PSScriptRoot '..\backend' | Resolve-Path

Get-Process -Name 'KasseAPI_Final' -ErrorAction SilentlyContinue | Stop-Process -Force

$dirs = @('obj', 'bin', '_test_build_out', '_testout', '_ef_build', '_build_out')
foreach ($name in $dirs) {
    $path = Join-Path $backend $name
    if (Test-Path $path) {
        cmd /c "rmdir /s /q \\?\$path" | Out-Null
        Write-Host "Removed $path"
    }
}

Write-Host 'Done. Rebuild with: dotnet build backend\KasseAPI_Final.csproj'
