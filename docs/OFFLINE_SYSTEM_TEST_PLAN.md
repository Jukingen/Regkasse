# Offline System Test Plan

> **Last updated:** 2026-06-27  
> **Scope:** End-to-end validation of Regkasse offline capabilities — **both** independent pipelines.  
> **Index:** [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md)  
> **Reference:** [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md), [`docs/release/OFFLINE_ORDERS_FULL_SNAPSHOT.md`](release/OFFLINE_ORDERS_FULL_SNAPSHOT.md), [`AGENTS.md`](../AGENTS.md)

---

## 1. Scope & system boundaries

Regkasse has **two separate offline systems**. Tests must name which system is under test and must not conflate APIs, tables, or UI routes.

| System | Table | POS local storage | POS sync API | Admin UI |
|--------|-------|-------------------|--------------|----------|
| **New — full order snapshots** | `offline_orders` | `@regkasse/offline_orders_storage_v1` (AsyncStorage / IndexedDB) | `/api/pos/offline-orders/*` | `/rksv/offline-orders` |
| **Legacy — TSE payment intents** | `offline_transactions` | `@regkasse/offline_transactions_v1` | `POST /api/offline-transactions/replay` | `/admin/tse/offline-transactions` |

**Shared outcome only:** successful replay in either system may create rows in `payment_details` with TSE signatures. Storage, APIs, and operator workflows remain separate.

**Primary focus of Scenarios 1–10:** new `offline_orders` flow (`OfflineOrderManager`, `OfflineBanner`, Admin RKSV panel).  
**Legacy supplement:** Section 8 covers TSE intent queue regression.

---

## 2. Test environment

### 2.1 Services

| Component | URL / command | Notes |
|-----------|---------------|-------|
| Backend API | `http://localhost:5184` | `cd backend && dotnet run` |
| POS Web | `http://localhost:3000` (or Expo web port) | IndexedDB for offline orders |
| POS Mobile | Expo Go (iOS/Android) | AsyncStorage for offline orders |
| Frontend Admin | `http://localhost:3001` (typical) | `cd frontend-admin && npm run dev` |
| PostgreSQL | Local / Docker | Migration `20260627002059_AddOfflineOrdersTable` applied |

### 2.2 Tenant & auth

```bash
# Dev tenant resolution
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health

# Login (Cashier — payment.take)
curl -X POST http://localhost:5184/api/Auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: dev" \
  -d '{"loginIdentifier":"cashier1","password":"<password>"}'
```

| Item | Value |
|------|-------|
| Test tenant | `dev` (`X-Tenant-Id: dev` or `?tenant=dev`) |
| Cashier (POS) | `cashier1` — requires `payment.take` |
| Manager (Admin) | `manager1` — requires `payment.view` for RKSV panel |
| Cash register | Open register assigned to test device/session |

### 2.3 Network control

| Platform | Method |
|----------|--------|
| POS Web | Chrome DevTools → Network → **Offline** |
| POS Mobile | OS airplane mode / disable Wi‑Fi + cellular |
| Backend isolation | Stop backend process or block port 5184 |
| Simulated offline (dev) | `frontend/constants/devSimulatePosOffline.ts` (if enabled) |

### 2.4 Configuration constants (source of truth)

From `frontend/constants/offlineConfig.ts`:

| Setting | Value | Used for |
|---------|-------|----------|
| `OFFLINE_EXPIRY_HOURS` | 72 | Order expiry (local + server) |
| `OFFLINE_WARNING_HOURS` | 24 | Banner expiry warning threshold |
| `OFFLINE_CRITICAL_HOURS` | 6 | Token/offline time critical warning |
| `SYNC_INTERVAL_SECONDS` | 30 | Auto-sync poll interval |
| `SYNC_RETRY_MAX` | 3 | Max sync attempts → local `failed` |
| `MAX_OFFLINE_ORDERS` | 100 | POS order queue cap |
| `MAX_OFFLINE_TRANSACTIONS` | 50 | Legacy TSE offline cap (production RKSV) |
| `TOKEN_EXPIRY_HOURS` | 168 | Fallback when JWT has no `exp` |
| `ENABLE_OFFLINE_GUTSCHEIN` | `false` | Voucher offline — must stay disabled |

