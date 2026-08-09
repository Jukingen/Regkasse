# Production Deployment Runbook — Regkasse API (Linux host)

**Last updated:** 2026-08-08  
**Audience:** Ops / Super Admin deploying to `api.regkasse.at`  
**Keep Development local in Fake / Soft TSE** — production is a separate host + `ASPNETCORE_ENVIRONMENT=Production`.

| Related | Link |
|---------|------|
| Full deploy map | [`../DEPLOYMENT.md`](../DEPLOYMENT.md) |
| Release notes (2026-08-08) | [`RELEASE_NOTES_2026-08-08.md`](RELEASE_NOTES_2026-08-08.md) |
| Go-live checklist | [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) |
| Smoke tests | [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md) |
| Backup Fake vs PgDump | [`BACKUP_SYSTEM.md`](BACKUP_SYSTEM.md) § Understanding `"no real pg_dump"` |
| TSE cutover | [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) |
| FON cutover | [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) |
| Config template | [`../backend/appsettings.Production.example.json`](../backend/appsettings.Production.example.json) |
| Scripts | [`../scripts/ops/preflight-production.sh`](../scripts/ops/preflight-production.sh), [`../scripts/ops/deploy-production.sh`](../scripts/ops/deploy-production.sh) |

```text
POS:   https://pos.regkasse.at
FA:    https://admin.regkasse.at
API:   https://api.regkasse.at
```

---

## Development vs Production (hard separation)

| | Development (laptop) | Production (server) |
|--|----------------------|---------------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Production` |
| Backup adapter | `Fake` (expected `"no real pg_dump"`) | `PgDump` |
| TSE | Soft / Demo OK for local | `Tse:TseMode=Device`, `Mode=Real` |
| FinanzOnline | Simulation OK | all `UseSimulation=false`, `RksvSubmission:ClientKind=Real` |
| 2FA / CSRF | Dev bypass allowed | Enabled, no bypass |
| Secrets | user-secrets | `appsettings.Production.json` **or** systemd `EnvironmentFile` (never commit) |

Local real-dump experiment only: `scripts/test-real-backup.ps1` → then `scripts/revert-backup-fake.ps1`.

---

## 1. Pre-deployment checks (on the server)

```bash
cd /path/to/Regkasse   # git checkout with scripts/
./scripts/ops/preflight-production.sh
```

Manual equivalents:

```bash
psql --version
which pg_dump && pg_dump --version
df -h /var/backups
echo "$ASPNETCORE_ENVIRONMENT"   # or: systemctl show -p Environment regkasse-api
```

Create dirs if missing:

```bash
sudo mkdir -p /var/backups/regkasse/staging /var/backups/regkasse/archive
sudo chown -R regkasse:regkasse /var/backups/regkasse   # use your API service user
```

---

## 2. Production configuration (do not invent keys)

**Do not** put `ASPNETCORE_ENVIRONMENT` inside JSON — set it on the process (systemd / Docker).

Copy from the **tracked template**, then fill secrets offline:

```bash
cp backend/appsettings.Production.example.json /secure/store/appsettings.Production.json
# edit secrets in the secure store, then install next to the publish output
```

### Required Backup block (canonical keys)

```json
"Backup": {
  "ExecutionAdapterKind": "PgDump",
  "PgDumpExecutablePath": "/usr/bin/pg_dump",
  "VerifyLogicalDumpFileOnDisk": true,
  "PgDumpTimeoutSeconds": 7200,
  "LogicalDumpConnectionStringName": "DefaultConnection",
  "ArtifactStagingRoot": "/var/backups/regkasse/staging",
  "ExternalArchiveRoot": "/var/backups/regkasse/archive",
  "ExternalArchiveMutableTargetAccepted": true,
  "AcknowledgePhase1NoRealBackup": false,
  "AcknowledgeFakeBackupAdapterOutsideDevelopment": false,
  "ScheduledBackupEnabled": true,
  "ScheduledBackupCron": "0 2 * * *",
  "RetentionPolicyMode": "ReportOnly",
  "ArtifactRetentionDays": 30,
  "WorkerEnabled": true
}
```

Notes vs common mistakes:

| Wrong / outdated | Correct |
|------------------|---------|
| `Backup:Enabled` | `Backup:WorkerEnabled` (+ `ScheduledBackupEnabled`) |
| `Backup:RetentionDays` | `ArtifactRetentionDays` + `RetentionPolicyMode` |
| `FinanzOnline:UseSimulation` only | Also `Session` / `Registrierkassen` / `TransmissionQuery` + `RksvSubmission` |
| Soft TSE in Production | Startup / `/api/health/ready` fail-closed |

Jwt, ConnectionStrings, Fiskaly ApiKey/Secret, SMTP, license PEM paths: prefer **env** (`ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`, …) over committing into the publish tree.

---

## 3. Deploy steps (scripted)

```bash
# On build host or server with SDK + this repo checkout:
export REGKASSE_DEPLOY_CONFIRM=YES
export CONNECTION_STRING='Host=…;Database=kasse_prod;Username=…;Password=…'
export REGKASSE_ROOT=/var/www/regkasse
export API_PUBLISH_DIR=/var/www/regkasse/api
export SYSTEMD_UNIT=regkasse-api
export API_BASE=https://api.regkasse.at

