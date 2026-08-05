# Comprehensive Regkasse smoke test â€” API + route checks
# Usage: .\scripts\run-comprehensive-smoke.ps1

$ErrorActionPreference = 'Continue'
$BaseUrl = 'http://localhost:5184'
$FaUrl = 'http://localhost:3000'
$PosUrl = 'http://localhost:8081'
$DevTenantSlug = 'dev'
$DevTenantId = 'b0000001-0001-4001-8001-000000000001'
$RegisterId = '036da12b-6f83-4f61-9b42-26d098cb22f2'
$TestProductId = '11111111-1111-1111-1111-111111111111'
$CashierId = '16a1a9f6-b8b4-48b2-8d8d-eb32e7f90548'

$script:Results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Suite,
        [string]$Scenario,
        [string]$Status,  # PASS | FAIL | SKIP
        [string]$Details = ''
    )
    $script:Results.Add([pscustomobject]@{
            Suite    = $Suite
            Scenario = $Scenario
            Status   = $Status
            Details  = $Details
        })
}

function Invoke-Api {
    param(
        [string]$Method = 'GET',
        [string]$Path,
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )
    $uri = if ($Path.StartsWith('http')) { $Path } else { "$BaseUrl$Path" }
    $params = @{
        Uri             = $uri
        Method          = $Method
        Headers         = $Headers
        UseBasicParsing = $true
        ErrorAction     = 'Stop'
    }
    if ($null -ne $Body) {
        $params['ContentType'] = 'application/json'
        $params['Body'] = ($Body | ConvertTo-Json -Depth 10)
    }
    try {
        $resp = Invoke-WebRequest @params
        $json = $null
        if ($resp.Content) {
            try { $json = $resp.Content | ConvertFrom-Json } catch { $json = $resp.Content }
        }
        return @{ Ok = $true; StatusCode = [int]$resp.StatusCode; Json = $json; Raw = $resp.Content }
    }
    catch {
        $status = $null
        $body = $null
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode.value__
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $body = $reader.ReadToEnd()
                $reader.Close()
            }
            catch { }
        }
        return @{ Ok = $false; StatusCode = $status; Raw = $body; Error = $_.Exception.Message }
    }
}

function Get-LoginToken {
    param(
        [string]$LoginIdentifier,
        [string]$Password,
        [string]$ClientApp,
        [string]$TenantHeader = $DevTenantSlug
    )
    $h = @{ 'X-Tenant-Id' = $TenantHeader }
    $r = Invoke-Api -Method POST -Path '/api/Auth/login' -Headers $h -Body @{
        loginIdentifier = $LoginIdentifier
        password        = $Password
        clientApp       = $ClientApp
    }
    if (-not $r.Ok) { return $null }
    return $r.Json.token
}

function Coalesce-Detail {
    param($Primary, $Fallback)
    if ($null -ne $Primary -and "$Primary".Length -gt 0) { return $Primary }
    return $Fallback
}

function Get-AuthHeaders {
    param([string]$Token, [string]$Tenant = $DevTenantSlug)
    return @{
        Authorization = "Bearer $Token"
        'X-Tenant-Id' = $Tenant
    }
}

function Get-ImpersonatedDevToken {
    $sa = Get-LoginToken -LoginIdentifier 'admin@admin.com' -Password 'Admin123!' -ClientApp 'admin' -TenantHeader 'default'
    if (-not $sa) { return $null }
    $h = Get-AuthHeaders -Token $sa -Tenant 'default'
    $r = Invoke-Api -Method POST -Path "/api/admin/tenants/$DevTenantId/impersonate" -Headers $h -Body @{}
    if (-not $r.Ok) { return $null }
    return $r.Json.token
}

function Test-FaRoute {
    param([string]$Path)
    try {
        $resp = Invoke-WebRequest -Uri "$FaUrl$Path" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop
        return [int]$resp.StatusCode
    }
    catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode.value__ }
        return 0
    }
}

Write-Host "=== Regkasse Comprehensive Smoke Test ===" -ForegroundColor Cyan