Backend mirrors: 72 h expiry, max 3 sync attempts, voucher rejected on save.

### 2.5 Pre-test checklist

- [ ] Database migrated (`dotnet ef database update`)
- [ ] Cash register **open** for test tenant
- [ ] TSE reachable (or Demo TSE mode for dev) for successful replay fiscal assertions
- [ ] NTP/time sync healthy if testing fiscal online path
- [ ] Clear local offline storage before baseline runs (AsyncStorage / IndexedDB / browser storage)
- [ ] Note JWT expiry (default session **24 h** per `AGENTS.md`; offline session uses JWT `exp` when present)

---

## 3. POS E2E scenarios — offline orders (`offline_orders`)

### Scenario 1: Normal online operation

**Given:** Internet available, valid credentials, open cash register  
**When:** User opens POS and logs in  
**Then:**

- [ ] Login succeeds; JWT stored (session + `OfflineSessionManager`)
- [ ] `OfflineBanner` is **hidden** (`isOnline && pendingCount === 0`)
- [ ] Checkout completes via normal payment API (no local offline queue write)
- [ ] Receipt shows TSE signature and sequential BelegNr
- [ ] `offline_orders` table has no new pending rows for this checkout path

**Verification:**

```sql
SELECT count(*) FROM offline_orders
WHERE tenant_id = '<tenant_uuid>' AND status = 'pending';
-- Expect 0 after online-only session
```

---

### Scenario 2: Offline mode activation

**Given:** User logged in with valid token, at least one item in cart  
**When:** Network disconnected (DevTools Offline / airplane mode)  
**Then:**

- [ ] `NetInfo` / `navigator.onLine` reports offline within a few seconds
- [ ] `OfflineBanner` appears with German copy:
  - Title: **OFFLINE-MODUS**
  - Subtitle: **Keine Internetverbindung**
  - Pending count: **0 Bestellung(en) warten** (or equivalent singular/plural)
- [ ] JWT remains in storage; `OfflineSessionManager.canWorkOffline()` returns `true`
- [ ] User can browse products and build cart (no forced logout)

**Negative:** Banner must **not** appear when online with zero pending orders.

---

### Scenario 3: Offline order creation

**Given:** Offline mode active, token valid, payment method **cash** or **card** (not voucher)  
**When:** User completes checkout while API unreachable  
**Then:**

- [ ] Order persisted locally (`@regkasse/offline_orders_storage_v1`)
- [ ] `offlineOrderId` matches pattern: `OFFLINE-{yyyyMMddHHmmss}-{4digits}` (UTC timestamp)
- [ ] Local fields: `status: pending`, `syncAttempts: 0`, `expiresAt ≈ createdAt + 72h`
- [ ] `orderData` contains full payment snapshot (incl. `cashRegisterId`)
- [ ] Banner pending count increments (+1)
- [ ] No voucher secrets in local payload (grep storage — must not contain voucher codes)

**Inspect local storage (web):** DevTools → Application → IndexedDB → `regkasse_offline_orders`

**Backend (before sync):** No row yet **or** row only after upload phase — local-first save does not require backend until sync.

---

### Scenario 4: Offline order sync (automatic)

**Given:** ≥1 pending local offline order, valid token, backend reachable  
**When:** Network restored; wait up to **30 seconds** (`SYNC_INTERVAL_SECONDS`)  
**Then:**

