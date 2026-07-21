# Health & monitoring guardrails

## Layout

There is no legacy plain `"OK"` dependency probe at `/api/health` anymore. Canonical surfaces:

| Path | Purpose | I/O |
|------|---------|-----|
| `GET /health`, `GET /health/live`, `GET /api/health/live` | Liveness | None (plain `OK`) |
| `GET /api/health/ready`, `GET /health/ready` | Readiness | DB `CanConnect` only (≤2s timeout) |
| `GET /api/health` | Dependency snapshot | DB + **cached** TSE + **cached** NTP |
| `GET /api/health/license` | License diagnostic (unchanged) | In-process license service |

Implementation: `Controllers/HealthController.cs` + `HealthChecks/*`.

## Critical dependencies

| Check | Source | Unhealthy vs Degraded |
|-------|--------|------------------------|
| `database` | `DatabaseHealthCheck` | Unhealthy → HTTP 503 |
| `tse` | `ITseHealthMonitor` snapshot (background probe) | Offline/Degraded → HTTP 200 Degraded |
| `ntp` | `INtpTimeSyncStatus` + `NtpSettings` (no NTP network call) | Fiscal blocked → HTTP 200 Degraded |

TSE/NTP must **not** perform device or network I/O on the hot health path — that is handled by `TseHealthCheckService` / `NtpTimeSyncService`.

## Performance

- DB probe capped at **2 seconds** (`DatabaseHealthCheck.TimeoutMilliseconds`).
- Ready probe runs **only** the `ready` tag (database).
- Full `/api/health` runs `deps` tag only (database, tse, ntp) — not FinanzOnline/backup/elmah mode checks.
- Liveness endpoints remain allocation-light for Kubernetes / LB spam.

## Ops notes

- Orchestrators: use `/health/live` (or `/health`) for liveness and `/health/ready` for readiness.
- Monitoring dashboards: prefer `/api/health` JSON `entries.*.status` for TSE/NTP visibility without failing the process when fiscal deps are degraded.
