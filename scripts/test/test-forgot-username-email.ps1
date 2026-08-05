param(
    [Parameter(Mandatory = $true)]
    [string]$Email,

    [string]$BaseUrl = "",
    [string]$DevMailDir = "",
    [switch]$OpenDevMail,
    [int]$WaitSeconds = 2
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "dev-mail-config.ps1")
$config = Get-DevMailConfig

$Email = $Email.Trim()

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = $config.BaseUrl
}

if ([string]::IsNullOrWhiteSpace($DevMailDir)) {
    $DevMailDir = Join-Path $PSScriptRoot "..\backend\App_Data\dev-mail"
}

function Write-ResultBox {
    param(
        [string]$Title,
        [string]$EmailAddress,
        [string]$Username,
        [string]$ExtraLine = ""
    )

    Write-Host ""
    Write-Host "========================================"
    Write-Host "  $Title"
    Write-Host "========================================"
    Write-Host ""
    Write-Host "  E-posta       : $EmailAddress"

    if ($Username) {
        Write-Host "  Kullanici adi : $Username"
        Write-Host ""
        Write-Host "  Bu adla (veya ayni e-posta ile) giris yapabilirsiniz."
    } else {
        Write-Host "  Kullanici adi : BULUNAMADI"
        Write-Host ""
        Write-Host "  Bu e-posta ile aktif kullanici yok veya mail yakalanmadi."
        Write-Host "  Admin ^> Kullanicilar listesinden e-postayi kontrol edin."
    }

    if ($ExtraLine) {
        Write-Host ""
        Write-Host "  $ExtraLine"
    }

    Write-Host ""
    Write-Host "========================================"
}

function Get-DevMailCount {
    if (-not (Test-Path $DevMailDir)) { return 0 }
    return (Get-ChildItem -Path $DevMailDir -Filter "*.txt" -ErrorAction SilentlyContinue).Count
}

function Get-LatestDevMailFile {
    if (-not (Test-Path $DevMailDir)) { return $null }
    return Get-ChildItem -Path $DevMailDir -Filter "*.txt" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Get-UsernameFromMailContent {
    param([string]$Content)
    if ($Content -match 'current username=([^,\r\n]+)') {
        return $Matches[1].Trim()
    }
    if ($Content -match '(?s)Ihr Benutzername lautet:\s*\r?\n\s*•\s*(.+?)\r?\n') {
        return $Matches[1].Trim()
    }
    foreach ($line in ($Content -split "`r?`n")) {
        if ($line -match '^\s*•\s*(.+)\s*$') {
            return $Matches[1].Trim()
        }
    }
    return $null
}

function Get-DevDebugFromMailContent {
    param([string]$Content)
    if ($Content -match 'Matched account:\s*(.+)') {
        return $Matches[1].Trim()
    }
    return $null
}

Write-Host ""
Write-Host "Sorgulanan e-posta: $Email"
Write-Host "Backend           : $BaseUrl"
Write-Host ""

Write-Host "Backend kontrol ediliyor..."
try {
    $null = Invoke-RestMethod -Uri "$BaseUrl/api/health" -TimeoutSec 5
} catch {
    Write-ResultBox -Title "SONUC" -EmailAddress $Email -Username ""
    Write-Host "[HATA] Backend calismiyor. Once: cd backend; dotnet run"
    exit 1
}

$beforeCount = Get-DevMailCount

$body = @{
    email     = $Email
    clientApp = "admin"
}

Write-Host "Sistem sorgulaniyor (forgot-username)..."
try {
    $null = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/Auth/forgot-username" `
        -ContentType "application/json" `
        -Body ($body | ConvertTo-Json) `
        -TimeoutSec 20
} catch {
    Write-ResultBox -Title "SONUC" -EmailAddress $Email -Username ""
    Write-Host "[HATA] Istek basarisiz: $($_.Exception.Message)"
    exit 1
}

Start-Sleep -Seconds $WaitSeconds

$afterCount = Get-DevMailCount

if ($afterCount -le $beforeCount) {
    Write-ResultBox -Title "SONUC" -EmailAddress $Email -Username "" `
        -ExtraLine "FA yine de ""gonderildi"" der; bu guvenlik icin normaldir."
    exit 1
}

$latest = Get-LatestDevMailFile
$captured = Get-Content -Path $latest.FullName -Raw
$username = Get-UsernameFromMailContent -Content $captured
$devDebug = Get-DevDebugFromMailContent -Content $captured
$isOldTemplate = $captured -match 'Folgende Benutzernamen'

Write-Host ""
Write-Host "----------------------------------------"
Write-Host "Kaydedilen mail (dev-mail)"
Write-Host "Dosya: $($latest.FullName)"
Write-Host "----------------------------------------"
Get-Content -Path $latest.FullName
Write-Host "----------------------------------------"

$extra = "Mail dosyasi: backend\App_Data\dev-mail\"
if ($devDebug) {
    $extra = "$extra`n  Hesap: $devDebug"
}

Write-ResultBox -Title "SONUC" -EmailAddress $Email -Username $username -ExtraLine $extra

if ($isOldTemplate) {
    Write-Host "[UYARI] Eski backend kodu. Ctrl+C ile durdurup: cd backend; dotnet run"
}

if ($OpenDevMail -and (Test-Path $DevMailDir)) {
    Start-Process explorer.exe $DevMailDir
}

exit 0