- [ ] `OfflineSyncService` / `OfflineOrderManager` detects online (`NetInfo` reconnect → `sync:online`)
- [ ] Upload: `POST /api/pos/offline-orders` for orders without `serverOrderGuid`
- [ ] Replay: `POST /api/pos/offline-orders/replay?cashRegisterId={id}`
- [ ] Batch BelegNr reserved via `ISequenceReservationService` (consecutive, no gaps for successful batch)
- [ ] Each success creates `payment_details` with TSE signature
- [ ] Local order **deleted** from storage on success
- [ ] Banner hidden when `pendingCount === 0`
- [ ] `sync:completed` event emitted with `{ synced, errors }` (listeners may show Alert — not required on banner)

**Backend verification:**

```sql
SELECT offline_order_id, status, synced_payment_id, synced_invoice_number, sync_attempts
FROM offline_orders
WHERE offline_order_id LIKE 'OFFLINE-%'
ORDER BY created_at_utc DESC
LIMIT 5;
-- Expect status = 'synced', synced_payment_id NOT NULL
```

**Audit:** `OFFLINE_ORDER_CREATED` on save; payment creation follows normal audit trail.

---

### Scenario 5: Offline order sync (manual)

**Given:** Pending offline orders, device online  
**When:** User taps **Jetzt synchronisieren** / **Synchronisieren** on `OfflineBanner`  
**Then:**

- [ ] Button shows loading: **Synchronisierung läuft…** + spinner
- [ ] Button disabled while sync in progress
- [ ] Same upload + replay pipeline as Scenario 4
- [ ] On full success: pending count → 0, banner hides (online) or switches to offline pending state
- [ ] On partial failure: failed orders remain locally with incremented `syncAttempts` and `lastError`

**API trace (optional):** Confirm single replay call per cash register batch, not N duplicate payments.

---

### Scenario 6: Offline expiry warning (< 24 h)

**Given:** Pending order with `expiresAt` within 24 hours  
**When:** Banner renders / status poll (5 s hook interval)  
**Then:**

- [ ] Banner shows warning line (German):
  - **⚠️ {N} Bestellung(en) laufen innerhalb von 24 Stunden ab**
- [ ] Warning text uses highlight color (`#ffe082`) on banner
- [ ] `OfflineOrderManager.checkExpiryWarning` fires for orders ≤ 24 h remaining
- [ ] `OfflineNotificationService` may emit `offline:warning` / `offline:critical` for token hours (separate from order expiry)

**Test shortcut:** Manually edit local storage `expiresAt` to `now + 12h` — do not wait 48 h real time.

---

### Scenario 7: Offline order expiry (72 h)

**Given:** Pending order past `expiresAt`  
**When:** Sync tick runs (`purgeExpiredPending`) **or** backend cleanup job runs  
**Then:**

**POS (local):**

- [ ] Expired pending rows **deleted** from local storage (not marked `expired` locally)
- [ ] Banner pending count decreases accordingly
- [ ] No automatic replay attempted for expired local rows

**Backend (server-side rows):**

- [ ] `OfflineOrderCleanupHostedService` (every **6 h**) deletes pending rows where `expires_at_utc <= now`
- [ ] Audit log action: **`OFFLINE_ORDERS_CLEANUP`** (batch), **not** per-row `OFFLINE_ORDER_EXPIRED`
- [ ] Deleted rows removed from DB (hard delete)

**Test shortcut:**

1. Insert pending row with `expires_at_utc = now() - interval '1 minute'` via SQL, **or**
2. Call cleanup via service test / wait for hosted service in long-running env test

```sql
-- After cleanup
SELECT * FROM offline_orders WHERE offline_order_id = '<test_id>';
-- Expect 0 rows
```

---

### Scenario 8: Token expiry while offline

**Given:** User offline with expired or missing JWT (`OfflineSessionManager.isTokenExpired() === true`)  
**When:** User attempts cart/checkout or auto-sync runs  
**Then:**

