# Offline System Manual Test Checklist

> **Last updated:** 2026-06-27  
> **Primary scope:** POS full order snapshots (`offline_orders`).  
> **Index:** [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md)  
> **Detailed scenarios:** [`OFFLINE_SYSTEM_TEST_PLAN.md`](OFFLINE_SYSTEM_TEST_PLAN.md)  
> **Production deploy:** [`OFFLINE_PRODUCTION_DEPLOYMENT.md`](OFFLINE_PRODUCTION_DEPLOYMENT.md)  
> **Automated smoke:** `node scripts/test-offline-system.mjs` (Windows) or `./scripts/test-offline-system.sh`

---

## Preparation

- [ ] Backend running on `http://localhost:5184` (`cd backend && dotnet run`)
- [ ] Database migrated (`20260627002059_AddOfflineOrdersTable`)
- [ ] POS Web open (Expo web — typically `http://localhost:8081` or project dev port)
- [ ] Frontend Admin running for FA tests (`cd frontend-admin && npm run dev`)
- [ ] Test user available: **cashier1** (POS — `payment.take`)
- [ ] Manager user available: **manager1** (FA — `settings.manage`, `payment.view`)
- [ ] Test tenant active: **dev** (`X-Tenant-Id: dev` in dev)
- [ ] Cash register **open** and assigned to POS session
- [ ] Network tools ready:
  - **Web:** Chrome DevTools → Network → **Offline**
  - **Mobile:** Airplane mode / disable Wi‑Fi
- [ ] Clear prior offline data (optional clean start):
  - Web: DevTools → Application → IndexedDB → `regkasse_offline_orders` → delete
  - Mobile: clear app storage or uninstall/reinstall Expo Go build

**Tester:** _______________  
**Date:** _______________  
**Platform:** Web / iOS / Android (circle one)

---

## Tests — POS (`offline_orders`)

### Login

- [ ] Login with username + password (`cashier1`)
- [ ] Login succeeds; main POS tabs visible
- [ ] JWT stored (session storage / AsyncStorage — not plain text password)
- [ ] Offline session saved (`offline_session` key via `OfflineSessionManager`)
- [ ] No offline banner while online with zero pending orders

---

### Offline mode

- [ ] Disconnect internet (DevTools → Offline or airplane mode)
- [ ] Within ~5 s, offline state detected
- [ ] Banner appears with German copy:
  - [ ] **OFFLINE-MODUS**
  - [ ] **Keine Internetverbindung**
  - [ ] Pending line shows **0 Bestellungen warten** (or singular)
- [ ] User can still browse products and use cart
- [ ] Token still valid — no forced logout

---

### Order creation (single)

- [ ] Add **1 product** to cart; pay with **cash** or **card** (not voucher)
- [ ] Checkout completes locally (no API error blocking UX)
- [ ] Order saved to local storage:
  - Web: IndexedDB `regkasse_offline_orders` → store `orders`
  - Mobile: `@regkasse/offline_orders_storage_v1`
- [ ] Row fields present: `status: pending`, `syncAttempts: 0`, `expiresAt ≈ now + 72h`
- [ ] `offlineOrderId` matches pattern: `OFFLINE-{yyyyMMddHHmmss}-{4digits}`
- [ ] Banner pending count: **1**

---

### Multiple orders

- [ ] Create **5 more** orders offline (total **6**)
- [ ] All 6 visible in local storage
- [ ] Banner shows **6 Bestellungen warten** (or equivalent)
- [ ] No duplicate `offlineOrderId` values

---

### Voucher block (negative test)

- [ ] While offline, attempt **voucher** payment (or voucher split amount)
- [ ] Payment blocked with German message (e.g. *Gutschein erfordert Online-Verbindung* or full voucher-offline text)
- [ ] **No** new row in offline order storage
- [ ] Pending count unchanged

---

### Expiry warning

> **Shortcut:** Edit local storage — set one order’s `expiresAt` to **now + 12 hours** (do not wait 48 h).

- [ ] Banner shows warning line (German):
  - [ ] **⚠️ {N} Bestellung(en) laufen innerhalb von 24 Stunden ab**
- [ ] Warning text highlighted (amber `#ffe082` on banner)
- [ ] Order still present in storage (not deleted yet)

---

### Sync — automatic

- [ ] Reconnect internet (DevTools Online / disable airplane mode)
- [ ] Auto-sync runs within **30 seconds** (`SYNC_INTERVAL_SECONDS`)
- [ ] Network tab shows:
  - [ ] `POST /api/pos/offline-orders` (upload, if not yet on server)
  - [ ] `POST /api/pos/offline-orders/replay?cashRegisterId=...`