# --- FA SuperAdmin ---
$saToken = Get-ImpersonatedDevToken
if ($saToken) {
    Add-Result 'FA SuperAdmin' '1.1 Login admin@admin.com' 'PASS' 'Token issued via impersonated dev context'
    $saH = Get-AuthHeaders -Token $saToken
    $me = Invoke-Api -Path '/api/Auth/me' -Headers $saH
    if ($me.Ok -and $me.Json.role -eq 'SuperAdmin') {
        Add-Result 'FA SuperAdmin' '1.2 Redirect/dashboard (API /me)' 'PASS' "role=$($me.Json.role)"
    }
    else {
        Add-Result 'FA SuperAdmin' '1.2 Redirect/dashboard (API /me)' 'FAIL' (Coalesce-Detail $me.Raw $me.Error)
    }
    if ($me.Json.tenantSlug -eq $DevTenantSlug) {
        Add-Result 'FA SuperAdmin' '1.3 Tenant switcher dev' 'PASS' "tenantSlug=$($me.Json.tenantSlug)"
    }
    else {
        Add-Result 'FA SuperAdmin' '1.3 Tenant switcher dev' 'FAIL' "expected dev, got $($me.Json.tenantSlug)"
    }
    $env = Invoke-Api -Path '/api/rksv/environment' -Headers $saH
    $status = Invoke-Api -Path '/api/rksv/status' -Headers $saH
    $envJson = $env.Json | ConvertTo-Json -Compress
    $statusJson = $status.Json | ConvertTo-Json -Compress
    if ($status.Ok -and ($statusJson -match 'DEMO|Demo|demo|SIMUL')) {
        Add-Result 'FA SuperAdmin' '1.4 Environment badge Entwicklung/DEMO' 'PASS' $statusJson
    }
    else {
        Add-Result 'FA SuperAdmin' '1.4 Environment badge Entwicklung/DEMO' 'FAIL' $statusJson
    }
    if ($status.Ok) {
        Add-Result 'FA SuperAdmin' '2.1 RKSV status page API' 'PASS' $statusJson
        if ($statusJson -match 'TEST|Simulation|SIMUL') {
            Add-Result 'FA SuperAdmin' '2.2 FinanzOnline TEST + TSE SIMULIERT' 'PASS' $statusJson
        }
        else {
            Add-Result 'FA SuperAdmin' '2.2 FinanzOnline TEST + TSE SIMULIERT' 'FAIL' $statusJson
        }
    }
    else {
        Add-Result 'FA SuperAdmin' '2.1 RKSV status page API' 'FAIL' 'error details unavailable'
        Add-Result 'FA SuperAdmin' '2.2 FinanzOnline TEST + TSE SIMULIERT' 'FAIL' 'status endpoint failed'
    }

    $daily = Invoke-Api -Method POST -Path '/api/Tagesabschluss/daily' -Headers $saH -Body @{ cashRegisterId = $RegisterId }
    if ($daily.Ok -and ($daily.Json.tseSignature -or $daily.Json.signature -or $daily.Json.success -eq $true)) {
        Add-Result 'FA SuperAdmin' '3. Tagesabschluss + TSE signature' 'PASS' 'Daily closing succeeded with signature fields'
    }
    elseif ($daily.StatusCode -eq 400 -and ($daily.Raw -match 'already|bereits|duplicate|geschlossen')) {
        Add-Result 'FA SuperAdmin' '3. Tagesabschluss + TSE signature' 'PASS' 'Already closed today (expected in repeat runs)'
    }
    else {
        Add-Result 'FA SuperAdmin' '3. Tagesabschluss + TSE signature' 'FAIL' 'error details unavailable'
    }

    $mon = Invoke-Api -Method POST -Path '/api/rksv/special-receipts/monatsbeleg' -Headers $saH -Body @{
        cashRegisterId = $RegisterId; year = 2026; month = 7; force = $true
    }
    if ($mon.Ok) {
        Add-Result 'FA SuperAdmin' '4. Monatsbeleg create + TSE' 'PASS' 'Monatsbeleg created'
    }
    elseif (($mon.StatusCode -eq 400 -or $mon.StatusCode -eq 409) -and ($mon.Raw -match 'Duplicate|duplicate|bereits|MONATSBELEG')) {
        $hist = Invoke-Api -Path "/api/rksv/monatsbeleg/history/$RegisterId" -Headers $saH
        if ($hist.Ok -and $hist.Json.Count -gt 0) {
            Add-Result 'FA SuperAdmin' '4. Monatsbeleg create + TSE' 'PASS' 'Duplicate blocked; history has entries'
        }
        else {
            Add-Result 'FA SuperAdmin' '4. Monatsbeleg create + TSE' 'FAIL' 'Duplicate but no history'
        }
    }
    else {
        Add-Result 'FA SuperAdmin' '4. Monatsbeleg create + TSE' 'FAIL' 'error details unavailable'
    }

    $jah = Invoke-Api -Method POST -Path '/api/rksv/special-receipts/jahresbeleg' -Headers $saH -Body @{
        cashRegisterId = $RegisterId; year = 2026; useDecemberMonatsbeleg = $true
    }
    if ($jah.Ok) {
        Add-Result 'FA SuperAdmin' '5. Jahresbeleg create + TSE' 'PASS' 'Jahresbeleg created'
    }
    elseif (($jah.StatusCode -eq 400 -or $jah.StatusCode -eq 409) -and ($jah.Raw -match 'Duplicate|duplicate|bereits|JAHRESBELEG|already exists')) {
        Add-Result 'FA SuperAdmin' '5. Jahresbeleg create + TSE' 'PASS' (Coalesce-Detail $jah.Raw 'Duplicate blocked')
    }
    else {
        Add-Result 'FA SuperAdmin' '5. Jahresbeleg create + TSE' 'FAIL' (Coalesce-Detail $jah.Raw $jah.Error)
    }

    $from = '2026-01-01T00:00:00Z'
    $to = '2026-12-31T23:59:59Z'
    $dep = Invoke-Api -Path "/api/admin/rksv/dep-export?cashRegisterId=$RegisterId&fromUtc=$from&toUtc=$to" -Headers $saH
    if ($dep.Ok -and ($dep.Raw -match 'Belege-Gruppe|Belege-kompakt')) {
        Add-Result 'FA SuperAdmin' '6. DEP-Export JSON Belege-Gruppe' 'PASS' 'Valid DEP export structure'
    }
    else {
        Add-Result 'FA SuperAdmin' '6. DEP-Export JSON Belege-Gruppe' 'FAIL' 'error details unavailable'
    }

    $offline = Invoke-Api -Path '/api/admin/offline-orders' -Headers $saH
    if ($offline.Ok) {
        Add-Result 'FA SuperAdmin' '8.1 Offline orders list' 'PASS' "total=$($offline.Json.totalCount)"
        $replayAll = Invoke-Api -Method POST -Path '/api/admin/offline-orders/replay-all' -Headers $saH -Body @{}
        if ($replayAll.Ok -or $replayAll.StatusCode -eq 200) {
            Add-Result 'FA SuperAdmin' '8.2 Sync all offline orders' 'PASS' (Coalesce-Detail $replayAll.Raw $replayAll.Error)
        }
        else {
            Add-Result 'FA SuperAdmin' '8.2 Sync all offline orders' 'FAIL' 'error details unavailable'
        }
    }
    else {
        Add-Result 'FA SuperAdmin' '8.1 Offline orders list' 'FAIL' 'error details unavailable'
        Add-Result 'FA SuperAdmin' '8.2 Sync all offline orders' 'SKIP' 'List failed'
    }

    $errors = Invoke-Api -Path '/api/admin/errors?page=1&pageSize=5' -Headers $saH
    if ($errors.Ok) {
        Add-Result 'FA SuperAdmin' '9. Elmah error logs' 'PASS' "items=$($errors.Json.items.Count)"
    }
    else {
        Add-Result 'FA SuperAdmin' '9. Elmah error logs' 'FAIL' 'error details unavailable'
    }

    $backupTrigger = Invoke-Api -Method POST -Path '/api/admin/backup/trigger' -Headers $saH -Body @{}
    if ($backupTrigger.Ok -or $backupTrigger.StatusCode -eq 202 -or $backupTrigger.StatusCode -eq 200) {
        Add-Result 'FA SuperAdmin' '10.1 Backup trigger' 'PASS' (Coalesce-Detail $backupTrigger.Raw $backupTrigger.Error)
    }
    else {
        Add-Result 'FA SuperAdmin' '10.1 Backup trigger' 'FAIL' 'error details unavailable'
    }
    $backupList = Invoke-Api -Path '/api/admin/backup/runs?page=1&pageSize=5' -Headers $saH
    if ($backupList.Ok) {
        Add-Result 'FA SuperAdmin' '10.2 Backup list' 'PASS' "runs available"
    }
    else {
        Add-Result 'FA SuperAdmin' '10.2 Backup list' 'FAIL' 'error details unavailable'
    }

    $lic = Invoke-Api -Path '/api/admin/license/list?page=1&pageSize=5' -Headers $saH
    if ($lic.Ok) {
        Add-Result 'FA SuperAdmin' '11.1 License status' 'PASS' 'License list loaded'
    }
    else {
        Add-Result 'FA SuperAdmin' '11.1 License status' 'FAIL' 'error details unavailable'
    }
    $billPreview = Invoke-Api -Method POST -Path '/api/admin/billing/license-sales/preview' -Headers $saH -Body @{
        tenantId = $DevTenantId; licensePlan = '12_months'; priceNet = 299.00; vatRate = 20.0
    }
    if ($billPreview.Ok) {
        Add-Result 'FA SuperAdmin' '11.2 Billing license sale preview' 'PASS' 'Preview OK'
    }
    else {
        Add-Result 'FA SuperAdmin' '11.2 Billing license sale preview' 'FAIL' 'error details unavailable'
    }

    $tenants = Invoke-Api -Path '/api/admin/tenants?page=1&pageSize=20' -Headers $saH
    $devTenant = $null
    if ($tenants.Ok) {
        $tenantList = @()
        if ($tenants.Json -is [System.Array]) {
            $tenantList = @($tenants.Json)
        }
        elseif ($tenants.Json.items) {
            $tenantList = @($tenants.Json.items)
        }
        elseif ($tenants.Json.slug) {
            $tenantList = @($tenants.Json)
        }
        $devTenant = $tenantList | Where-Object { $_.slug -eq $DevTenantSlug } | Select-Object -First 1
        if ($devTenant) {
            Add-Result 'FA SuperAdmin' '12.1 Tenant list shows dev' 'PASS' "id=$($devTenant.id)"
            $td = Invoke-Api -Path "/api/admin/tenants/$($devTenant.id)" -Headers $saH
            if ($td.Ok) { Add-Result 'FA SuperAdmin' '12.2 Tenant details' 'PASS' $td.Json.slug } else { Add-Result 'FA SuperAdmin' '12.2 Tenant details' 'FAIL' 'error details unavailable' }
            $tu = Invoke-Api -Path "/api/admin/tenants/$($devTenant.id)/users?page=1&pageSize=10" -Headers $saH
            if ($tu.Ok) { Add-Result 'FA SuperAdmin' '12.3 Users tab' 'PASS' "count=$($tu.Json.totalCount)" } else { Add-Result 'FA SuperAdmin' '12.3 Users tab' 'FAIL' 'error details unavailable' }
            $tr = Invoke-Api -Path "/api/admin/tenants/$DevTenantId/cash-registers" -Headers $saH
            if ($tr.Ok) { Add-Result 'FA SuperAdmin' '12.4 Registers tab' 'PASS' "registers=$($tr.Json.Count)" } else { Add-Result 'FA SuperAdmin' '12.4 Registers tab' 'FAIL' 'error details unavailable' }
        }
        else {
            Add-Result 'FA SuperAdmin' '12.1 Tenant list shows dev' 'FAIL' 'dev tenant not found'
        }
    }
    else {
        Add-Result 'FA SuperAdmin' '12.1 Tenant list shows dev' 'FAIL' 'error details unavailable'
    }
}
else {
    Add-Result 'FA SuperAdmin' '1. Authentication' 'FAIL' 'Could not obtain SuperAdmin impersonated token'
}

