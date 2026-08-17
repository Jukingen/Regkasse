# Renders alertmanager.yml.example with environment placeholders.
# Alertmanager does not expand ${ENV} itself — this script writes a host-only file.
#
# Usage (from repo root):
#   $env:SLACK_WEBHOOK_URL = "https://hooks.slack.com/services/..."
#   $env:ONCALL_WEBHOOK_URL = "https://hooks.slack.com/services/..."
#   $env:ALERTMANAGER_EMAIL_TO = "ops@regkasse.at"
#   pwsh ./monitoring/alertmanager/render-alertmanager-config.ps1 -OutputPath ./monitoring/alertmanager/alertmanager.rendered.yml
#
# Do not commit the rendered file. Point docker-compose at it:
#   volumes:
#     - ./alertmanager/alertmanager.rendered.yml:/etc/alertmanager/alertmanager.yml:ro

param(
    [string]$TemplatePath = (Join-Path $PSScriptRoot "alertmanager.yml.example"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "alertmanager.rendered.yml")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $TemplatePath)) {
    throw "Template not found: $TemplatePath"
}

$required = @("SLACK_WEBHOOK_URL", "ONCALL_WEBHOOK_URL")
$missing = @($required | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
if ($missing.Count -gt 0) {
    throw "Missing required environment variables: $($missing -join ', ')"
}

$replacements = @{
    '${SLACK_WEBHOOK_URL}'              = $env:SLACK_WEBHOOK_URL
    '${ONCALL_WEBHOOK_URL}'             = $env:ONCALL_WEBHOOK_URL
    '${ALERTMANAGER_SMTP_SMARTHOST}'    = $(if ($env:ALERTMANAGER_SMTP_SMARTHOST) { $env:ALERTMANAGER_SMTP_SMARTHOST } else { "smtp.example.invalid:587" })
    '${ALERTMANAGER_SMTP_FROM}'         = $(if ($env:ALERTMANAGER_SMTP_FROM) { $env:ALERTMANAGER_SMTP_FROM } else { "alerts@regkasse.at" })
    '${ALERTMANAGER_SMTP_AUTH_USERNAME}' = $(if ($env:ALERTMANAGER_SMTP_AUTH_USERNAME) { $env:ALERTMANAGER_SMTP_AUTH_USERNAME } else { "unused" })
    '${ALERTMANAGER_SMTP_AUTH_PASSWORD}' = $(if ($env:ALERTMANAGER_SMTP_AUTH_PASSWORD) { $env:ALERTMANAGER_SMTP_AUTH_PASSWORD } else { "unused" })
    '${ALERTMANAGER_EMAIL_TO}'          = $(if ($env:ALERTMANAGER_EMAIL_TO) { $env:ALERTMANAGER_EMAIL_TO } else { "ops@regkasse.at" })
    '${PAGERDUTY_ROUTING_KEY}'          = $(if ($env:PAGERDUTY_ROUTING_KEY) { $env:PAGERDUTY_ROUTING_KEY } else { "00000000000000000000000000000000" })
}

$text = Get-Content -LiteralPath $TemplatePath -Raw
foreach ($pair in $replacements.GetEnumerator()) {
    $text = $text.Replace($pair.Key, $pair.Value)
}

Set-Content -LiteralPath $OutputPath -Value $text -Encoding utf8
Write-Host "Wrote $OutputPath"
Write-Host "Validate with: amtool check-config `"$OutputPath`""
Write-Host "Then POST a test alert to http://127.0.0.1:9093/api/v2/alerts (host only; do not commit this file)."
