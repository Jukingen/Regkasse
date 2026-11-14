# Start the Development API with Fake TSE, then run POS Playwright.
# Does not edit appsettings. A manually started Fiskaly API is not reused.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$PlaywrightArgs
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$ApiUrl = if ($env:E2E_API_URL) { $env:E2E_API_URL } else { "http://127.0.0.1:5184" }
if (-not $PlaywrightArgs -or $PlaywrightArgs.Count -eq 0) {
    $PlaywrightArgs = @("critical-pos-flows")
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Tse__TseMode = "Demo"
$env:Tse__Mode = "Fake"
$env:Tse__Provider = "fake"
# TseSoftFallbackPolicy requires both flags in Development. Without them
# Mode=Fake still refuses to sign when Fiskaly credentials exist.
$env:Tse__FallbackEnabled = "true"
$env:Tse__SoftTseEnabled = "true"

$alreadyListening = $false
try {
    Invoke-WebRequest -Uri "$ApiUrl/api/health" -UseBasicParsing -TimeoutSec 2 | Out-Null
    $alreadyListening = $true
} catch {
    $alreadyListening = $false
}
if ($alreadyListening) {
    Write-Error "Port 5184 is already serving an API. Stop that process first. Reusing it would keep Fiskaly for this run."
    exit 1
}

$api = Start-Process -FilePath "cmd.exe" -ArgumentList @("/c", "npm run dev:backend") -WorkingDirectory $Root -PassThru -NoNewWindow
try {
    Write-Host "Waiting for $ApiUrl/api/health ..."
    $healthy = $false
    for ($i = 0; $i -lt 90; $i++) {
        if ($api.HasExited) {
            throw "The API process exited before it became healthy."
        }
        try {
            Invoke-WebRequest -Uri "$ApiUrl/api/health" -Headers @{ "X-Tenant-Id" = "dev" } -UseBasicParsing -TimeoutSec 2 | Out-Null
            $healthy = $true
            break
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    if (-not $healthy) {
        throw "The API did not become healthy in time."
    }

    Push-Location (Join-Path $Root "frontend")
    try {
        & npx playwright test @PlaywrightArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } finally {
        Pop-Location
    }
} finally {
    if (-not $api.HasExited) {
        taskkill /PID $api.Id /T /F | Out-Null
    }
}
