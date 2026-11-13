param(
    [string]$BaseUrl = "http://localhost:5184",
    [string]$AdminLoginIdentifier = "admin@admin.com",
    [string]$AdminPassword = "Admin123!",
    [string]$ClientApp = "admin"
)

$ErrorActionPreference = "Stop"

function Invoke-ApiJson {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Url,
        [hashtable]$Headers,
        $Body
    )

    $invokeArgs = @{
        Method      = $Method
        Uri         = $Url
        TimeoutSec  = 20
        ErrorAction = "Stop"
    }

    if ($Headers) { $invokeArgs.Headers = $Headers }
    if ($null -ne $Body) {
        $invokeArgs.ContentType = "application/json"
        $invokeArgs.Body = ($Body | ConvertTo-Json -Depth 8)
    }

    try {
        $bodyResp = Invoke-RestMethod @invokeArgs
        return @{ StatusCode = 200; Body = $bodyResp; Raw = ($bodyResp | ConvertTo-Json -Depth 8) }
    } catch {
        $statusCode = -1
        $raw = ""
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode.value__
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $raw = $reader.ReadToEnd()
            } catch {
                $raw = $_.Exception.Message
            }
        } else {
            $raw = $_.Exception.Message
        }
        $parsed = $raw
        if ($raw) {
            try { $parsed = $raw | ConvertFrom-Json } catch { $parsed = $raw }
        }
        return @{ StatusCode = $statusCode; Body = $parsed; Raw = $raw }
    }
}

function Add-Result {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details
    )
    $script:results += [pscustomobject]@{
        Check   = $Name
        Passed  = $Passed
        Details = $Details
    }
}

$results = @()

Write-Host "== Tenant Isolation Smoke =="
Write-Host "BaseUrl: $BaseUrl"

# 1) Super admin login
$loginPayload = @{
    loginIdentifier = $AdminLoginIdentifier
    password        = $AdminPassword
    clientApp       = $ClientApp
}
$login = Invoke-ApiJson -Method "POST" -Url "$BaseUrl/api/Auth/login" -Body $loginPayload
if ($login.StatusCode -ne 200 -or -not $login.Body.token) {
    Add-Result -Name "SuperAdmin login" -Passed $false -Details "Expected 200/token, got $($login.StatusCode)"
    $results | Format-Table -AutoSize
    exit 1
}
Add-Result -Name "SuperAdmin login" -Passed $true -Details "OK"
$superToken = [string]$login.Body.token
$superHeaders = @{ Authorization = "Bearer $superToken" }

# 2) Get active business tenants (non-admin/default)
$tenantsResp = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/tenants" -Headers $superHeaders
if ($tenantsResp.StatusCode -ne 200) {
    Add-Result -Name "List tenants" -Passed $false -Details "Expected 200, got $($tenantsResp.StatusCode)"
    $results | Format-Table -AutoSize
    exit 1
}

$businessTenants = @($tenantsResp.Body | Where-Object {
    $_.isActive -eq $true -and $_.slug -ne "admin" -and $_.slug -ne "default"
})

if ($businessTenants.Count -lt 2) {
    $suffix = (Get-Date).ToUniversalTime().ToString("MMddHHmmss")
    $slug = "smoke-$suffix"
    $createTenantBody = @{
        name  = "Smoke Tenant $suffix"
        slug  = $slug
        email = "smoke+$suffix@regkasse.at"
    }
    $createTenantResp = Invoke-ApiJson -Method "POST" -Url "$BaseUrl/api/admin/tenants" -Headers $superHeaders -Body $createTenantBody
    if ($createTenantResp.StatusCode -eq 201 -or $createTenantResp.StatusCode -eq 200) {
        $tenantsResp = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/tenants" -Headers $superHeaders
        $businessTenants = @($tenantsResp.Body | Where-Object {
            $_.isActive -eq $true -and $_.slug -ne "admin" -and $_.slug -ne "default"
        })
        Add-Result -Name "Auto-create tenant" -Passed $true -Details "Created $slug for smoke"
    } else {
        Add-Result -Name "Auto-create tenant" -Passed $false -Details "Create tenant failed with $($createTenantResp.StatusCode)"
    }
}

if ($businessTenants.Count -lt 2) {
    Add-Result -Name "Tenant availability" -Passed $false -Details "Need at least 2 active business tenants, found $($businessTenants.Count) after auto-create"
    $results | Format-Table -AutoSize
    exit 1
}

$tenantA = $businessTenants[0]
$tenantB = $businessTenants[1]
Add-Result -Name "Tenant selection" -Passed $true -Details "A=$($tenantA.slug) B=$($tenantB.slug)"

function Get-ImpersonationToken([string]$tenantId) {
    $imp = Invoke-ApiJson -Method "POST" -Url "$BaseUrl/api/admin/tenants/$tenantId/impersonate" -Headers $superHeaders
    if ($imp.StatusCode -ne 200 -or -not $imp.Body.token) { return $null }
    return [string]$imp.Body.token
}

$tokenA = Get-ImpersonationToken -tenantId $tenantA.id
$tokenB = Get-ImpersonationToken -tenantId $tenantB.id
if (-not $tokenA -or -not $tokenB) {
    Add-Result -Name "Impersonation tokens" -Passed $false -Details "Failed to get token(s) for selected tenants"
    $results | Format-Table -AutoSize
    exit 1
}
Add-Result -Name "Impersonation tokens" -Passed $true -Details "Issued for both tenants"

