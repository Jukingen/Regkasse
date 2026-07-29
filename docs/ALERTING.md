# Alerting guide — Regkasse

How critical signals reach Slack, email, and on-call — without duplicate noise.

**Last updated:** 2026-07-29

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
| **Email** | Security / compliance digests | Activity notification config + Sentry email |
| **FA Activity bell** | In-app business events | `ActivityEventType` + SSE |
| **PagerDuty / Opsgenie** | Replace on-call Slack when ready | Alertmanager receiver (see example) |

Default Alertmanager config in-repo uses a **null** receiver (no spam). Copy the example and substitute webhooks on the host only.

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

## Enable Alertmanager → Slack

```bash
cd monitoring
cp alertmanager/alertmanager.yml.example alertmanager/alertmanager.yml
# Edit: replace ${SLACK_WEBHOOK_URL} / ${ONCALL_WEBHOOK_URL} with real HTTPS hooks
# (or envsubst from a host secret file — never commit)

docker compose -f docker-compose.monitoring.yml up -d alertmanager
curl -X POST http://127.0.0.1:9090/-/reload   # reload Prometheus if needed
```

Test:

```bash
# Fire a silence-free test via Alertmanager UI or amtool
curl -fsS http://127.0.0.1:9093/-/healthy
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
