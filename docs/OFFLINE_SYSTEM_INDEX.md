# Offline System — Documentation Index

> **Last updated:** 2026-06-27  
> **Status:** Production-ready rollout (order snapshots + monitoring + FA ops surfaces).  
> **Rule:** Two independent offline pipelines — never merge APIs, tables, or admin nav.

---

## Quick reference

| System | DB table | POS API | Admin API | FA UI |
|--------|----------|---------|-----------|-------|
| **New — full order snapshots** | `offline_orders` | `/api/pos/offline-orders/*` | `/api/admin/offline-orders/*` | `/rksv/offline-orders` |
| **Legacy — TSE payment intents** | `offline_transactions` | `POST /api/offline-transactions/replay` | `/api/admin/offline-transactions/*` | `/admin/tse/offline-transactions` |
| **Monitoring (both)** | — | — | `/api/admin/offline-monitoring/*` | Dashboard widget + RKSV hub |

**Agent modules:** [`ai/modules/offline_orders.md`](../ai/modules/offline_orders.md), [`ai/modules/offline_transactions_legacy.md`](../ai/modules/offline_transactions_legacy.md)

---

## Delivery waves (2026-06-27)

### Wave 1 — Order snapshots (core)

| Layer | Deliverables |
|-------|----------------|
| **Database** | `offline_orders` — migration `20260627002059_AddOfflineOrdersTable` |
| **Backend** | `OfflineOrderService`, `SequenceReservationService`, POS + Admin controllers, cleanup hosted service |
| **POS** | `OfflineOrderManager`, `OfflineBanner`, `offlineConfig.ts`, local storage, reconnect sync |
| **FA** | `/rksv/offline-orders` (list, filter, replay, replay-all), Orval client |
| **FA settings** | `/settings/offline` UI (requires backend `GET/PUT /api/admin/settings/offline` when deployed) |

### Wave 2 — Monitoring, alerting, dashboard

| Layer | Deliverables |
|-------|----------------|
| **Backend** | `OfflineMonitoringService`, `AdminOfflineMonitoringController`, `OfflineAlertRules`, `OfflineMonitoringOptions` |
| **Alerting** | `OfflineAlertService` → activity feed (`OfflineOrdersBacklogGrowing`, `OfflineOrdersExpiringSoon`, `OfflineSyncStalled`) |
| **Dashboard** | Widget id `offline-system-status` in `DashboardWidgetCatalog` |
| **FA** | `OfflineStatusWidget`, `useOfflineMonitoring`, i18n `dashboard.offlineStatusWidget.*` |
| **Tests** | `OfflineMonitoringServiceTests`, `OfflineAlertServiceTests`, `offlineMonitoringApi.test.ts` |

### Wave 3 — QA & operations docs

| Document | Purpose |
|----------|---------|
| [`OFFLINE_SYSTEM_TEST_PLAN.md`](OFFLINE_SYSTEM_TEST_PLAN.md) | E2E scenarios, API tests, RKSV, FA, legacy regression |
| [`OFFLINE_MANUAL_TEST_CHECKLIST.md`](OFFLINE_MANUAL_TEST_CHECKLIST.md) | Checkbox QA for operators |
| [`OFFLINE_PRODUCTION_DEPLOYMENT.md`](OFFLINE_PRODUCTION_DEPLOYMENT.md) | Pre-deploy, deploy steps, verify gate, Day 1 / Week 1 monitoring, rollback |
| [`scripts/test-offline-system.mjs`](../scripts/test-offline-system.mjs) | Structural smoke (Windows-friendly) |
| [`scripts/test-offline-system.sh`](../scripts/test-offline-system.sh) | Same smoke for bash CI |

**Engineering changelog:** [`CHANGELOG_RECENT.md`](CHANGELOG_RECENT.md) — sections 2026-06-27.

---

## Architecture & design