$headersA = @{ Authorization = "Bearer $tokenA" }
$headersB = @{ Authorization = "Bearer $tokenB" }

# 3) /api/admin/users tenant-isolated list (type=tenant)
$usersAResp = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/users?type=tenant" -Headers $headersA
$usersBResp = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/users?type=tenant" -Headers $headersB
$usersListsOk = ($usersAResp.StatusCode -eq 200 -and $usersBResp.StatusCode -eq 200)
Add-Result -Name "Users list status" -Passed $usersListsOk -Details "A=$($usersAResp.StatusCode), B=$($usersBResp.StatusCode)"

$usersA = @()
$usersB = @()
if ($usersAResp.StatusCode -eq 200) { $usersA = @($usersAResp.Body) }
if ($usersBResp.StatusCode -eq 200) { $usersB = @($usersBResp.Body) }

$tenantAIdStr = ([string]$tenantA.id).ToLowerInvariant()
$tenantBIdStr = ([string]$tenantB.id).ToLowerInvariant()
$usersAOnlyTenant = ($usersA | Where-Object { ([string]$_.tenantId).ToLowerInvariant() -ne $tenantAIdStr }).Count -eq 0
$usersBOnlyTenant = ($usersB | Where-Object { ([string]$_.tenantId).ToLowerInvariant() -ne $tenantBIdStr }).Count -eq 0
Add-Result -Name "Users tenant context" -Passed ($usersAOnlyTenant -and $usersBOnlyTenant) -Details "Rows scoped to impersonated tenant IDs"

$userIdsA = @($usersA | ForEach-Object { $_.userId } | Where-Object { $_ })
$userIdsB = @($usersB | ForEach-Object { $_.userId } | Where-Object { $_ })
$userOverlap = @($userIdsA | Where-Object { $userIdsB -contains $_ } | Select-Object -Unique)
Add-Result -Name "Users overlap check" -Passed ($userOverlap.Count -eq 0) -Details "OverlapCount=$($userOverlap.Count)"

# 4) Cross-tenant user detail must return 404
if ($userIdsA.Count -gt 0) {
    $crossUser = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/users/$($userIdsA[0])" -Headers $headersB
    Add-Result -Name "Cross-tenant user detail -> 404" -Passed ($crossUser.StatusCode -eq 404) -Details "Got $($crossUser.StatusCode)"
} else {
    Add-Result -Name "Cross-tenant user detail -> 404" -Passed $false -Details "No tenant-A user found for cross-check"
}

# 5) /api/admin/payments list with tenant impersonation
$paymentsAResp = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/payments?pageSize=50" -Headers $headersA
$paymentsBResp = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/payments?pageSize=50" -Headers $headersB
$paymentsStatusOk = ($paymentsAResp.StatusCode -eq 200 -and $paymentsBResp.StatusCode -eq 200)
Add-Result -Name "Payments list status" -Passed $paymentsStatusOk -Details "A=$($paymentsAResp.StatusCode), B=$($paymentsBResp.StatusCode)"

$paymentIdsA = @()
$paymentIdsB = @()
if ($paymentsAResp.StatusCode -eq 200 -and $paymentsAResp.Body.items) { $paymentIdsA = @($paymentsAResp.Body.items | ForEach-Object { $_.id }) }
if ($paymentsBResp.StatusCode -eq 200 -and $paymentsBResp.Body.items) { $paymentIdsB = @($paymentsBResp.Body.items | ForEach-Object { $_.id }) }
$paymentOverlap = @($paymentIdsA | Where-Object { $paymentIdsB -contains $_ } | Select-Object -Unique)
Add-Result -Name "Payments overlap check" -Passed ($paymentOverlap.Count -eq 0) -Details "OverlapCount=$($paymentOverlap.Count)"

# 6) Cross-tenant payment detail must return 404 (403 forbidden is not acceptable for this rule)
if ($paymentIdsA.Count -gt 0) {
    $crossPayment = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/payments/$($paymentIdsA[0])" -Headers $headersB
    $ok = ($crossPayment.StatusCode -eq 404)
    Add-Result -Name "Cross-tenant payment detail -> 404" -Passed $ok -Details "Got $($crossPayment.StatusCode)"
} else {
    # fallback: random GUID should still not return 403 (obscurity contract)
    $randomId = [guid]::NewGuid().ToString()
    $notFoundA = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/payments/$randomId" -Headers $headersA
    $notFoundB = Invoke-ApiJson -Method "GET" -Url "$BaseUrl/api/admin/payments/$randomId" -Headers $headersB
    $ok = ($notFoundA.StatusCode -eq 404 -and $notFoundB.StatusCode -eq 404)
    Add-Result -Name "Payment detail fallback 404" -Passed $ok -Details "A=$($notFoundA.StatusCode), B=$($notFoundB.StatusCode)"
}

Write-Host ""
$results | Format-Table -AutoSize

$failed = @($results | Where-Object { -not $_.Passed })
if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "FAILED CHECKS: $($failed.Count)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "ALL TENANT ISOLATION SMOKE CHECKS PASSED" -ForegroundColor Green
exit 0

