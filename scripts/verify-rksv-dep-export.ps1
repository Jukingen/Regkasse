param(
    [Parameter(Mandatory = $false)]
    [string]$DepExportPath = "",

    [Parameter(Mandatory = $false)]
    [string]$CryptoMaterialPath = "",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = "./verification_output",

    [Parameter(Mandatory = $false)]
    [switch]$UseFixtures,

    [Parameter(Mandatory = $false)]
    [switch]$DetailedOutput
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$fixtureDir = Join-Path $repoRoot "backend\Tests\fixtures\prueftool"

if ($UseFixtures) {
    $DepExportPath = Join-Path $fixtureDir "dep-export.json"
    $CryptoMaterialPath = Join-Path $fixtureDir "crypto-material.json"
}

if ([string]::IsNullOrWhiteSpace($DepExportPath) -or [string]::IsNullOrWhiteSpace($CryptoMaterialPath)) {
    Write-Error "DepExportPath and CryptoMaterialPath are required (or pass -UseFixtures for committed test fixtures)."
}

function Resolve-PrueftoolJavaExecutable {
    $candidates = @()

    if ($env:PRUEFTOOL_JAVA) {
        $candidates += $env:PRUEFTOOL_JAVA
    }
    if ($env:JAVA_HOME) {
        $candidates += (Join-Path $env:JAVA_HOME "bin\java.exe")
    }

    $programFiles = @(
        ${env:ProgramFiles},
        ${env:ProgramFiles(x86)}
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($root in $programFiles) {
        $microsoftJdk = Get-ChildItem -Path (Join-Path $root "Microsoft") -Filter "java.exe" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "jdk-1[7-9]|jdk-2[0-9]" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($microsoftJdk) {
            $candidates += $microsoftJdk.FullName
        }
    }

    if (Get-Command java -ErrorAction SilentlyContinue) {
        $candidates += (Get-Command java).Source
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if ([string]::IsNullOrWhiteSpace($candidate) -or -not (Test-Path -LiteralPath $candidate)) {
            continue
        }
        return $candidate
    }

    return $null
}

function Get-JavaMajorVersion {
    param([string]$JavaExecutable)

    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $versionOutput = & $JavaExecutable -version 2>&1 | ForEach-Object { "$_" }
    $ErrorActionPreference = $prevErrorAction

    $text = $versionOutput -join " "
    if ($text -match 'version "1\.(\d+)') {
        return [int]$Matches[1]
    }
    if ($text -match 'version "(\d+)') {
        return [int]$Matches[1]
    }
    return 0
}

$javaExe = Resolve-PrueftoolJavaExecutable
if (-not $javaExe) {
    Write-Error "Java not found. Install JDK 17+ (e.g. winget install Microsoft.OpenJDK.17) or set PRUEFTOOL_JAVA."
    exit 1
}

$javaMajor = Get-JavaMajorVersion -JavaExecutable $javaExe
if ($javaMajor -lt 17) {
    Write-Warning "Prüftool requires JDK 17+ for AES-256 turnover decrypt. Detected Java $javaMajor at: $javaExe"
    Write-Warning "CRYPTO_TURNOVER_COUNTER will FAIL on Java 8 even for official BMF fixtures. Install Microsoft.OpenJDK.17."
}

$prevErrorAction = $ErrorActionPreference
$ErrorActionPreference = "Continue"
Write-Host "Using Java: $javaExe" -ForegroundColor Cyan
& $javaExe -version 2>&1 | ForEach-Object { Write-Host $_ }
$ErrorActionPreference = $prevErrorAction

$ErrorActionPreference = "Stop"

$testsDir = Join-Path $repoRoot "backend\Tests"
$libDir = Join-Path $testsDir "lib"
$depJar = Join-Path $testsDir "regkassen-verification-depformat-1.1.1.jar"

if (-not (Test-Path $depJar)) {
    Write-Error "DEP verification JAR not found: $depJar"
    exit 1
}

if (-not (Test-Path $libDir)) {
    Write-Error "Prüftool lib directory not found: $libDir"
    exit 1
}

$depExportFull = (Resolve-Path -LiteralPath $DepExportPath).Path
$cryptoFull = (Resolve-Path -LiteralPath $CryptoMaterialPath).Path
$outputFull = (New-Item -ItemType Directory -Force -Path $OutputDir).FullName

$cpParts = @((Resolve-Path -LiteralPath $depJar).Path)
$cpParts += Get-ChildItem -Path $libDir -Filter "*.jar" | ForEach-Object { $_.FullName }
$classpath = $cpParts -join ';'

$mainClass = "at.asitplus.regkassen.verification.cmdline.CheckDEPExportFormat"

Write-Host "Running BMF DEP verification..." -ForegroundColor Cyan
Write-Host "  DEP file:    $depExportFull"
Write-Host "  Crypto file: $cryptoFull"
Write-Host "  Output dir:  $outputFull"

$javaArgs = @(
    "-cp", $classpath,
    $mainClass,
    "-v", "-f",
    "-i", $depExportFull,
    "-c", $cryptoFull,
    "-o", $outputFull
)

if ($DetailedOutput) {
    $javaArgs += "-d"
}

& $javaExe @javaArgs
$javaExitCode = $LASTEXITCODE

$summaryPath = Join-Path $outputFull "DEP-global.json"
$verificationState = $null
if (Test-Path $summaryPath) {
    try {
        $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
        $verificationState = $summary.verificationState
    }
    catch {
        Write-Warning "Could not parse DEP-global.json: $_"
    }
}

if ($verificationState -eq "PASS") {
    Write-Host "DEP verification PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "DEP verification FAILED (java exit: $javaExitCode, state: $verificationState)" -ForegroundColor Red

if (Test-Path $summaryPath) {
    Write-Host ""
    Write-Host "Summary (DEP-global.json):" -ForegroundColor Yellow
    Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 10
}

exit 1
