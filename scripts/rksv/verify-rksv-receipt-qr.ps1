param(
    [Parameter(Mandatory = $false)]
    [string]$QrCodeRepPath = "",

    [Parameter(Mandatory = $false)]
    [string]$CryptoMaterialPath = "",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = "./verification_output_qr",

    [Parameter(Mandatory = $false)]
    [switch]$UseFixtures,

    [Parameter(Mandatory = $false)]
    [switch]$DetailedOutput
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$fixtureDir = Join-Path $repoRoot "backend\Tests\fixtures\prueftool"

if ($UseFixtures) {
    $QrCodeRepPath = Join-Path $fixtureDir "qr-code-rep.json"
    $CryptoMaterialPath = Join-Path $fixtureDir "crypto-material.json"
}

if ([string]::IsNullOrWhiteSpace($QrCodeRepPath) -or [string]::IsNullOrWhiteSpace($CryptoMaterialPath)) {
    Write-Error "QrCodeRepPath and CryptoMaterialPath are required (or pass -UseFixtures)."
}

function Resolve-PrueftoolJavaExecutable {
    $candidates = @()
    if ($env:PRUEFTOOL_JAVA) { $candidates += $env:PRUEFTOOL_JAVA }
    if ($env:JAVA_HOME) { $candidates += (Join-Path $env:JAVA_HOME "bin\java.exe") }
    if (Get-Command java -ErrorAction SilentlyContinue) { $candidates += (Get-Command java).Source }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }
    return $null
}

$javaExe = Resolve-PrueftoolJavaExecutable
if (-not $javaExe) {
    Write-Error "Java not found. Install JDK 17+ or set PRUEFTOOL_JAVA."
    exit 1
}

$testsDir = Join-Path $repoRoot "backend\Tests"
$libDir = Join-Path $testsDir "lib"
$receiptsJar = Join-Path $testsDir "regkassen-verification-receipts-1.1.1.jar"

if (-not (Test-Path $receiptsJar)) {
    Write-Error "Receipts verification JAR not found: $receiptsJar"
    exit 1
}
if (-not (Test-Path $libDir)) {
    Write-Error "Prüftool lib directory not found: $libDir"
    exit 1
}

$qrFull = (Resolve-Path -LiteralPath $QrCodeRepPath).Path
$cryptoFull = (Resolve-Path -LiteralPath $CryptoMaterialPath).Path
$outputFull = (New-Item -ItemType Directory -Force -Path $OutputDir).FullName

$cpParts = @((Resolve-Path -LiteralPath $receiptsJar).Path)
$cpParts += Get-ChildItem -Path $libDir -Filter "*.jar" | ForEach-Object { $_.FullName }
$classpath = $cpParts -join ';'

$mainClass = "at.asitplus.regkassen.verification.cmdline.CheckSingleReceipt"

Write-Host "Running BMF CheckSingleReceipt..." -ForegroundColor Cyan
Write-Host "  QR rep:      $qrFull"
Write-Host "  Crypto file: $cryptoFull"
Write-Host "  Output dir:  $outputFull"

$javaArgs = @(
    "-cp", $classpath,
    $mainClass,
    "-v", "-f",
    "-i", $qrFull,
    "-c", $cryptoFull,
    "-o", $outputFull
)

if ($DetailedOutput) { $javaArgs += "-d" }

& $javaExe @javaArgs
$javaExitCode = $LASTEXITCODE

$verificationState = $null
Get-ChildItem -Path $outputFull -Filter "*.json" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        if ($null -ne $json.verificationState) {
            $verificationState = $json.verificationState
        }
    }
    catch { }
}

if ($verificationState -eq "PASS") {
    Write-Host "Receipt QR verification PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "Receipt QR verification FAILED (java exit: $javaExitCode, state: $verificationState)" -ForegroundColor Red
exit 1