- [ ] `canWorkOffline()` returns `false`
- [ ] `OfflineSyncService.autoSync` skips sync when token invalid
- [ ] Cart operations blocked (`useCart` logs token expired; add/update rejected)
- [ ] `AuthContext` clears storage and redirects to login when token expired on init
- [ ] Optional warning via `OfflineNotificationService`: **Token läuft in Kürze ab. Bitte verbinden Sie sich mit dem Internet.** (when expiring soon, not fully expired)

**Test shortcut:** Set JWT `exp` in past in stored session JSON, or use short-lived test token.

**Note:** Offline work requires valid JWT; there is no indefinite offline mode beyond token lifetime.

---

### Scenario 9: Voucher offline block

**Given:** Offline mode active  
**When:** User selects voucher payment or enters voucher split amount  
**Then:**

- [ ] Payment method **voucher** disabled in UI (`posOfflineBlocksVoucherByMethod`)
- [ ] Split entry with voucher amount blocked (`posOfflineBlocksVoucherSplitEntry`)
- [ ] Checkout attempt shows German error (queue save path):
  - **Gutschein-Zahlungen sind ohne Online-Verbindung nicht möglich. Bitte Internet prüfen und erneut versuchen. Aus Sicherheitsgründen wird der Gutscheincode nicht lokal zwischengespeichert.**
- [ ] POS UI may also show: **Gutschein erfordert Online-Verbindung**
- [ ] `OfflineOrderManager.saveOrder` throws; no local row created
- [ ] Backend `POST /api/pos/offline-orders` with `paymentMethod: voucher` → **400**  
  `"Voucher payments cannot be queued as offline orders."`

---

### Scenario 10: Multiple offline orders (batch performance)

**Given:** User creates **50** offline orders (cash/card), same register  
**When:** Network restored; auto or manual sync  
**Then:**

- [ ] All 50 uploaded then replayed in one batch per register
- [ ] `ReserveSequencesAsync(50, cashRegisterId)` allocates 50 consecutive counters
- [ ] 50 distinct BelegNr in `payment_details`; signature chain continuous
- [ ] Failed tail sequences released (`ReleaseSequencesAsync`) if mid-batch failure injected
- [ ] End-to-end sync completes in **< 5 seconds** on local dev hardware (adjust for CI/staging)
- [ ] Local storage empty for successful orders

**Failure injection (optional):** Force 3rd order replay failure → verify max 3 attempts → status `failed` on server, local `failed` after 3 sync attempts.

---

## 4. Backend API scenarios — `/api/pos/offline-orders`

Execute with Cashier JWT + `X-Tenant-Id: dev`.

| ID | Test | Steps | Expected |
|----|------|-------|----------|
| B1 | Save order | `POST /api/pos/offline-orders` valid body | 200, `offlineOrderId`, `expiresAtUtc = created + 72h`, audit `OFFLINE_ORDER_CREATED` |
| B2 | Voucher rejected | `paymentMethod: voucher` | 400 |
| B3 | Unknown register | Invalid `cashRegisterId` | 404 |
| B4 | List pending | `GET .../pending?cashRegisterId=` | Only `pending` + non-expired, FIFO order |
| B5 | Replay empty | Replay with no pending | 200, `total: 0` |
| B6 | Replay success | Save + replay | `synced`, `syncedPaymentId`, `syncedInvoiceNumber` populated |
| B7 | Status lookup | `GET .../{offlineOrderId}/status` | Matches row state |
| B8 | Missing order | Unknown `offlineOrderId` | 404 |
| B9 | Permission | No `payment.take` | 403 |
| B10 | Cross-tenant | JWT tenant A, header tenant B | **404** (not 403) |
| B11 | Reserved receipt conflict | Replay with conflicting BelegNr | `RECEIPT_NUMBER_CONFLICT`, sequence release |
| B12 | Max attempts | Fail replay 3× | `status = failed`, `sync_attempts = 3` |

**Sample save payload:**

```json
{
  "cashRegisterId": "<uuid>",
  "orderData": { "cashRegisterId": "<uuid>", "totalAmount": 12.50, "items": [] },
  "orderTotal": 12.50,
  "paymentMethod": "cash"
}
```

