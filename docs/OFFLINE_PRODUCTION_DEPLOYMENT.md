# Offline System — Production Deployment Checklist

> **Last updated:** 2026-06-27  
> **Index:** [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md)  
> **Scope:** Production rollout for **both** offline pipelines (order snapshots + legacy TSE intents).  
> **Do not merge:** [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md)

| System | Table | Admin UI | POS sync API |
|--------|-------|----------|--------------|
| **New — full order snapshots** | `offline_orders` | `/rksv/offline-orders` | `/api/pos/offline-orders/*` |
| **Legacy — TSE payment intents** | `offline_transactions` | `/admin/tse/offline-transactions` | `POST /api/offline-transactions/replay` |

**Recommended release window:** deploy **backend**, **frontend-admin**, and **POS app build** together when offline monitoring, dashboard widget, or replay logic changed. See [`docs/ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md).

---

## Pre-Deployment

### Backend — database

- [ ] Migration `20260627002059_AddOfflineOrdersTable` applied (`offline_orders` table + indexes)
- [ ] Migration `20260624150842_AddBillingBackupHistory` applied (`billing_backup_history` table) — license billing backup; ship with same release if billing backup is in scope
- [ ] Verify indexes exist: `idx_offline_orders_tenant`, `idx_offline_orders_status`, `idx_offline_orders_expires`, `idx_offline_orders_created`
- [ ] Staging smoke: `dotnet ef database update` against a copy of production schema (no drift)

### Backend — configuration (`appsettings.Production.json`)

Copy defaults from [`backend/appsettings.example.json`](../backend/appsettings.example.json). Secrets via environment variables or secret store — **never** commit production credentials.

- [ ] `ConnectionStrings:DefaultConnection` — PostgreSQL (SSL, pooling, backup policy aligned with ops)
- [ ] `JwtSettings` — issuer, audience, secret (min 32 chars), `ExpirationHours`
- [ ] **`Tse`** section:
  - [ ] `OfflineModeEnabled: true`
  - [ ] `MaxOfflineTransactionsPerCashRegister: 50` (RKSV cap; do not exceed 50 in production)
  - [ ] `AutoReplayIntervalSeconds`, `OfflineAfterConsecutiveFailures`, `DegradedAfterConsecutiveFailures`
- [ ] **`Fiskaly`** — production API key/secret, TSE serial, signing cert material, `ApiBaseUrl: https://api.fiskaly.com/v1`
- [ ] **`NtpSettings`** — `Enabled: true`, `DevelopmentBypass: false`, `MaxAllowedOffsetSeconds` (block fiscal online payments when clock drift exceeds limit)
- [ ] **`OfflineReplay`** — `MaxLockWaitMs`, `LockRetryIntervalMs` (legacy TSE replay advisory lock)
- [ ] **`OfflineMonitoring`** — queue thresholds, expiry warning hours, stalled sync hours, TSE cap warning percent
- [ ] **`OfflineAlertRules`** — alert thresholds and background check interval:

```json
"OfflineAlertRules": {
  "MaxPendingOrders": 50,
  "MaxPendingAgeHours": 48,
  "MaxSyncRetries": 5,
  "MinSyncSuccessRate": 80,
  "CheckIntervalSeconds": 300
}
```

- [ ] **`BillingBackup`** (if enabled for this release):

```json
"BillingBackup": {
  "Enabled": true,
  "BasePath": "/var/regkasse/billing-backups",
  "RetentionYears": 7,
  "BackupOnSaleCreation": true,
  "SendPdfViaEmail": false,
  "EmailRecipients": "",
  "DailyBackupHourUtc": 2
}
```

- [ ] `Email:Smtp` — required if billing backup email or activity email channels are enabled
- [ ] `OfflineVoucherEncryption:EncryptionKeyBase64` — only if voucher-at-rest encryption is used (voucher **payments** remain blocked offline)

### Backend — hosted services (auto-start with API)

Confirm these are registered in `ApplicationHost.cs` and start without errors in staging logs:

- [ ] `OfflineAlertService` — tenant anomaly sweep → activity feed (critical)
- [ ] `OfflineOrderCleanupHostedService` — deletes expired pending `offline_orders` (every 6 h)
- [ ] `OfflineReplayHostedService` — legacy TSE intent background replay
- [ ] `BillingBackupHostedService` — if `BillingBackup:Enabled`

### Frontend (POS)

Production build must bake env at **compile time** (EAS / CI). See [`frontend/.env.example`](../frontend/.env.example).

- [ ] `EXPO_PUBLIC_API_BASE_URL` → `https://{tenant-slug}.regkasse.at/api` (or tenant-specific production API URL)
- [ ] `OFFLINE_CONFIG` defaults reviewed in [`frontend/constants/offlineConfig.ts`](../frontend/constants/offlineConfig.ts):
  - [ ] `TOKEN_EXPIRY_HOURS: 168` (7 days recommended for offline sessions)
  - [ ] `MAX_OFFLINE_ORDERS: 50–100` (align with tenant policy; backend RKSV TSE cap remains 50 for **transactions**)
  - [ ] `MAX_OFFLINE_TRANSACTIONS: 50` (RKSV limit — do not raise above 50)
  - [ ] `OFFLINE_EXPIRY_HOURS: 72` (matches server cleanup / RKSV guidance)
  - [ ] `ENABLE_OFFLINE_GUTSCHEIN: false` (voucher offline must stay disabled)
  - [ ] `SYNC_ENDPOINTS.ORDERS` → `/api/pos/offline-orders/replay`
  - [ ] `SYNC_ENDPOINTS.PAYMENTS` → `/api/offline-transactions/replay`
- [ ] POS APK/IPA signed with production certificate; SSL/TLS to API valid (no self-signed in prod)
- [ ] NTP/time sync on device OS enabled (POS blocks online fiscal flow when server reports clock drift)

### Frontend (Admin — FA)

Build-time env: [`frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`](../frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md).

- [ ] `NEXT_PUBLIC_API_BASE_URL` → production API origin (e.g. `https://admin.regkasse.at` or shared API host)
- [ ] `NEXT_PUBLIC_RKSV_ENVIRONMENT=PRODUCTION` (or org-standard value) set **before** `npm run build`
- [ ] Wildcard DNS + TLS for `*.regkasse.at` and `admin.regkasse.at`
- [ ] **Offline settings** page: `/settings/offline` — requires `settings.manage` (Manager / tenant admin; not Super Admin only)
- [ ] **Offline orders** page: `/rksv/offline-orders` — requires `payment.view`
- [ ] **Legacy TSE queue** (if still used): `/admin/tse/offline-transactions` — requires `payment.view`
- [ ] **Dashboard widget** `offline-system-status` — visible to users with `payment.view`; enable in dashboard layout if using saved widget preferences
- [ ] Verify `/api/admin/settings/offline` responds on staging (GET/PUT). If **404**, FA save will fail — POS still uses `frontend/constants/offlineConfig.ts` defaults until backend settings API is deployed

### Monitoring & alerting

- [ ] Structured logging level `Information` (or higher) for offline services in production log sink
- [ ] `OfflineAlertRules` tuned for tenant volume (pending count, age, success rate)
- [ ] Activity feed pipeline verified (critical anomalies → `OfflineOrdersBacklogGrowing`, `OfflineOrdersExpiringSoon`, `OfflineSyncStalled`)
- [ ] Optional email/webhook recipients configured for critical activity events (tenant notification settings)
- [ ] Dashboard **Offline System** widget loads `/api/admin/offline-monitoring/status` + `/sync-health` without 404
- [ ] On-call runbook linked: [`docs/OFFLINE_MANUAL_TEST_CHECKLIST.md`](OFFLINE_MANUAL_TEST_CHECKLIST.md), [`docs/OFFLINE_SYSTEM_TEST_PLAN.md`](OFFLINE_SYSTEM_TEST_PLAN.md)

---

## Deployment Steps

### 1. Backend

```bash
cd backend

# Apply migrations (staging first, then production)
dotnet ef database update --project KasseAPI_Final.csproj

# Release build
dotnet publish KasseAPI_Final.csproj -c Release -o ./publish

# Deploy publish output to server (rsync, CI artifact, etc.)
# Restart API process — use your host service name, e.g.:
#   systemctl restart regkasse-api
#   kubectl rollout restart deployment/regkasse-api
```

**Post-restart log checks (first 5 minutes):**

- [ ] No EF migration errors on startup
- [ ] `OfflineAlertService` started
- [ ] `OfflineOrderCleanupHostedService` started
- [ ] `OfflineReplayHostedService` started (legacy)
- [ ] Fiskaly/TSE health check succeeds for at least one pilot register

### 2. Frontend (POS)

Set production API URL **before** build (inlined at compile time). See [`frontend/.env.example`](../frontend/.env.example).

```bash
cd frontend

export EXPO_PUBLIC_API_BASE_URL=https://{slug}.regkasse.at/api

npm ci

# Mobile — production binary (EAS; see frontend/eas.json)
npx eas build --platform android --profile production
# npx eas build --platform ios --profile production
# Deploy APK/IPA to registers or app store / internal distribution

# Web — static export (if hosting POS as web)
npx expo export -p web
# Deploy dist/ output to web server (nginx, CDN, etc.)
```

> **Note:** `frontend/package.json` has no `npm run build` script. Production POS uses **EAS** (mobile) or **`npx expo export -p web`** (web). Do not ship a dev `expo start` bundle to production.

- [ ] `EXPO_PUBLIC_API_BASE_URL` points to tenant production API (`https://{slug}.regkasse.at/api`)
- [ ] Roll out to pilot registers first (1–2 devices)
- [ ] Confirm app version recorded in release notes / support channel

### 3. Frontend Admin (FA)

Build-time env must be set **before** `npm run build`. See [`frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`](../frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md).

```bash
cd frontend-admin

export NEXT_PUBLIC_API_BASE_URL=https://{slug}.regkasse.at
export NEXT_PUBLIC_RKSV_ENVIRONMENT=PRODUCTION

npm ci
npm run build

# Deploy build output to server; restart FA process
npm run start
# Or serve .next/ via your hosting stack (standalone, PM2, container, etc.)
```

Deploy **after** backend (or in the same maintenance window). FA before backend briefly shows 404 on new monitoring routes.

### 4. Verify (release gate)

Complete these checks on **staging first**, then repeat on production after deploy.

#### API

- [ ] **Health check passes**

```bash
curl -sS -o /dev/null -w "%{http_code}\n" \
  "https://{slug}.regkasse.at/api/health"
# Expected: 200
```

- [ ] **Offline orders API responds** (requires Manager JWT with `payment.view`)

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-orders?status=pending&page=1&pageSize=20"
# Expected: HTTP 200 + JSON list (items may be empty)

curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/status"
# Expected: HTTP 200 + status payload
```

Optional POS endpoint (cashier JWT with `payment.take`):

```bash
curl -sS -H "Authorization: Bearer $CASHIER_TOKEN" \
  "https://{slug}.regkasse.at/api/pos/offline-orders/pending"
# Expected: HTTP 200
```

#### Frontend Admin (browser)

Log in as **Manager** on `https://{slug}.regkasse.at` (production uses subdomain tenant resolution — no `X-Tenant-Id` header).

- [ ] **Offline settings page loads** — `https://{slug}.regkasse.at/settings/offline`  
  Requires `settings.manage`. Form renders; save succeeds if `GET/PUT /api/admin/settings/offline` is deployed (404 on save = backend gap — log defect; POS defaults still apply).

- [ ] **Offline orders page loads** — `https://{slug}.regkasse.at/rksv/offline-orders`  
  Requires `payment.view`. Table loads without 403; Network tab shows `GET /api/admin/offline-orders` → 200.

- [ ] **Dashboard offline widget** (optional) — enable widget `offline-system-status` if using saved layout; loads `/api/admin/offline-monitoring/status` + `/sync-health`.

#### POS pilot (one register)

- [ ] Online checkout succeeds with TSE signature
- [ ] Simulate offline → banner **OFFLINE-MODUS** / **Keine Internetverbindung**
- [ ] Offline cash order persists locally; sync on reconnect clears queue

### 5. Optional — OpenAPI alignment

If API routes changed in this release:

```bash
node scripts/generate-backend-openapi.mjs
cd frontend-admin && npm run generate:api
node scripts/validate-critical-openapi-paths.mjs
```

> Note: offline monitoring routes may still be consumed via `customInstance` until added to OpenAPI.

---

## Post-Deployment Verification

Use section **4. Verify (release gate)** above for the minimum gate. Additional checks below.

### Extended API smoke

```bash
# System status (requires payment.view JWT on tenant subdomain)
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/status" | jq .

curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/sync-health" | jq .

# Pending orders list
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-orders?status=pending&page=1&pageSize=20" | jq .
```

### Admin UI (extended)

- [ ] Dashboard → **Offline System** widget shows healthy / pending counts
- [ ] Legacy TSE queue `/admin/tse/offline-transactions` (if still in use)

### POS pilot (extended)

- [ ] Online checkout succeeds with TSE signature
- [ ] Simulate offline → banner **OFFLINE-MODUS** / **Keine Internetverbindung** (German copy)
- [ ] Create cash payment offline → order persisted locally
- [ ] Restore network → sync completes; pending count returns to 0; banner hides
- [ ] Voucher payment blocked offline (expected)

### Automated smoke (CI or ops workstation)

```bash
node scripts/test-offline-system.mjs
# Optional with live API:
# node scripts/test-offline-system.mjs --with-api --base-url https://{slug}.regkasse.at --token "$TOKEN"
```

### Legacy regression (if TSE offline intents still in use)

- [ ] `/admin/tse/offline-transactions` lists backlog
- [ ] Manual retry or auto replay produces fiscal `payment_details` row
- [ ] TSE offline cap warning at ~40/50 transactions (80%)

---

## Post-Deployment Monitoring

### Day 1 (first 24 hours)

Check every **2–4 hours** during business hours; once overnight.

#### Monitor offline order creation

- [ ] Dashboard widget **Offline System** — `totalPendingOrders` trend (should not grow unbounded)
- [ ] FA → `/rksv/offline-orders` — new rows appear with status `pending` when registers go offline
- [ ] API snapshot:

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/orders/stats" | jq .
```

- [ ] Server logs: no spike in `400`/`409` on `POST /api/pos/offline-orders` or replay errors

#### Monitor sync success rate

- [ ] Dashboard widget sync health ≥ `OfflineAlertRules.MinSyncSuccessRate` (default **80%**)
- [ ] API:

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/sync-health" | jq .
```

- [ ] Pending queue drains after connectivity returns on pilot registers (pending → `synced`)
- [ ] No sustained growth in `failed` status on `/rksv/offline-orders`

#### Check for anomalies

- [ ] API:

```bash
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/anomalies" | jq .
```

- [ ] Admin activity feed — no new critical offline events:
  - `OfflineOrdersBacklogGrowing`
  - `OfflineOrdersExpiringSoon`
  - `OfflineSyncStalled`
  - Legacy: `OfflineQueueGrowing` (TSE intent queue)
- [ ] `OfflineAlertService` logs — warnings acceptable; repeated **critical** entries need triage

#### Verify no data loss

- [ ] For each pilot register: POS pending count matches server pending count after sync cycle
- [ ] Every `synced` offline order has linked `payment_details` / receipt visible in POS history
- [ ] No unexpected rows in `expired` or `failed` without operator awareness
- [ ] Cleanup job log: `Offline order cleanup removed N expired row(s)` — investigate if **N > 0** on Day 1 (orders expiring too fast or clock skew)

**Day 1 exit criteria:** sync success rate stable, zero unresolved critical anomalies, pilot registers fully synced.

---

### Week 1 (days 2–7)

Daily check; deeper review on day 7.

#### Review offline usage statistics

- [ ] Compare `orders/stats` daily: `pending`, `synced`, `failed`, `expired`
- [ ] Identify registers with repeated offline usage (training vs connectivity issue)
- [ ] Legacy TSE queue depth on `/admin/tse/offline-transactions` (if enabled)
- [ ] Export or screenshot weekly totals for compliance / ops record

```bash
# Daily status + stats bundle
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/status" | jq .
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/orders/stats" | jq .
curl -sS -H "Authorization: Bearer $TOKEN" \
  "https://{slug}.regkasse.at/api/admin/offline-monitoring/transactions/stats" | jq .
```

#### Check alert logs

- [ ] Review `OfflineAlertService` / API logs for anomaly codes: `too_many_pending`, `old_pending`, `sync_failure`, `expired_pending`, `tse_cap_warning`, `tse_cap_reached`
- [ ] Activity feed notification history — acknowledge and document any critical offline alerts
- [ ] Tune `OfflineAlertRules` / `OfflineMonitoring` if false positives (document change in change log)

#### Verify backup system

- [ ] If `BillingBackup:Enabled` — confirm daily run succeeded (`billing_backup_history` status / admin backup UI)
- [ ] Tenant database backup includes `offline_orders` and `offline_transactions` tables (standard PG backup)
- [ ] Restore drill on **non-production** copy includes offline tables (optional Week 1 task)

#### User feedback collection

- [ ] Cashiers: offline banner clarity, sync timing, blocked voucher behavior
- [ ] Managers: FA offline orders page usability, dashboard widget accuracy
- [ ] Support channel: ticket tag `offline-rollout` — track issues separately
- [ ] Document findings; open defects for P1/P2 before full fleet rollout

**Week 1 exit criteria:** statistics stable, alerts understood/tuned, backups verified, no open P1 defects, ready for wider POS rollout.

---

## Rollback Plan

### If critical issues found (emergency mitigation)

Use when fiscal integrity, data loss, or widespread sync failure is suspected. **Drain or replay existing queues before disabling** where possible.

#### 1. Disable new offline order snapshots (POS)

POS reads flags from [`frontend/constants/offlineConfig.ts`](../frontend/constants/offlineConfig.ts) at build time (not from backend today):

```typescript
ENABLE_OFFLINE_ORDERS: false,
ENABLE_OFFLINE_PAYMENTS: false,  // legacy non-fiscal TSE queue on device
```

- [ ] Set flags → rebuild POS (`eas build` / `expo export`) → redeploy to affected registers  
- [ ] **Or** when `/api/admin/settings/offline` is deployed and POS consumes it: FA → `/settings/offline` → disable toggles → push config (verify POS picks up server settings before relying on this path)

Existing local queues remain on devices until cleared or synced — instruct registers to **stay online** until pending = 0.

#### 2. Disable legacy TSE offline mode (backend)

In `appsettings.Production.json`:

```json
"Tse": {
  "OfflineModeEnabled": false
}
```

- [ ] Apply config change
- [ ] **Restart backend** (API process / container / `systemctl restart …`)

This stops new non-fiscal TSE offline intents server-side; does not remove rows already in `offline_transactions` or `offline_orders`.

#### 3. Rollback frontend to previous version

```bash
# Frontend Admin — redeploy previous build artifact
cd frontend-admin
npm run build   # from previous git tag / CI artifact
# Deploy prior .next output; restart FA process

# POS — reinstall previous APK/IPA or web export on registers
```

Deploy order: **backend config first** (if TSE offline disabled) → **drain queues** → **POS rollback** → **FA rollback** (optional; FA rollback alone does not stop offline creation).

#### 4. Do not (without compliance sign-off)

- Truncate `offline_orders` or `offline_transactions`
- Delete pending rows while devices may still replay
- Roll back database migrations that removed columns

---

### Component rollback reference

| Component | Action | Risk |
|-----------|--------|------|
| **Backend** | Redeploy previous API build; set `Tse:OfflineModeEnabled: false` for emergency stop | Old code may not understand new columns (usually safe if migrations are additive only) |
| **FA** | Redeploy previous admin build | Dashboard widget may 404 until API restored |
| **POS** | Reinstall previous APK/IPA; or set `ENABLE_OFFLINE_ORDERS=false` and rebuild | Local offline queue format is versioned; avoid downgrading across storage schema changes |
| **Data** | Do not manually delete `offline_orders` in production unless support procedure | Fiscal audit trail |

If rollback is required **after** pending offline orders exist:

1. Keep new backend running until queues are empty **or** document manual replay procedure with finance/compliance sign-off (`/rksv/offline-orders` → replay-all / per-row retry).
2. Do not truncate `offline_orders` / `offline_transactions` without audit entry.
3. Log rollback decision: actor, time, reason, pending counts at rollback start.

---

## Production Guardrails (reminder)

- Voucher (Gutschein) payments: **never** offline — backend rejects; POS must not enqueue.
- `offline_orders` expiry: **72 h** default; expired pending rows are **hard-deleted** by cleanup (audit: `OFFLINE_ORDERS_CLEANUP`).
- Order replay max **3** sync attempts per row (`failed` status); `OfflineAlertRules.MaxSyncRetries` is monitoring-only unless aligned in code.
- TSE offline transaction cap: **50** per cash register (RKSV).
- Cross-tenant access returns **HTTP 404**, not 403.

---

## Related Documentation

| Document | Purpose |
|----------|---------|
| [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md) | Two pipelines — do not merge |
| [`docs/release/OFFLINE_ORDERS_FULL_SNAPSHOT.md`](release/OFFLINE_ORDERS_FULL_SNAPSHOT.md) | Order snapshot design |
| [`docs/OFFLINE_SYSTEM_TEST_PLAN.md`](OFFLINE_SYSTEM_TEST_PLAN.md) | E2E test scenarios |
| [`docs/OFFLINE_MANUAL_TEST_CHECKLIST.md`](OFFLINE_MANUAL_TEST_CHECKLIST.md) | Manual QA checklist |
| [`ai/modules/offline_orders.md`](../ai/modules/offline_orders.md) | Agent module — API + rules |
| [`ai/modules/offline_transactions_legacy.md`](../ai/modules/offline_transactions_legacy.md) | Legacy TSE intents |
| [`docs/ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md) | Coupled backend + FA releases |

---

**Sign-off**

| Role | Name | Date |
|------|------|------|
| Backend / API | | |
| Frontend Admin | | |
| POS / Mobile | | |
| Compliance / RKSV | | |
| Operations | | |