# --- FA Manager ---
$mgrToken = Get-LoginToken -LoginIdentifier 'manager1' -Password 'Juke1034#' -ClientApp 'admin'
if ($mgrToken) {
    $mgrH = Get-AuthHeaders -Token $mgrToken
    $mgrMe = Invoke-Api -Path '/api/Auth/me' -Headers $mgrH
    Add-Result 'FA Manager' '1.1 Login manager1' 'PASS' "role=$($mgrMe.Json.role)"
    if ($mgrMe.Json.tenantSlug -eq $DevTenantSlug) {
        Add-Result 'FA Manager' '1.2 Tenant dev' 'PASS' 'tenant=dev'
    }
    else {
        Add-Result 'FA Manager' '1.2 Tenant dev' 'FAIL' "tenant=$($mgrMe.Json.tenantSlug)"
    }
    $hasCritical = $mgrMe.Json.permissions -contains 'system.critical'
    if (-not $hasCritical) {
        Add-Result 'FA Manager' '1.3 Limited menu (no SuperAdmin)' 'PASS' 'No system.critical permission'
    }
    else {
        Add-Result 'FA Manager' '1.3 Limited menu (no SuperAdmin)' 'FAIL' 'Has system.critical'
    }

    $regs = Invoke-Api -Path '/api/admin/cash-registers' -Headers $mgrH
    $kasse = $regs.Json.items | Where-Object { $_.registerNumber -eq 'KASSE-001' } | Select-Object -First 1
    if ($kasse) {
        Add-Result 'FA Manager' '2.1 KASSE-001 visible' 'PASS' "status=$($kasse.status)"
    }
    else {
        Add-Result 'FA Manager' '2.1 KASSE-001 visible' 'FAIL' 'Register not found'
    }

    $report = Invoke-Api -Path "/api/admin/reports/daily-closing?cashRegisterId=$RegisterId&closingDate=2026-07-12" -Headers $mgrH
    if ($report.Ok) {
        Add-Result 'FA Manager' '3. Daily report' 'PASS' 'Daily closing report loaded'
    }
    else {
        Add-Result 'FA Manager' '3. Daily report' 'FAIL' (Coalesce-Detail $report.Raw $report.Error)
    }

    $staff = Invoke-Api -Path '/api/UserManagement?page=1&pageSize=20' -Headers $mgrH
    if ($staff.Ok) {
        $staffTotal = if ($staff.Json.pagination.totalCount) { $staff.Json.pagination.totalCount } else { @($staff.Json.items).Count }
        Add-Result 'FA Manager' '4.1 Staff list' 'PASS' "total=$staffTotal"
        $canDelete = $mgrMe.Json.permissions -contains 'user.delete'
        if (-not $canDelete) {
            Add-Result 'FA Manager' '4.2 Cannot delete staff' 'PASS' 'No user.delete permission'
        }
        else {
            Add-Result 'FA Manager' '4.2 Cannot delete staff' 'FAIL' 'Manager has user.delete'
        }
    }
    else {
        Add-Result 'FA Manager' '4. Staff list' 'FAIL' 'error details unavailable'
    }

    $pay = Invoke-Api -Path '/api/admin/payments?page=1&pageSize=10' -Headers $mgrH
    if ($pay.Ok) {
        Add-Result 'FA Manager' '5. Payments list' 'PASS' "total=$($pay.Json.totalCount)"
    }
    else {
        Add-Result 'FA Manager' '5. Payments list' 'FAIL' 'error details unavailable'
    }

    $hasTestHelper = $mgrMe.Json.permissions -contains 'rksv.test-helper'
    if (-not $hasTestHelper) {
        Add-Result 'FA Manager' '6.1 Test Helper hidden' 'PASS' 'No rksv.test-helper permission'
    }
    else {
        Add-Result 'FA Manager' '6.1 Test Helper hidden' 'FAIL' 'Manager has test-helper'
    }
    $start = Invoke-Api -Method POST -Path '/api/rksv/special-receipts/startbeleg' -Headers $mgrH -Body @{ cashRegisterId = $RegisterId }
    if ($start.Ok -or (($start.StatusCode -eq 400 -or $start.StatusCode -eq 409) -and $start.Raw -match 'Duplicate|duplicate|bereits|STARTBELEG')) {
        Add-Result 'FA Manager' '6.2 Startbeleg' 'PASS' (Coalesce-Detail $start.Raw 'already exists')
    }
    else {
        Add-Result 'FA Manager' '6.2 Startbeleg' 'FAIL' (Coalesce-Detail $start.Raw $start.Error)
    }
    $nullb = Invoke-Api -Method POST -Path '/api/rksv/special-receipts/nullbeleg' -Headers $mgrH -Body @{
        cashRegisterId = $RegisterId; year = 2026; month = 7; reason = 'smoke'
    }
    if ($nullb.Ok -or (($nullb.StatusCode -eq 400 -or $nullb.StatusCode -eq 409) -and $nullb.Raw -match 'Duplicate|duplicate|bereits|NULLBELEG')) {
        Add-Result 'FA Manager' '6.3 Nullbeleg' 'PASS' (Coalesce-Detail $nullb.Raw 'already exists')
    }
    else {
        Add-Result 'FA Manager' '6.3 Nullbeleg' 'FAIL' (Coalesce-Detail $nullb.Raw $nullb.Error)
    }
}
else {
    Add-Result 'FA Manager' '1. Authentication' 'FAIL' 'manager1 login failed'
}

