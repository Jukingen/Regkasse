# Offline Orders — Full POS Order Snapshots

> **Status:** Implemented (2026-06-27)  
> **Scope:** Backend DB + services, POS local queue/sync, Admin RKSV panel, batch BelegNr reservation  
> **Not a replacement for:** Legacy `offline_transactions` (payment-intent replay / TSE backlog). Both models coexist.  
> **Separation (read first):** [`OFFLINE_SYSTEMS_SEPARATION.md`](OFFLINE_SYSTEMS_SEPARATION.md)

## Problem & design

Legacy offline flow stores **payment intents** (`offline_transactions`) and replays them via `POST /api/offline-transactions/replay`. The new flow stores **full order snapshots** (`offline_orders`) created when POS cannot reach the server during checkout — closer to “save entire cart + payment payload, sync later as one unit.”

| Aspect | Legacy (`offline_transactions`) | New (`offline_orders`) |
|--------|----------------------------------|-------------------------|
| Payload | Payment intent JSON | Full `CreatePaymentRequest` snapshot (`order_data` JSONB) |
| POS API | `/api/offline-transactions/replay` | `/api/pos/offline-orders/*` |
| Admin UI | `/admin/tse/offline-transactions` (Fiskal → TSE) | `/rksv/offline-orders` (RKSV → Diagnose) |
| Expiry | Policy varies by intent row | **72 h**; expired pending rows **deleted** (not marked expired) |
| Voucher | Encrypted / rejected in queue rules | **Rejected** — never queued offline |
| BelegNr on replay | Per-item allocation in replay service | **Batch reserve upfront** via `ISequenceReservationService` |

---

## Legacy Offline Transactions (Separate)

The legacy `offline_transactions` system at `/admin/tse/offline-transactions` remains separate:

- **Purpose:** TSE health monitoring and fiscal offline payments
- **Does NOT** handle orders or order sync
- **Do NOT merge** with `offline_orders`

Full dual-system reference: [`OFFLINE_SYSTEMS_SEPARATION.md`](OFFLINE_SYSTEMS_SEPARATION.md).

---

## Backend

### Database

**Migration:** `20260627002059_AddOfflineOrdersTable`

**Table:** `offline_orders` (tenant-scoped via `ITenantEntity`)

| Column | Type | Notes |
|--------|------|-------|
| `id` | uuid PK | |
| `tenant_id` | uuid FK → `tenants` | NOT NULL, indexed |
| `cash_register_id` | uuid FK → `cash_registers` | NOT NULL, indexed |
| `offline_order_id` | varchar(50) | Human id: `OFFLINE-{yyyyMMddHHmmss}-{4digitRandom}` |
| `order_data` | jsonb | Serialized `CreatePaymentRequest` |
| `order_total` | numeric(10,2) | |
| `payment_method` | varchar(50) | e.g. `cash`, `card` |
| `status` | varchar(20) | `pending`, `synced`, `failed`, `expired` (constants in `OfflineOrderStatuses`) |
| `synced_payment_id` | uuid FK → `payment_details` | SET NULL on delete |
| `synced_invoice_number` | varchar(50) | BelegNr after success |
| `sync_attempts` | int | Max **3** → `failed` |
| `last_sync_attempt_utc` | timestamptz | |
| `created_at_utc` | timestamptz | |
| `expires_at_utc` | timestamptz | `created_at_utc + 72h` |
| `synced_at_utc` | timestamptz | |
| `error_message` | text | Truncated on failure |

**Key files:** `Models/OfflineOrder.cs`, `Models/OfflineOrderStatuses.cs`, `Data/AppDbContext.cs`

### Services

| Service | Role |
|---------|------|
| `IOfflineOrderService` / `OfflineOrderService` | Save, list pending, replay, admin list, cleanup |
| `ISequenceReservationService` / `SequenceReservationService` | Batch BelegNr counter reservation for safe replay |
| `OfflineOrderCleanupHostedService` | Every **6 h** — deletes expired pending rows, audit `OFFLINE_ORDERS_CLEANUP` |

**Replay flow (`ReplayPendingOrdersAsync`):**

1. Load pending, non-expired orders for register (FIFO by `created_at_utc`).
2. **`ReserveSequencesAsync(count, cashRegisterId)`** — atomic block in `receipt_sequences`.
3. For each order: build BelegNr via `ToBelegNrAsync`, set `CreatePaymentRequest.ReservedReceiptNumber`, call `PaymentService.CreatePaymentAsync`.
4. On failure: **`ReleaseSequencesAsync`** for failed slots (tail rollback in `next_sequence`).
5. Update row status: `synced` | `pending` (retry) | `failed` (≥3 attempts or deterministic failure).

**Audit actions:** `OFFLINE_ORDER_CREATED`, `OFFLINE_ORDERS_CLEANUP`

**Payment integration:** `CreatePaymentRequest.ReservedReceiptNumber` skips in-transaction BelegNr allocation; conflict → `RECEIPT_NUMBER_CONFLICT`.

### Sequence reservation

**Interface:** `ISequenceReservationService`