| Document | Content |
|----------|---------|
| [`release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md) | **Start here** — two pipelines, do-not-merge rules |
| [`release/OFFLINE_ORDERS_FULL_SNAPSHOT.md`](release/OFFLINE_ORDERS_FULL_SNAPSHOT.md) | Order snapshot design, replay, BelegNr reservation |
| [`release/POS_OFFLINE_QUEUE_UX.md`](release/POS_OFFLINE_QUEUE_UX.md) | POS banner UX (German copy: **OFFLINE-MODUS**) |
| [`release/OFFLINE_REPLAY_BATCH_CORRELATION.md`](release/OFFLINE_REPLAY_BATCH_CORRELATION.md) | Legacy batch correlation |
| [`release/OFFLINE_REPLAY_ADVISORY_LOCK_TIMEOUT.md`](release/OFFLINE_REPLAY_ADVISORY_LOCK_TIMEOUT.md) | Legacy replay locking |
| [`release/OFFLINE_PAYLOAD_HASH_LEGACY.md`](release/OFFLINE_PAYLOAD_HASH_LEGACY.md) | Legacy payload hash |
| [`release/OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md`](release/OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md) | Legacy structural fallback |

---

## API summary

### Monitoring (`payment.view`)

| Method | Path |
|--------|------|
| `GET` | `/api/admin/offline-monitoring/status` |
| `GET` | `/api/admin/offline-monitoring/orders/stats` |
| `GET` | `/api/admin/offline-monitoring/transactions/stats` |
| `GET` | `/api/admin/offline-monitoring/anomalies` |
| `GET` | `/api/admin/offline-monitoring/sync-health` |

DTO shapes: `KasseAPI_Final.Services.Billing` — `OfflineSystemStatus`, `OfflineOrderStats`, `OfflineAnomaly`, `SyncHealth` in `BillingDtos.cs`.

### Configuration (`appsettings`)

| Section | Purpose |
|---------|---------|
| `Tse` | RKSV offline cap (50), auto-replay, degraded thresholds |
| `OfflineMonitoring` | Queue thresholds, expiry warnings, stalled sync hours |
| `OfflineAlertRules` | Alert thresholds + `CheckIntervalSeconds` |
| `OfflineReplay` | Legacy advisory lock tuning |

Example values: [`backend/appsettings.example.json`](../backend/appsettings.example.json).

---

## Frontend Admin surfaces

| Route | Permission | Feature |
|-------|------------|---------|
| `/rksv/offline-orders` | `payment.view` | Pending/synced/failed orders, replay |
| `/settings/offline` | `settings.manage` | Tenant offline limits toggles (UI; API optional) |
| `/admin/tse/offline-transactions` | `payment.view` | Legacy TSE intent queue |
| Dashboard widget `offline-system-status` | `payment.view` | Pending counts, sync health, link to RKSV page |

---

## POS configuration

Source: [`frontend/constants/offlineConfig.ts`](../frontend/constants/offlineConfig.ts)

| Key | Default | Notes |
|-----|---------|-------|
| `MAX_OFFLINE_TRANSACTIONS` | 50 | RKSV hard cap |
| `MAX_OFFLINE_ORDERS` | 100 | Order snapshot queue |
| `OFFLINE_EXPIRY_HOURS` | 72 | Matches server cleanup |
| `TOKEN_EXPIRY_HOURS` | 168 | 7 days offline session |
| `ENABLE_OFFLINE_GUTSCHEIN` | false | Must stay false |

Env: `EXPO_PUBLIC_API_BASE_URL` at build time.

---

## Tests

```bash
# Backend
cd backend && dotnet test --filter "OfflineMonitoringServiceTests|OfflineAlertServiceTests|SequenceReservationServiceTests"

# Frontend Admin
cd frontend-admin && npm run test -- src/features/offline/api/__tests__/offlineMonitoringApi.test.ts

# Structural smoke
node scripts/test-offline-system.mjs
```

---

## Known gaps / follow-ups

| Item | Status |
|------|--------|
| `GET/PUT /api/admin/settings/offline` | FA UI ready; backend endpoint may return 404 until implemented |
| OpenAPI / Orval for `/api/admin/offline-monitoring/*` | FA uses `customInstance` directly |
| `OfflineAlertRules.MaxSyncRetries` (5) vs order replay max (3) | Monitoring-only unless aligned in `OfflineOrderService` |
| Dashboard widget | Users with saved preferences may need to enable `offline-system-status` |

---

## Related repo rules

- [`AGENTS.md`](../AGENTS.md) — Directory hints, offline two-system table, fiscal guardrails
- [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) — § offline behavior
- [`ai/03_API_CONTRACT.md`](../ai/03_API_CONTRACT.md) — API boundaries
- [`ai/02_DATABASE_CONTRACT.md`](../ai/02_DATABASE_CONTRACT.md) — schema conventions
