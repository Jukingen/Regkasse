<#
.SYNOPSIS
  Downloads the official BMF Prüftool V1.1.1 ZIP into backend/Tests/ (gitignored JARs).

.DESCRIPTION
  Source: https://github.com/BMF-RKSV-Technik/at-registrierkassen-mustercode/releases/tag/V1.1.1
  Places:
    - backend/Tests/regkassen-verification-depformat-1.1.1.jar
    - backend/Tests/regkassen-verification-receipts-1.1.1.jar (optional helper)
    - backend/Tests/lib/*.jar

  Idempotent: skips download when the DEP JAR and lib/ already exist (unless -Force).
#>
param(
    [Parameter(Mandatory = $false)]
    [switch]$Force,

    [Parameter(Mandatory = $false)]
    [string]$ExpectedSha256 = "2C1D65B7B0024262E36EBBFBA296C47773C68C7000A6F33C36F80B1215E414B6"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$testsDir = [System.IO.Path]::Combine($repoRoot, "backend", "Tests")
$depJar = [System.IO.Path]::Combine($testsDir, "regkassen-verification-depformat-1.1.1.jar")
$receiptsJar = [System.IO.Path]::Combine($testsDir, "regkassen-verification-receipts-1.1.1.jar")
$libDir = [System.IO.Path]::Combine($testsDir, "lib")

$releaseUrl = "https://github.com/BMF-RKSV-Technik/at-registrierkassen-mustercode/releases/download/V1.1.1/regkassen-verification-1.1.1.zip"

function Test-PrueftoolInstalled {
    if (-not (Test-Path -LiteralPath $depJar)) { return $false }
    if (-not (Test-Path -LiteralPath $libDir)) { return $false }
    $jarCount = @(Get-ChildItem -LiteralPath $libDir -Filter "*.jar" -ErrorAction SilentlyContinue).Count
    return $jarCount -ge 10
}

if (-not $Force -and (Test-PrueftoolInstalled)) {
    Write-Host "BMF Prüftool already present under $testsDir (use -Force to reinstall)." -ForegroundColor Green
    exit 0
}

New-Item -ItemType Directory -Force -Path $testsDir | Out-Null
$workDir = Join-Path ([System.IO.Path]::GetTempPath()) ("regkasse-bmf-prueftool-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

try {
    $zipPath = Join-Path $workDir "regkassen-verification-1.1.1.zip"
    Write-Host "Downloading BMF Prüftool V1.1.1..." -ForegroundColor Cyan
    Write-Host "  $releaseUrl"

    Invoke-WebRequest -Uri $releaseUrl -OutFile $zipPath -UseBasicParsing

    $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $expected = $ExpectedSha256.ToUpperInvariant()
    if ($actualHash -ne $expected) {
        throw "SHA256 mismatch for Prüftool ZIP. Expected $expected, got $actualHash."
    }
    Write-Host "SHA256 OK: $actualHash" -ForegroundColor Green

    $extractRoot = Join-Path $workDir "extract"
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractRoot -Force

    $bundleRoot = Get-ChildItem -LiteralPath $extractRoot -Directory |
        Where-Object { $_.Name -like "regkassen-verification-*" } |
        Select-Object -First 1
    if (-not $bundleRoot) {
        throw "Could not find regkassen-verification-* folder inside the ZIP."
    }

    $srcDep = Join-Path $bundleRoot.FullName "regkassen-verification-depformat-1.1.1.jar"
    $srcReceipts = Join-Path $bundleRoot.FullName "regkassen-verification-receipts-1.1.1.jar"
    $srcLib = Join-Path $bundleRoot.FullName "lib"

    if (-not (Test-Path -LiteralPath $srcDep)) {
        throw "Missing $srcDep in release ZIP."
    }
    if (-not (Test-Path -LiteralPath $srcLib)) {
        throw "Missing lib/ in release ZIP."
    }

    if (Test-Path -LiteralPath $libDir) {
        Remove-Item -LiteralPath $libDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $libDir | Out-Null

    Copy-Item -LiteralPath $srcDep -Destination $depJar -Force
    if (Test-Path -LiteralPath $srcReceipts) {
        Copy-Item -LiteralPath $srcReceipts -Destination $receiptsJar -Force
    }
    Copy-Item -Path (Join-Path $srcLib "*") -Destination $libDir -Force

    if (-not (Test-PrueftoolInstalled)) {
        throw "Install completed but Prüftool still looks incomplete under $testsDir."
    }

    Write-Host "Installed:" -ForegroundColor Green
    Write-Host "  $depJar"
    if (Test-Path -LiteralPath $receiptsJar) { Write-Host "  $receiptsJar" }
    Write-Host "  $libDir ($((Get-ChildItem -LiteralPath $libDir -Filter '*.jar').Count) jars)"
}
finally {
    try { Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
}