- [ ] Successful orders **removed** from local storage
- [ ] Banner **hidden** when online and pending count = 0  
  _(Note: banner does not show “✅ X synced”; success = empty queue + no banner)_
- [ ] Backend: rows in `offline_orders` with `status = synced`, `synced_payment_id` set
- [ ] Receipts / payments have TSE signature and sequential BelegNr

---

### Sync — manual

- [ ] Create **1–2** offline orders while online is flaky OR use pending from prior step before reconnect
- [ ] Tap **Jetzt synchronisieren** / **Synchronisieren** on banner
- [ ] Button shows **Synchronisierung läuft…** + spinner; disabled while running
- [ ] Orders sync; pending count updates correctly

---

### Token expiry

> **Shortcut:** Expire JWT in stored `offline_session` (`expiresAt` in past) or use expired test token.

- [ ] With expired token, user **cannot** add to cart / checkout offline
- [ ] Auto-sync does not run with invalid token
- [ ] App redirects to **login** (or shows auth error on init)
- [ ] Optional (before expiry): warning **Token läuft in Kürze ab…** when token near expiry

---

### Performance (batch)

- [ ] Go offline; create **50** orders (cash/card)
- [ ] Reconnect; trigger sync (auto or manual)
- [ ] All 50 sync successfully
- [ ] Total sync time **< 5 seconds** on dev machine (note actual: _______ s)
- [ ] No data loss — 50 rows in `payment_details` / 50 removed from local storage
- [ ] BelegNr sequence continuous (no gaps for successful batch)

---

## Tests — Frontend Admin

### Offline settings (`/settings/offline`)

- [ ] Login as **manager1** (or user with `settings.manage`)
- [ ] Navigate: **Settings → Offline mode** (`/settings/offline`)
- [ ] Page loads with form fields:
  - [ ] Max offline transactions
  - [ ] Max offline orders
  - [ ] Offline expiry hours
  - [ ] Token expiry hours
  - [ ] Enable offline orders / payments toggles
- [ ] Change **max offline orders** to **50**
- [ ] Save → success toast (i18n save message)
- [ ] Reload page → value still **50**
- [ ] Revert to default (**100**) if desired for other testers

> If save fails with 404, backend `/api/admin/settings/offline` may not be deployed — log as defect; POS defaults still apply from `frontend/constants/offlineConfig.ts`.

---

### RKSV offline orders panel (`/rksv/offline-orders`)

- [ ] Navigate: **RKSV → Diagnose → Offline-Bestellungen**
- [ ] Pending / synced / failed filters work
- [ ] Orders from POS sync appear in list
- [ ] Single **Replay** action works for stuck pending row (if test data available)
- [ ] Page does **not** show legacy TSE intent queue (that is `/admin/tse/offline-transactions`)

---

## Tests — Legacy queue (optional regression)

Only if payment/TSE code changed in the release.

- [ ] Settings → **Offline-Warteschlange** opens queue screen
- [ ] TSE non-fiscal pending entries use `@regkasse/offline_transactions_v1` (separate from order snapshots)
- [ ] Replay via `/api/offline-transactions/replay` still works

See [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md).

---

## Quick verification commands

```bash
# Health + tenant
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health

# Automated structure checks
node scripts/test-offline-system.mjs
# or: ./scripts/test-offline-system.sh
```

```sql
-- Recent offline orders (replace tenant id)
SELECT offline_order_id, status, sync_attempts, synced_invoice_number, created_at_utc
FROM offline_orders
WHERE tenant_id = '<tenant_uuid>'
ORDER BY created_at_utc DESC
LIMIT 20;
```

---

## Test sign-off

| Area | Pass | Fail | Notes |
|------|------|------|-------|
| Login & session | ☐ | ☐ | |
| Offline mode & banner | ☐ | ☐ | |
| Order creation & storage | ☐ | ☐ | |
| Multiple orders | ☐ | ☐ | |
| Voucher block | ☐ | ☐ | |
| Expiry warning | ☐ | ☐ | |
| Auto sync | ☐ | ☐ | |
| Manual sync | ☐ | ☐ | |
| Token expiry | ☐ | ☐ | |
| Batch performance (50) | ☐ | ☐ | |
| FA offline settings | ☐ | ☐ | |
| RKSV offline orders | ☐ | ☐ | |

- [ ] All **required** tests passed
- [ ] Defects logged (IDs: _______________)
- [ ] No data loss or RKSV sequence gaps observed
- [ ] System ready for production / staging promotion

**Signed off by:** _______________  
**Date:** _______________