sudo -E ./scripts/ops/deploy-production.sh
```

What the script does:

1. `preflight-production.sh`
2. `prepare-rollback-backup.sh` (archives previous release; excludes secrets)
3. `dotnet publish` → `API_PUBLISH_DIR` (restores stashed `appsettings.Production.json` if present)
4. `dotnet ef database update`
5. `systemctl restart regkasse-api`
6. `scripts/smoke-test.sh` against `API_BASE`

Skip flags: `SKIP_PREFLIGHT`, `SKIP_BUILD`, `SKIP_MIGRATE`, `SKIP_RESTART`, `SKIP_SMOKE`, `SKIP_ROLLBACK_ARCHIVE`.

---

## 4. Post-deployment verification

```bash
curl -fsS https://api.regkasse.at/api/health/live
curl -fsS https://api.regkasse.at/api/health/ready

# With Super Admin / ops JWT:
curl -fsS -H "Authorization: Bearer $TOKEN" \
  https://api.regkasse.at/api/admin/rksv/dep-export/status

curl -fsS -H "Authorization: Bearer $TOKEN" \
  https://api.regkasse.at/api/rksv/environment
# Expect: production / not simulation for fiscal go-live
```

Trigger a **System** backup from FA (`/backup`) or admin API and confirm the artifact manifest is **not** Fake / `"no real pg_dump"`.

Full smoke (login + optional DEP):

```bash
export API_BASE=https://api.regkasse.at
export TENANT_ID=… LOGIN_IDENTIFIER=… LOGIN_PASSWORD=…
export REQUIRE_READY=1
./scripts/smoke-test.sh
```

---

## 5. Monitoring (minimum)

| Check | Interval | Target |
|-------|----------|--------|
| Uptime | 1 min | `GET /api/health/live` |
| Ready / fiscal posture | 5 min | `GET /api/health/ready` |
| Backup success | daily | FA `/backup` or last succeeded System run |
| Disk | hourly | `/var/backups` free space |
| Metrics | scrape | `/metrics` (see [`MONITORING.md`](MONITORING.md)) |

Configure Slack/email on backup failure (`Backup:FailureAlertEmailRecipients` / existing alert webhooks).

---

## 6. Rollback

```bash
sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh
# or named stamp:
sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh 20260807-120000
```

- Restores **application packages** only — **not** PostgreSQL / EF migrations.
- DB PITR is a separate DR procedure ([`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)).

---

## 7. Server details still needed from you

Fill before first cutover (ticket / password manager — do not commit):

| Item | Value |
|------|--------|
| SSH host / jump | |
| OS / arch | |
| Deploy path (`REGKASSE_ROOT`) | default `/var/www/regkasse` |
| systemd unit name | default `regkasse-api` |
| PostgreSQL version + `pg_dump` path | |
| DB name / backup role | |
| TLS terminator (nginx/caddy) | |
| Fiskaly / FON credentials location | |
| On-call webhook | |

