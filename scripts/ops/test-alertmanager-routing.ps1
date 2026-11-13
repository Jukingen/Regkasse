#Requires -Version 5.1
<#
.SYNOPSIS
  POST a synthetic alert to a local or host Alertmanager.

.DESCRIPTION
  Does not prove Slack/email delivery unless Alertmanager is running with rendered
  receivers (not the in-repo null config). Never commit webhook URLs.

  Usage:
    .\scripts\ops\test-alertmanager-routing.ps1
    .\scripts\ops\test-alertmanager-routing.ps1 -BaseUrl http://127.0.0.1:9093 -Pager
#>

param(
    [string]$BaseUrl = "http://127.0.0.1:9093",
    [switch]$Pager
)

$ErrorActionPreference = "Stop"
$health = "$BaseUrl/-/healthy"
try {
    $h = Invoke-WebRequest -Uri $health -UseBasicParsing -TimeoutSec 5
    if ($h.StatusCode -ne 200) { throw "health HTTP $($h.StatusCode)" }
}
catch {
    Write-Error "Alertmanager not reachable at $health. Start monitoring compose or point -BaseUrl at the host. $_"
    exit 1
}

$channel = if ($Pager) { "pager" } else { "slack" }
$body = @(
    @{
        labels = @{
            alertname = "RegkasseRoutingTest"
            severity  = "warning"
            channel   = $channel
        }
        annotations = @{
            summary     = "Routing test"
            description = "Ignore — Alertmanager receiver check"
        }
    }
) | ConvertTo-Json -Depth 6

Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v2/alerts" -ContentType "application/json" -Body "[$body]" | Out-Null
Write-Host "Posted RegkasseRoutingTest (channel=$channel) to $BaseUrl/api/v2/alerts"
Write-Host "Confirm Slack #regkasse-alerts (or #regkasse-oncall / email ops@regkasse.at), then silence the alert."
exit 0