| Method | Purpose |
|--------|---------|
| `ReserveSequencesAsync(count, cashRegisterId)` | Atomic UPSERT on `receipt_sequences`; returns consecutive int counters for UTC day |
| `ReleaseSequencesAsync(sequences, cashRegisterId)` | Rolls back unused **tail** slots (`next_sequence == seq + 1`) |
| `IsSequenceAvailableAsync(sequenceNumber, cashRegisterId)` | BelegNr not present in active `payment_details` |
| `ToBelegNrAsync(cashRegisterId, sequenceNumber)` | `AT-{RegisterNumber}-{yyyyMMdd}-{seq}` |
| `ReserveNextReceiptNumberAsync(...)` | Single-order path with conflict retry |

**Tests:** `KasseAPI_Final.Tests/SequenceReservationServiceTests.cs` (PostgreSQL collection)

### API endpoints

#### POS — `/api/pos/offline-orders`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| `POST` | `/api/pos/offline-orders` | `payment.take` | Save offline order snapshot |
| `GET` | `/api/pos/offline-orders/pending?cashRegisterId=` | `payment.take` | List pending for register |
| `POST` | `/api/pos/offline-orders/replay?cashRegisterId=` | `payment.take` | Replay all pending (batch sequence reserve) |
| `GET` | `/api/pos/offline-orders/{offlineOrderId}/status` | `payment.take` | Status by business id |

**Controller:** `PosOfflineOrdersController`

#### Admin — `/api/admin/offline-orders`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| `GET` | `/api/admin/offline-orders` | `payment.view` | Paginated list (`status`, `cashRegisterId`, `pageNumber`, `pageSize`) |
| `POST` | `/api/admin/offline-orders/{id}/replay` | `payment.view` | Replay single pending order |
| `POST` | `/api/admin/offline-orders/replay-all?cashRegisterId=` | `payment.view` | Replay all pending (optional register filter) |

**Controller:** `AdminOfflineOrdersController`  
Cross-tenant / missing tenant → **404**. Uses `ISettingsTenantResolver` like other admin ops.

---

## Frontend POS (`frontend/`)

| File | Role |
|------|------|
| `services/offline/offlineStorage.ts` | `OfflineOrder` type; AsyncStorage (native) / IndexedDB (web) |
| `services/offline/offlineOrderManager.ts` | Save, auto-sync (30 s), upload + replay via POS API |
| `services/payment/offlineOrderQueue.ts` | Legacy-compatible queue wrapper |
| `hooks/useOfflineOrderManager.ts` | Status polling (5 s), `saveOrder`, `syncNow`, `getPending` |
| `components/OfflineBanner.tsx` | German UI — expiry warning, manual sync |
| `app/(tabs)/_layout.tsx` | Banner integration |
| `hooks/useApiManager.ts` | Also syncs `offlineOrderQueue` on reconnect |

**UI language:** German (de-DE) only.  
**Voucher:** Must not be saved offline.

See also legacy queue UX: [`POS_OFFLINE_QUEUE_UX.md`](POS_OFFLINE_QUEUE_UX.md) (`pendingPaymentQueue` / `offline_transactions`).

---

## Frontend Admin (`frontend-admin/`)

| Item | Value |
|------|-------|
| Route | `/rksv/offline-orders` |
| Permission | `payment.view` (Manager + Super Admin) |
| Menu | RKSV → Diagnose → **Offline-Bestellungen** |
| Hooks | Orval: `useGetApiAdminOfflineOrders`, `usePostApiAdminOfflineOrdersIdReplay`, `usePostApiAdminOfflineOrdersReplayAll` (`src/api/generated/admin/admin.ts`) |
| i18n | `nav.rksvLeafOfflineOrders`, `rksvHub.offlineOrdersPage.*` (de/en/tr) |

**Patterns:** `useAntdApp()` for modal/message; TanStack Query; filters for status + cash register.

**Related (legacy):** `/admin/tse/offline-transactions` — TSE payment-intent backlog (`offline_transactions`). See [`OFFLINE_SYSTEMS_SEPARATION.md`](OFFLINE_SYSTEMS_SEPARATION.md).

---

## Operational rules

1. **72-hour window:** Pending orders must sync within 72 h or cleanup job deletes them.
2. **Max 3 sync attempts** per order → `failed`.
3. **Tenant isolation:** EF global filters; admin cross-tenant → 404.
4. **No voucher offline orders** — backend rejects; POS must not enqueue.
5. **BelegNr:** Batch reserve before replay batch; release unused tail on failure.
6. **Hosted cleanup:** `OfflineOrderCleanupHostedService` — scope + `IServiceScopeFactory` pattern.

---

## Validation

```bash
# Backend
cd backend && dotnet build
cd backend && dotnet test --filter "FullyQualifiedName~SequenceReservationServiceTests"

# Admin i18n (after locale edits)
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true
```

**Migration (local):**

```bash
cd backend && dotnet ef database update --project KasseAPI_Final.csproj
```

---

## Related documentation

- [`docs/API_CONTRACTS.md`](../API_CONTRACTS.md) — API supplement (offline orders section)
- [`ai/modules/offline_orders.md`](../../ai/modules/offline_orders.md) — AI module guardrails
- [`docs/release/POS_OFFLINE_QUEUE_UX.md`](POS_OFFLINE_QUEUE_UX.md) — legacy intent queue UX
- [`docs/release/OFFLINE_REPLAY_BATCH_CORRELATION.md`](OFFLINE_REPLAY_BATCH_CORRELATION.md) — legacy replay correlation
- [`AGENTS.md`](../../AGENTS.md) — voucher offline rule, API boundaries