Until those are known, run **preflight** on the target box and keep fiscal modes in Production config locked to Device/Real.

---

## Cache Management (Super Admin)

Domain cache (license status, product lists, permissions, tenant settings, etc.) is served by `ICacheService` (Redis in Production; in-process memory in Development). **Prefer automatic invalidation after writes.** Manual clear is for emergencies only (see below).

**Auth:** Super Admin role + `system.critical`  
**Audit:** `SYSTEM_CACHE_CLEARED`  
**FA UI:** Systemwartung → **Cache leeren** (full flush of tracked keys)  
**Related:** [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md) (license-only clear), [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md) § Cache Settings

### Tenant-specific clear

Drops that tenant’s domain keys (license status, product list prefix, tenant settings):

```bash
curl -X POST "https://api.regkasse.at/api/admin/cache/clear" \
  -H "Authorization: Bearer $SUPERADMIN_JWT" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"<uuid>"}'
```

Example with a concrete tenant id:

```bash
curl -X POST "https://api.regkasse.at/api/admin/cache/clear" \
  -H "Authorization: Bearer <super-admin-token>" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"123e4567-e89b-12d3-a456-426614174000"}'
```

License-status only (same JWT), if FA shows a stale “License not found” after a sale:

```bash
curl -X POST "https://api.regkasse.at/api/admin/license/cache/clear" \
  -H "Authorization: Bearer $SUPERADMIN_JWT" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"<uuid>"}'
# or: {"tenantSlug":"<slug>"}
```

Optional prefix clear: `{"prefix":"product_list_"}` (removes all keys starting with that prefix).

### Full cache flush

```bash
curl -X POST "https://api.regkasse.at/api/admin/cache/clear" \
  -H "Authorization: Bearer $SUPERADMIN_JWT" \
  -H "Content-Type: application/json" \
  -d '{"clearAll":true}'
```

> **Warning:** Clear all cache only when absolutely necessary; it will impact performance temporarily (cold Cache-Aside refill on the next license/product/permission reads across tenants). Prefer `tenantId` (or license-only clear) whenever the inconsistency is scoped to one mandant.

### When to use manual clearing

| Situation | Action |
|-----------|--------|
| Confirmed **data inconsistency** (stale license/product after failed deploy, Redis blip, or missed invalidation) for **one** tenant | `POST …/cache/clear` with `{"tenantId":"…"}` |
| After **database migrations** or **manual DB fixes** that reshape cached snapshots (rare; prefer deploy + natural TTL first) | Prefer `tenantId` clear for affected mandants; `clearAll` only if impact is global |
| Investigating suspected cache-related bugs (ops / support) | Tenant or prefix clear; capture `redisStatus` from `/api/health/ready` before/after |
| Stale license status only | `POST …/license/cache/clear` |
| Broad Redis corruption / unknown bad keys / emergency data inconsistency | `{"clearAll":true}` — last resort |
| Routine deploys / normal SaaS traffic | **Do not** clear — rely on event invalidation + TTLs |

Also:

- Check `GET /api/health/ready` → `redisStatus` / `entries.cache` (Healthy or Degraded) before/after  
- Do **not** restart the API solely to flush domain cache  
- Clearing backend cache does **not** reset Admin React Query caches in open browsers (operators may need a hard refresh or `queryClient.invalidateQueries()` — see `frontend-admin/README.md`)

## Checklist snapshot

| Item | Status |
|------|--------|
| Code + DEP simulation metadata | Ready |
| Production.example Backup=PgDump | Ready |
| Deploy / preflight scripts | Ready |
| Local Dev stays Fake | Ready |
| DNS / TLS on prod host | Ops |
| systemd EnvironmentFile secrets | Ops |
| TSE Device + FON Real cutover | Ops + compliance |
| First real System backup verified | Ops |
| Monitoring / on-call | Ops |