---

## 5. Admin panel scenarios — `/rksv/offline-orders`

**Prerequisite:** Manager+ with `payment.view`, FA logged in.

| ID | Test | Expected |
|----|------|----------|
| A1 | Page access | RKSV → Diagnose → **Offline-Bestellungen** loads |
| A2 | Permission gate | User without `payment.view` → 403 redirect |
| A3 | List & filters | Status filter (`pending`/`synced`/`failed`/all), cash register filter, pagination |
| A4 | Single replay | Row action replay → success toast, row → `synced` |
| A5 | Replay all | `POST .../replay-all?cashRegisterId=` processes pending batch |
| A6 | i18n | Labels in de/en/tr via `rksvHub.offlineOrdersPage.*` |
| A7 | Separation | Page does **not** call `/api/admin/offline-transactions` |
| A8 | Cross-tenant | Impersonation shows only target tenant rows |

**Negative navigation test:** `/admin/tse/offline-transactions` shows **legacy** TSE queue — different data source, different copy.

---

## 6. RKSV & fiscal compliance

| ID | Requirement | Verification |
|----|-------------|--------------|
| F1 | BelegNr sequential per register/day | Compare `synced_invoice_number` sequence segment after batch replay |
| F2 | TSE signature on replayed receipts | `payment_details.tse_signature` NOT NULL |
| F3 | No duplicate BelegNr | Replay same offline order twice → idempotent / conflict handling |
| F4 | Batch reserve before replay | Log/trace shows `ReserveSequencesAsync` before payment loop |
| F5 | Failed replay releases tail sequences | Inject failure; `receipt_sequences.next_sequence` rolled back for unused slots |
| F6 | Voucher never offline | POS + API rejection (Scenario 9) |
| F7 | 72 h window | Expired pending not replayable |
| F8 | NTP gate (online fiscal) | Block payment when time sync failed (separate from offline queue, but verify no bypass via replay) |

---

## 7. Security & multi-tenant

| ID | Test | Expected |
|----|------|----------|
| S1 | Tenant isolation | Tenant A JWT cannot read Tenant B offline orders → 404 |
| S2 | No voucher secrets at rest | Local storage scan — no plaintext voucher codes |
| S3 | API boundary POS | POS never calls `/api/admin/offline-orders` |
| S4 | API boundary Admin | Admin never calls `/api/pos/offline-orders` for operator workflows |
| S5 | Audit trail | `OFFLINE_ORDER_CREATED`, `OFFLINE_ORDERS_CLEANUP` include `tenant_id`, actor |

---

## 8. Legacy TSE offline transactions (regression)

Run when touching shared POS sync or payment code. Full UX spec: [`docs/release/POS_OFFLINE_QUEUE_UX.md`](release/POS_OFFLINE_QUEUE_UX.md).

| ID | Test | Expected |
|----|------|----------|
| L1 | Non-fiscal pending enqueue | Payment fails → `NON_FISCAL_PENDING` → `@regkasse/offline_transactions_v1` |
| L2 | Settings queue screen | **Offline-Warteschlange** lists pending/synced/failed |
| L3 | Replay API | `POST /api/offline-transactions/replay` idempotent |
| L4 | TSE offline cap | Production cap 50/register (`TseOptions.MaxOfflineTransactionsPerCashRegister`); warn at 40 |
| L5 | Reconnect alert | After sync, Alert: **X Zahlung(en) erfolgreich synchronisiert** |
| L6 | Separation | Legacy queue does **not** write to `offline_orders` |

---

## 9. Automated test matrix

