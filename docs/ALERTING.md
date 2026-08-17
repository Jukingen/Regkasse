# Alerting guide — Regkasse

How critical signals reach Slack, email, and on-call — without duplicate noise.

**Last updated:** 2026-08-17

| Related | Link |
|---------|------|
| Monitoring | [`MONITORING.md`](MONITORING.md) |
| Metrics | [`METRICS.md`](METRICS.md) |
| Prometheus rules | [`../monitoring/prometheus/alerts.yml`](../monitoring/prometheus/alerts.yml) |
| Alertmanager example | [`../monitoring/alertmanager/alertmanager.yml.example`](../monitoring/alertmanager/alertmanager.yml.example) |
| FA Sentry recipes | [`../frontend-admin/monitoring/sentry-alert-recipes.md`](../frontend-admin/monitoring/sentry-alert-recipes.md) |
| CI notify | [`.github/workflows/notify-failure.yml`](../.github/workflows/notify-failure.yml) |

---

## Channels

| Channel | Use for | Config |
|---------|---------|--------|
| **Slack `#regkasse-alerts`** | Warnings + most criticals | `SLACK_WEBHOOK_URL` → Alertmanager / Sentry / Actions |
| **Slack / Pager `#regkasse-oncall`** | Page-worthy (API down, TSE/FON) | `ONCALL_WEBHOOK_URL` |
| **Email** | Ops mailbox + security / compliance | Alertmanager `email_configs` → `ops@regkasse.at` (override `ALERTMANAGER_EMAIL_TO`); also Activity + Sentry |
| **FA Activity bell** | In-app business events | `ActivityEventType` + SSE |
| **PagerDuty** | Optional replacement for on-call Slack | `PAGERDUTY_ROUTING_KEY` on the `oncall` receiver |

Default tracked `monitoring/alertmanager/alertmanager.yml` uses a **null** receiver (no spam on local compose). Production must **render** [`alertmanager.yml.example`](../monitoring/alertmanager/alertmanager.yml.example) on the host — Alertmanager does **not** expand `${ENV}` itself.

---

## Alert matrix

### Infrastructure / availability (Prometheus + blackbox)

| Alert | Severity | Channel | Meaning |
|-------|----------|---------|---------|
| `RegkasseApiDown` | critical | pager | `/api/health/live` fail ≥2m |
| `RegkasseApiNotReady` | critical | slack | `/api/health/ready` fail ≥5m (DB/fiscal) |
| `RegkasseAdminDown` | warning | slack | FA `/health` fail ≥3m |
| `RegkassePosWebDown` | warning | slack | POS `/healthz` fail ≥5m |

### API SLO

| Alert | Severity | Channel | Threshold |
|-------|----------|---------|-----------|
| `RegkasseHighErrorRate` | critical | slack | error ratio >5% for 5m |
| `RegkasseHighLatencyP95` | warning | slack | p95 >1000ms for 10m |

### Fiscal / business

| Alert | Severity | Channel | Meaning |
|-------|----------|---------|---------|
| `RegkasseFinanzOnlineFailures` | critical | pager | FON submit failures elevated |
| `RegkasseTseFleetUnhealthy` | critical | pager | `tse_fleet_status` degraded |
| `RegkasseOfflineReplayFailures` | warning | slack | replay fail ratio >10% |

### Frontend Admin (Sentry)

See [`sentry-alert-recipes.md`](../frontend-admin/monitoring/sentry-alert-recipes.md):

- API error rate >5% (client window)
- API call >1000ms
- Unhandled / axios 5xx volume
- External uptime on `admin…/health`

### CI / deploy

| Signal | Channel |
|--------|---------|
| Workflow failure | `notify-failure.yml` → `SLACK_WEBHOOK_URL` |
| Smoke fail + rollback | `ONCALL_WEBHOOK_URL` (staging/canary) |
| Production smoke fail | Manual rollback + on-call text |

### Security

| Signal | Channel |
|--------|---------|
| Auth anomalies / critical actions | Activity + email (notification config) |
| Sentry security issues | Email + Slack (Sentry project alerts) |
| License lockdown / grace | Activity `LicenseExpired` / reminders |

Prefer routing **security** to a restricted mailbox; do not put secrets in Slack payloads.

---

## Enable Alertmanager receivers (host only)

Tracked `alertmanager.yml` stays null. Render the example (Slack `#regkasse-alerts` / `#regkasse-oncall`, email `ops@regkasse.at`, optional PagerDuty):

```powershell
$env:SLACK_WEBHOOK_URL = "https://hooks.slack.com/services/..."   # host secret
$env:ONCALL_WEBHOOK_URL = "https://hooks.slack.com/services/..."
$env:ALERTMANAGER_EMAIL_TO = "ops@regkasse.at"
# Optional SMTP + PagerDuty:
# $env:ALERTMANAGER_SMTP_SMARTHOST = "smtp.example.com:587"
# $env:ALERTMANAGER_SMTP_FROM = "alerts@regkasse.at"
# $env:ALERTMANAGER_SMTP_AUTH_USERNAME = "..."
# $env:ALERTMANAGER_SMTP_AUTH_PASSWORD = "..."
# $env:PAGERDUTY_ROUTING_KEY = "..."

pwsh ./monitoring/alertmanager/render-alertmanager-config.ps1
# Linux alternative: envsubst < monitoring/alertmanager/alertmanager.yml.example > /secure/alertmanager.yml
```

Mount the **rendered** file in compose (do not commit it). Slack-only: delete `email_configs`, `pagerduty_configs`, and unused `smtp_*` keys from the rendered YAML before reload.

```bash
# Validate, then reload
amtool check-config monitoring/alertmanager/alertmanager.rendered.yml
docker compose -f monitoring/docker-compose.monitoring.yml up -d alertmanager
curl -fsS http://127.0.0.1:9093/-/healthy
curl -X POST http://127.0.0.1:9090/-/reload   # Prometheus, if needed
```

### Test routing (Staging / loopback only)

This is **not** proof that Production pages a human until the host uses the rendered file and someone acknowledges the test alert.

```bash
curl -fsS http://127.0.0.1:9093/-/healthy

# Synthetic alert (requires a live Alertmanager with real receivers)
curl -sS -X POST http://127.0.0.1:9093/api/v2/alerts \
  -H 'Content-Type: application/json' \
  -d '[{"labels":{"alertname":"RegkasseRoutingTest","severity":"warning","channel":"slack"},"annotations":{"summary":"Routing test","description":"Ignore — Alertmanager receiver check"}}]'
```

Confirm Slack `#regkasse-alerts` (or pager channel / email). Silence afterwards.

Windows:

```powershell
.\scripts\ops\test-alertmanager-routing.ps1
.\scripts\ops\test-alertmanager-routing.ps1 -Pager
```

---

## Tuning

- Raise `for:` windows on staging to avoid flapping during deploys.
- Inhibit warning when critical of same `alertname` is firing (configured in example).
- Keep fiscal pager alerts **tight** — false TSE pages erode trust.

---

## Runbook links

| Alert | First checks |
|-------|----------------|
| ApiDown | `docker logs regkasse-backend-prod`; compose ps; disk full |
| ApiNotReady | `/api/health/ready` JSON; DB; `TseProductionOptionsValidator`; FON simulation flags |
| FinanzOnlineFailures | FA FON outbox; cutover guard; vendor SOAP errors |
| TseFleetUnhealthy | `/health/tse/mode`; Fiskaly credentials; device health FA |
| HighErrorRate | Grafana API error panel; recent deploy tag; Sentry |

More: [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).
