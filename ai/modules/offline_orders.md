# Module: Offline orders (full snapshots)

## Scope

- **`offline_orders`** table — full POS order snapshots (not payment-intent `offline_transactions`)
- POS save/replay: `/api/pos/offline-orders/*`
- Admin list/replay: `/api/admin/offline-orders/*`
- FA page: `/rksv/offline-orders` (`payment.view`)
- FA monitoring: `/api/admin/offline-monitoring/*`, dashboard widget `offline-system-status`
- FA settings UI: `/settings/offline` (`settings.manage`) — backend `GET/PUT /api/admin/settings/offline` when deployed
- Batch BelegNr reservation: `ISequenceReservationService`

**Do not conflate** with legacy `OfflineTransactionService` / `POST /api/offline-transactions/replay`.

**Separation guide:** [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../../docs/release/OFFLINE_SYSTEMS_SEPARATION.md) — legacy lives at `/admin/tse/offline-transactions` + `offline_transactions` table.

## Multi-tenant

- `offline_orders.tenant_id` NOT NULL; implements `ITenantEntity`.
- EF global query filters apply; cross-tenant admin access → HTTP **404**.
- Background cleanup uses `IServiceScopeFactory` + scoped `AppDbContext` (`OfflineOrderCleanupHostedService`).

## API boundaries

| Client | Prefix | Forbidden |
|--------|--------|-----------|
| POS | `/api/pos/offline-orders/*` | `/api/admin/offline-orders/*` |
| Admin | `/api/admin/offline-orders/*` | `/api/pos/offline-orders/*` |

### POS

| Method | Path | Permission |
|--------|------|------------|
| `POST` | `/api/pos/offline-orders` | `payment.take` |
| `GET` | `/api/pos/offline-orders/pending` | `payment.take` |
| `POST` | `/api/pos/offline-orders/replay` | `payment.take` |
| `GET` | `/api/pos/offline-orders/{offlineOrderId}/status` | `payment.take` |

### Admin

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/admin/offline-orders` | `payment.view` |
| `POST` | `/api/admin/offline-orders/{id}/replay` | `payment.view` |
| `POST` | `/api/admin/offline-orders/replay-all` | `payment.view` |

### Admin monitoring

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/admin/offline-monitoring/status` | `payment.view` |
| `GET` | `/api/admin/offline-monitoring/orders/stats` | `payment.view` |
| `GET` | `/api/admin/offline-monitoring/transactions/stats` | `payment.view` |
| `GET` | `/api/admin/offline-monitoring/anomalies` | `payment.view` |
| `GET` | `/api/admin/offline-monitoring/sync-health` | `payment.view` |

Background: `OfflineAlertService` (activity feed on critical anomalies), config `OfflineAlertRules` / `OfflineMonitoringOptions`.

## Business rules (high risk)

1. **Voucher payments** — never accept offline order save/replay for voucher method.
2. **72 h expiry** — pending rows past `expires_at_utc` are **deleted** by cleanup (not soft-expired).
3. **Max 3 sync attempts** → status `failed`.
4. **Replay BelegNr** — use `ISequenceReservationService.ReserveSequencesAsync` before batch replay; `ReleaseSequencesAsync` on failed tail slots.
5. **`CreatePaymentRequest.ReservedReceiptNumber`** — must match pre-reserved BelegNr; conflict code `RECEIPT_NUMBER_CONFLICT`.
6. Do not bypass tenant filters except documented Super Admin patterns.

## Key files

| Area | Path |
|------|------|
| Model | `backend/Models/OfflineOrder.cs` |
| Service | `backend/Services/Offline/OfflineOrderService.cs` |
| Sequence | `backend/Services/Offline/SequenceReservationService.cs` |
| POS controller | `backend/Controllers/PosOfflineOrdersController.cs` |
| Admin controller | `backend/Controllers/AdminOfflineOrdersController.cs` |
| Cleanup | `backend/Services/Hosted/OfflineOrderCleanupHostedService.cs` |
| Monitoring | `backend/Services/Offline/OfflineMonitoringService.cs` |
| Alerting | `backend/Services/Offline/OfflineAlertService.cs` |
| Monitoring API | `backend/Controllers/AdminOfflineMonitoringController.cs` |
| POS manager | `frontend/services/offline/offlineOrderManager.ts` |
| FA page | `frontend-admin/src/app/(protected)/rksv/offline-orders/page.tsx` |
| FA widget | `frontend-admin/src/features/dashboard/components/OfflineStatusWidget.tsx` |

## Human documentation

- [`docs/OFFLINE_SYSTEM_INDEX.md`](../../docs/OFFLINE_SYSTEM_INDEX.md) — master index
- [`docs/OFFLINE_SYSTEM_TEST_PLAN.md`](../../docs/OFFLINE_SYSTEM_TEST_PLAN.md)
- [`docs/OFFLINE_MANUAL_TEST_CHECKLIST.md`](../../docs/OFFLINE_MANUAL_TEST_CHECKLIST.md)
- [`docs/OFFLINE_PRODUCTION_DEPLOYMENT.md`](../../docs/OFFLINE_PRODUCTION_DEPLOYMENT.md)
- [`docs/release/OFFLINE_ORDERS_FULL_SNAPSHOT.md`](../../docs/release/OFFLINE_ORDERS_FULL_SNAPSHOT.md)