| Layer | Command / location | Covers |
|-------|-------------------|--------|
| Backend sequence reserve | `cd backend && dotnet test --filter "FullyQualifiedName~SequenceReservationServiceTests"` | Batch BelegNr, tail release |
| Backend offline replay (legacy) | `dotnet test --filter "FullyQualifiedName~OfflineReplay"` | Intent replay idempotency |
| Frontend offline hardening | `frontend/tsconfig.offlineHardeningCheck.json` (if used in CI) | Type-level offline modules |
| TSE banner copy contract | `frontend/__tests__/tseStatusBannerOfflineCopy.contract.test.ts` | German offline copy |
| Admin i18n | `node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true` | RKSV offline orders page keys |

**Gap (manual until added):** Dedicated `OfflineOrderService` integration tests — track as follow-up; Scenarios B1–B12 remain manual or curl-based.

---

## 10. Test data

### 10.1 Users & tenant

| Role | Login | Permission |
|------|-------|------------|
| Cashier | `cashier1` | `payment.take` |
| Manager | `manager1` | `payment.view`, admin RKSV |

### 10.2 Sample orders

Create 3 template carts for repeat runs:

| Template | Items | Payment | Total (approx.) |
|----------|-------|---------|-----------------|
| T1 — small | 2 products | cash | €5–15 |
| T2 — medium | 4 products + modifier | card | €20–40 |
| T3 — batch | 2 products × 25 repeats | cash | for Scenario 10 |

### 10.3 SQL helpers

```sql
-- Pending count by register
SELECT cash_register_id, status, count(*)
FROM offline_orders
WHERE tenant_id = '<tenant_uuid>'
GROUP BY 1, 2;

-- Recent audit entries
SELECT action_type, description, created_at
FROM audit_logs
WHERE action_type IN ('OFFLINE_ORDER_CREATED', 'OFFLINE_ORDERS_CLEANUP')
ORDER BY created_at DESC
LIMIT 20;
```

---

## 11. Execution checklist (release gate)

### POS — offline orders (Scenarios 1–10)

- [ ] 1 Normal online
- [ ] 2 Offline activation
- [ ] 3 Offline order creation
- [ ] 4 Auto sync
- [ ] 5 Manual sync
- [ ] 6 Expiry warning
- [ ] 7 Expiry cleanup
- [ ] 8 Token expiry
- [ ] 9 Voucher block
- [ ] 10 Batch performance

### Backend API (B1–B12)

- [ ] All pass or documented exceptions

### Admin (A1–A8)

- [ ] All pass

### Fiscal (F1–F8)

- [ ] All pass

### Legacy regression (L1–L6)

- [ ] Pass if payment/sync code changed

---

## 12. Success criteria

1. **All 10 POS scenarios pass** on Web + at least one mobile platform (iOS or Android).
2. **No data loss** for pending orders within the 72 h window when sync succeeds.
3. **RKSV compliance:** sequential BelegNr, TSE signatures on synced receipts, voucher never queued offline.
4. **Tenant isolation:** cross-tenant access returns HTTP **404**.
5. **UX:** German POS copy correct; banner states match connectivity + pending count; manual sync responsive.
6. **Performance:** 50-order batch sync < 5 s on dev reference machine.
7. **Separation:** `offline_orders` and `offline_transactions` tests pass independently; no API cross-wiring.
8. **Automated tests** in Section 9 green in CI.

---

## 13. Related documentation

| Document | Topic |
|----------|-------|
| [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md) | Two-system map |
| [`docs/release/OFFLINE_ORDERS_FULL_SNAPSHOT.md`](release/OFFLINE_ORDERS_FULL_SNAPSHOT.md) | Implementation detail |
| [`docs/release/POS_OFFLINE_QUEUE_UX.md`](release/POS_OFFLINE_QUEUE_UX.md) | Legacy queue UX |
| [`ai/modules/offline_orders.md`](../ai/modules/offline_orders.md) | AI guardrails (new) |
| [`ai/modules/offline_transactions_legacy.md`](../ai/modules/offline_transactions_legacy.md) | AI guardrails (legacy) |
| [`AGENTS.md`](../AGENTS.md) | Voucher rule, API boundaries, TSE offline limits |