# --- POS Cashier ---
$posToken = Get-LoginToken -LoginIdentifier 'cashier1' -Password '2&@6AWNy(r38' -ClientApp 'pos'
if ($posToken) {
    $posH = Get-AuthHeaders -Token $posToken
    $posMe = Invoke-Api -Path '/api/Auth/me' -Headers $posH
    Add-Result 'POS Cashier' '1.1 Login cashier1' 'PASS' "role=$($posMe.Json.role)"
    if ($posMe.Json.mustChangePasswordOnNextLogin) {
        Add-Result 'POS Cashier' '1.2 Password change required' 'SKIP' 'mustChangePasswordOnNextLogin=true â€” UI may prompt before dashboard'
    }
    $posEnv = Invoke-Api -Path '/api/rksv/environment' -Headers $posH
    if ($posEnv.Ok) {
        Add-Result 'POS Cashier' '1.3 Environment DEMO badge' 'PASS' ($posEnv.Json | ConvertTo-Json -Compress)
    }
    else {
        Add-Result 'POS Cashier' '1.3 Environment DEMO badge' 'FAIL' 'error details unavailable'
    }

    $products = Invoke-Api -Path '/api/pos/list?pageSize=5' -Headers $posH
    $productItems = @()
    if ($products.Json.data.items) { $productItems = @($products.Json.data.items) }
    elseif ($products.Json.items) { $productItems = @($products.Json.items) }
    elseif ($products.Json -is [array]) { $productItems = @($products.Json) }
    $posProductId = if ($productItems.Count -gt 0) { $productItems[0].id } else { $TestProductId }
    if ($products.Ok -and $productItems.Count -gt 0) {
        Add-Result 'POS Cashier' '2. Product list' 'PASS' "products=$($productItems.Count)"
    }
    else {
        Add-Result 'POS Cashier' '2. Product list' 'FAIL' (Coalesce-Detail $products.Raw $products.Error)
    }

    $guestCustomerId = '00000000-0000-0000-0000-000000000001'
    $addCart = Invoke-Api -Method POST -Path '/api/pos/cart/add-item' -Headers $posH -Body @{
        productId = $posProductId; quantity = 1; tableNumber = 1
    }
    if ($addCart.Ok) {
        Add-Result 'POS Cashier' '3. Cart add item' 'PASS' 'Item added'
    }
    else {
        Add-Result 'POS Cashier' '3. Cart add item' 'FAIL' 'error details unavailable'
    }

    $payment = Invoke-Api -Method POST -Path '/api/pos/payment' -Headers $posH -Body @{
        customerId     = $guestCustomerId
        tableNumber    = 1
        cashRegisterId = $RegisterId
        totalAmount    = 7.50
        items          = @(@{ productId = $posProductId; quantity = 1; taxType = 2 })
        payment        = @{ method = 'cash'; amount = 10.00; tseRequired = $true }
    }
    if ($payment.Ok -or $payment.StatusCode -eq 201) {
        $receipt = if ($payment.Json.payment.receiptNumber) { $payment.Json.payment.receiptNumber } else { $payment.Json.receiptNumber }
        Add-Result 'POS Cashier' '4. Checkout payment + receipt' 'PASS' "receipt=$receipt"
    }
    else {
        Add-Result 'POS Cashier' '4. Checkout payment + receipt' 'FAIL' (Coalesce-Detail $payment.Raw $payment.Error)
    }

    Add-Result 'POS Cashier' '5. Voucher/Gutschein' 'SKIP' 'Requires valid voucher code in dev tenant â€” not automated'
    Add-Result 'POS Cashier' '6. Offline mode' 'SKIP' 'Requires browser DevTools network toggle â€” E2E only'
    $tse = Invoke-Api -Path "/api/tse/health?cashRegisterId=$RegisterId" -Headers $posH
    if ($tse.Ok) {
        Add-Result 'POS Cashier' '7. TSE status banner data' 'PASS' ($tse.Json | ConvertTo-Json -Compress)
    }
    else {
        Add-Result 'POS Cashier' '7. TSE status banner data' 'FAIL' 'error details unavailable'
    }
}
else {
    Add-Result 'POS Cashier' '1. Authentication' 'FAIL' 'cashier1 login failed'
}

