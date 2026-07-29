# Health & monitoring guardrails

## Layout

There is no legacy plain `"OK"` dependency probe at `/api/health` anymore. Canonical surfaces:

| Path | Purpose | I/O |
|------|---------|-----|
| `GET /health`, `GET /health/live`, `GET /api/health/live` | Liveness | None (plain `OK`) |
| `GET /api/health/ready`, `GET /health/ready` | Readiness | DB `CanConnect` (≤2s) + **TSE fiscal config lock** + **FinanzOnline simulation gate** (config only; no BMF/device I/O) |
| `GET /api/health` | Dependency snapshot | DB + **cached** TSE + **cached** NTP |
| `GET /health/tse/mode` | TSE fiscal Production lock detail | Config only |
| `GET /health/finanzonline/mode` | FON simulation vs real | Config only |
| `GET /api/health/license` | License diagnostic (unchanged) | In-process license service |
| `GET /health/migrations`, `GET /api/health/migrations` | EF pending/applied posture | DB `__EFMigrationsHistory` (≤3s) |

Implementation: `Controllers/HealthController.cs` + `HealthChecks/*`.  
Fiscal Development vs Production: [`docs/ENVIRONMENT_CONFIGURATION.md`](../../docs/ENVIRONMENT_CONFIGURATION.md).

## Critical dependencies

| Check | Source | Unhealthy vs Degraded |
|-------|--------|------------------------|
| `database` | `DatabaseHealthCheck` | Unhealthy → HTTP 503 |
| `tse-fiscal-config` | `TseFiscalConfigLockEvaluator` | Production lock violated → Unhealthy (escape hatch → Degraded); Development → Healthy |
| `finanzonline` | FON `UseSimulation` + host lock | Production + simulation → Unhealthy; Development + simulation → Healthy |
| `tse` | `ITseHealthMonitor` snapshot (background probe) | Offline/Degraded → HTTP 200 Degraded on `/api/health` only (not on ready) |
| `ntp` | `INtpTimeSyncStatus` + `NtpSettings` (no NTP network call) | Fiscal blocked → HTTP 200 Degraded on `/api/health` only |
| `ef-migrations` | `EfMigrationsHealthCheck` / `IMigrationStatusService` | Pending → Degraded (HTTP 200); DB error → Unhealthy (503) |

TSE/NTP **device** probes must **not** run on the ready path — that remains `TseHealthCheckService` / `NtpTimeSyncService` for `/api/health`.

## Performance

- DB probe capped at **2 seconds** (`DatabaseHealthCheck.TimeoutMilliseconds`).
- Ready probe runs the `ready` tag: **database**, **tse-fiscal-config**, **finanzonline**.
- Full `/api/health` runs `deps` tag only (database, tse, ntp) — not backup/elmah mode checks.
- Liveness endpoints remain allocation-light for Kubernetes / LB spam.

## Ops notes

- Orchestrators: use `/health/live` (or `/health`) for liveness and `/health/ready` for readiness.
- Production misconfiguration (Soft TSE, FON simulation) fails **startup** (`ValidateOnStart`) and keeps ready **Unhealthy**.
- Monitoring dashboards: prefer `/api/health` JSON `entries.*.status` for device TSE/NTP visibility without failing the process when fiscal deps are degraded.
