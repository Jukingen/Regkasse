# Health & monitoring guardrails

## Layout

There is no legacy plain `"OK"` dependency probe at `/api/health` anymore. Canonical surfaces:

| Path | Purpose | I/O |
|------|---------|-----|
| `GET /health`, `GET /health/live`, `GET /api/health/live` | Liveness | None (plain `OK`) |
| `GET /api/health/ready`, `GET /health/ready` | Readiness | DB `CanConnect` (≤2s) + **TSE fiscal config lock** + **FinanzOnline simulation gate** (config only; no BMF/device I/O) + **cache/Redis ping** (≤1s) |
| `GET /api/health` | Dependency snapshot | DB + **cached** TSE + **cached** NTP + **cache/Redis** |
| `GET /health/tse/mode` | TSE fiscal Production lock detail | Config only |
| `GET /health/finanzonline/mode` | FON simulation vs real | Config only |
| `GET /api/health/license` | License diagnostic | DB `CanConnect` + `issued_licenses` / `license_sales` sample queries + in-process deployment snapshot. `status`: Healthy / Degraded / Unhealthy (503 when Unhealthy). |
| `GET /health/migrations`, `GET /api/health/migrations` | EF pending/applied posture | DB `__EFMigrationsHistory` (≤3s) |

Implementation: `Controllers/HealthController.cs` + `HealthChecks/*`.  
Fiscal Development vs Production: [`docs/ENVIRONMENT_CONFIGURATION.md`](../../docs/ENVIRONMENT_CONFIGURATION.md).

## Critical dependencies

| Check | Source | Unhealthy vs Degraded |
|-------|--------|------------------------|
| `database` | `DatabaseHealthCheck` | Unhealthy → HTTP 503 |
| `tse-fiscal-config` | `TseFiscalConfigLockEvaluator` | Production lock violated → Unhealthy (escape hatch → Degraded); Development → Healthy |
| `finanzonline` | FON `UseSimulation` + host lock | Production + simulation → Unhealthy; Development + simulation → Healthy |
| `cache` | `RedisCacheHealthCheck` via `ICacheService` (`health_check_ping`, ≤1s) + `RedisCacheService.IsRedisAvailable` when Redis backend is registered | Failure / timeout / `IsRedisAvailable=false` → **Degraded** (never Unhealthy — memory fallback may still serve). Top-level `redisStatus`: `Healthy` \| `Degraded` |
| `tse` | `ITseHealthMonitor` snapshot (background probe) | Offline/Degraded → HTTP 200 Degraded on `/api/health` only (not on ready) |
| `ntp` | `INtpTimeSyncStatus` + `NtpSettings` (no NTP network call) | Fiscal blocked → HTTP 200 Degraded on `/api/health` only |
| `ef-migrations` | `EfMigrationsHealthCheck` / `IMigrationStatusService` | Pending → Degraded (HTTP 200); DB error → Unhealthy (503) |

TSE/NTP **device** probes must **not** run on the ready path — that remains `TseHealthCheckService` / `NtpTimeSyncService` for `/api/health`.

### Cache / Redis on ready

- Registered on the `ready` and `deps` tags as check name `cache` (`RedisCacheHealthCheck`).
- Round-trip: `SetAsync` + `GetAsync` on key `health_check_ping` with a **1 second** timeout.
- Redis unreachable or slow → entry status **Degraded**, response field **`redisStatus`: `"Degraded"`**, log at **Warning** (not Error). Ready overall stays HTTP **200** when only cache is degraded (same as other Degraded deps).
- **Degraded vs Unhealthy:** Redis alone never marks ready as **Unhealthy** (HTTP 503). Unhealthy is reserved for critical deps (e.g. database). When Redis fails, `RedisCacheService` falls back to in-process `IMemoryCache` and `IsRedisAvailable=false` so `redisStatus` stays **Degraded** even if the ping succeeds via memory.
- Development with `Redis:Enabled=false` uses in-process memory; the probe still reports `Healthy` when memory cache works.
- Production must set `Redis:Enabled=true` and `Redis:ConnectionString` (see `appsettings.Production.example.json`).

## Performance

- DB probe capped at **2 seconds** (`DatabaseHealthCheck.TimeoutMilliseconds`).
- Cache/Redis probe capped at **1 second** (`RedisCacheHealthCheck.TimeoutMilliseconds`).
- Ready probe runs the `ready` tag: **database**, **tse-fiscal-config**, **finanzonline**, **cache**.
- Full `/api/health` runs `deps` tag (database, tse, ntp, cache) — not backup/elmah mode checks.
- Liveness endpoints remain allocation-light for Kubernetes / LB spam.

## Ops notes

- Orchestrators: use `/health/live` (or `/health`) for liveness and `/health/ready` for readiness.
- Production misconfiguration (Soft TSE, FON simulation) fails **startup** (`ValidateOnStart`) and keeps ready **Unhealthy**.
- Do **not** fail the load balancer solely because `redisStatus` is `Degraded` — treat it as an alert signal; domain cache may fall back to memory.
- Monitoring dashboards: prefer `/api/health` JSON `entries.*.status` for device TSE/NTP visibility without failing the process when fiscal deps are degraded; use `redisStatus` / `entries.cache` for Redis posture.