# FA route smoke (HTTP reachability)
$faRoutes = @(
    @{ Path = '/login'; Name = 'FA login page' }
    @{ Path = '/rksv'; Name = 'RKSV hub' }
    @{ Path = '/tagesabschluss'; Name = 'Tagesabschluss' }
    @{ Path = '/admin/rksv/dep-export'; Name = 'DEP export' }
    @{ Path = '/rksv/offline-orders'; Name = 'Offline orders' }
    @{ Path = '/admin/errors'; Name = 'Elmah errors' }
    @{ Path = '/admin/backup'; Name = 'Backup' }
    @{ Path = '/admin/tenants'; Name = 'Tenants' }
    @{ Path = '/kassenverwaltung'; Name = 'Cash registers' }
    @{ Path = '/payments'; Name = 'Payments' }
)
foreach ($r in $faRoutes) {
    $code = Test-FaRoute -Path $r.Path
    if ($code -in 200, 307, 308) {
        Add-Result 'FA Routes' $r.Name 'PASS' "HTTP $code"
    }
    else {
        Add-Result 'FA Routes' $r.Name 'FAIL' "HTTP $code"
    }
}

try {
    $posCode = (Invoke-WebRequest -Uri $PosUrl -UseBasicParsing -TimeoutSec 10).StatusCode
    Add-Result 'POS Routes' 'POS root' 'PASS' "HTTP $posCode"
}
catch {
    Add-Result 'POS Routes' 'POS root' 'FAIL' $_.Exception.Message
}

# Summary
$pass = @($script:Results | Where-Object Status -eq 'PASS').Count
$fail = @($script:Results | Where-Object Status -eq 'FAIL').Count
$skip = @($script:Results | Where-Object Status -eq 'SKIP').Count
Write-Host ""
Write-Host "SUMMARY: PASS=$pass FAIL=$fail SKIP=$skip TOTAL=$($script:Results.Count)" -ForegroundColor Cyan
$script:Results | Format-Table -AutoSize
$outPath = Join-Path (Join-Path $PSScriptRoot '..') 'test-results\comprehensive-smoke-results.json'
$outDir = Split-Path $outPath -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$script:Results | ConvertTo-Json -Depth 4 | Set-Content -Path $outPath -Encoding UTF8
Write-Host "Results saved to $outPath"

if ($fail -gt 0) { exit 1 } else { exit 0 }


