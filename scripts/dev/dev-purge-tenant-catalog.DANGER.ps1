# Development-only: hard-delete all products (and categories) for one tenant before a fresh FA demo import.
# Requires: backend running in Development, JWT with products.manage (or SuperAdmin).
#
# Examples:
#   .\scripts\dev-purge-tenant-catalog.DANGER.ps1 -TenantSlug dev -LoginIdentifier admin -Password '***'
#   .\scripts\dev-purge-tenant-catalog.DANGER.ps1 -TenantSlug dev -Token 'eyJ...'
#   .\scripts\dev-purge-tenant-catalog.DANGER.ps1 -TenantSlug dev -WithFiscalOverride

param(
    [string]$BaseUrl = 'http://localhost:5184',
    [string]$TenantSlug = 'dev',
    [string]$LoginIdentifier = 'admin',
    [string]$Password = '',
    [string]$Token = '',
    [switch]$KeepCategories,
    [switch]$WithFiscalOverride
)

$ErrorActionPreference = 'Stop'
$purgePath = '/api/admin/products/dev/purge-catalog'

function Get-HttpErrorBody {
    param($Exception)
    if ($null -eq $Exception) { return $null }
    try {
        $resp = $Exception.Response
        if ($null -eq $resp) { return $null }
        $stream = $resp.GetResponseStream()
        if ($null -eq $stream) { return $null }
        $reader = New-Object System.IO.StreamReader($stream)
        return $reader.ReadToEnd()
    } catch {
        return $null
    }
}

function Invoke-PurgeCatalogRequest {
    param(
        [string]$BearerToken,
        [string]$ConfirmPhrase
    )

    $headers = @{
        'Content-Type' = 'application/json'
        'Authorization' = "Bearer $BearerToken"
    }
    if ($TenantSlug) {
        $headers['X-Tenant-Id'] = $TenantSlug
    }

    $includeCategories = -not $KeepCategories.IsPresent
    $payload = @{
        tenantSlug = $TenantSlug
        includeCategories = $includeCategories
        confirmPhrase = $ConfirmPhrase
    } | ConvertTo-Json

    Write-Host "POST $BaseUrl$purgePath"
    Write-Host "confirmPhrase=$ConfirmPhrase includeCategories=$includeCategories tenantSlug=$TenantSlug"

  try {
        $response = Invoke-WebRequest -Method Post -Uri "$BaseUrl$purgePath" -Headers $headers -Body $payload -UseBasicParsing
        return $response.Content | ConvertFrom-Json
    } catch {
        $status = [int]$_.Exception.Response.StatusCode
        $body = Get-HttpErrorBody $_.Exception
        $ex = New-Object System.Exception("HTTP $status : $body", $_.Exception)
        $ex | Add-Member -NotePropertyName StatusCode -NotePropertyValue $status -Force
        $ex | Add-Member -NotePropertyName ResponseBody -NotePropertyValue $body -Force
        throw $ex
    }
}

if (-not $Token) {
    if (-not $Password) {
        Write-Error 'Provide -Token or -Password for login.'
        exit 1
    }

    $loginHeaders = @{ 'Content-Type' = 'application/json' }
    if ($TenantSlug) {
        $loginHeaders['X-Tenant-Id'] = $TenantSlug
    }

    $loginBody = @{
        loginIdentifier = $LoginIdentifier
        password = $Password
        clientApp = 'admin'
    } | ConvertTo-Json

    try {
        $loginResponse = Invoke-WebRequest -Method Post -Uri "$BaseUrl/api/Auth/login" -Headers $loginHeaders -Body $loginBody -UseBasicParsing
        $login = $loginResponse.Content | ConvertFrom-Json
    } catch {
        $body = Get-HttpErrorBody $_.Exception
        Write-Error "Login failed: HTTP $([int]$_.Exception.Response.StatusCode)"
        if ($body) { Write-Host $body }
        exit 1
    }

    $Token = $login.token
    if (-not $Token) { $Token = $login.data.token }
    if (-not $Token) {
        Write-Error 'Login failed: no token in response.'
        exit 1
    }
}

Write-Host "Purging tenant catalog for '$TenantSlug'..."

$phrases = @()
if ($WithFiscalOverride) {
    $phrases += 'DEV-PURGE-CATALOG-WITH-FISCAL'
} else {
    $phrases += 'DEV-PURGE-CATALOG'
    $phrases += 'DEV-PURGE-CATALOG-WITH-FISCAL'
}

$lastError = $null
foreach ($phrase in $phrases) {
    try {
        if ($phrase -eq 'DEV-PURGE-CATALOG-WITH-FISCAL' -and $phrases.Count -gt 1) {
            Write-Host ''
            Write-Host 'Fiscal payment records detected; retrying with development override phrase...'
        }

        $result = Invoke-PurgeCatalogRequest -BearerToken $Token -ConfirmPhrase $phrase
        $data = $result.data
        if (-not $data) { $data = $result }

        Write-Host "Done. Products deleted: $($data.productsDeleted), categories deleted: $($data.categoriesDeleted)"
        if ($data.hasFiscalPayments) {
            Write-Host 'Note: tenant had fiscal payment records (development override was used).'
        }
        exit 0
    } catch {
        $lastError = $_
        $body = $_.Exception.ResponseBody
        if (-not $body) { $body = $_.Exception.Message }

        $isFiscalBlock = $body -match 'fiscal payment records' -or $body -match 'DEV-PURGE-CATALOG-WITH-FISCAL'
        if ($isFiscalBlock -and $phrase -eq 'DEV-PURGE-CATALOG' -and $phrases.Count -gt 1) {
            continue
        }

        Write-Error $_.Exception.Message
        if ($body) {
            Write-Host 'API response:'
            Write-Host $body
        }
        if ($_.Exception.Message -match '404') {
            Write-Host ''
            Write-Host 'Hint: restart backend (dotnet run) so POST /api/admin/products/dev/purge-catalog is available.'
            Write-Host '      ASPNETCORE_ENVIRONMENT must be Development.'
        }
        exit 1
    }
}

if ($lastError) { throw $lastError }
