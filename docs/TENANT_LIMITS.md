# Tenant Limits

Per-mandant operational caps stored in `tenant_limits` (one row per tenant, `UNIQUE(tenant_id)`). Missing rows are created with defaults on first read (`ITenantLimitService.GetLimitsAsync`).

**Always-applied summary:** [`AGENTS.md`](../AGENTS.md) § Tenant Limits  
**Config:** [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md) § Tenant Limits / `CacheSettings:TenantLimitsCacheMinutes`

These caps are **not** the same as:

| System | Purpose |
|--------|---------|
| `TrialLimitGuard` | Extra registers/users while a SaaS trial is open (`TRIAL_LIMIT_EXCEEDED`) |
| `TenantOperationLimits` | Rate / operation-budget guards (`OPERATION_LIMIT_EXCEEDED`) |
| FA `/api/admin/settings/offline` | Offline-order settings (expiry, enable flags). **Not** the TSE intent queue cap |

Do **not** merge those systems into `tenant_limits`.

---

## Limits (9)

`maxUsersPerRegister` was removed: assignment is 1:1 (`CashRegister.AssignedUserId`), so a per-register cashier cap never fired.

| Key (`TenantLimitKeys`) | Column | Default | Enforced when |
|-------------------------|--------|---------|----------------|
| `maxActiveRegistersPerUser` | `max_active_registers_per_user` | **5** | Admin cash-register **assignment** (`AssignUserAsync`) |
| `maxProductsPerTenant` | `max_products_per_tenant` | **10000** | Product create (Admin + POS catalog) |
| `maxUsersPerTenant` | `max_users_per_tenant` | **50** | User create / invite (tenant membership) |
| `dailyMaxTransactions` | `daily_max_transactions` | **1000** | POS sale (`PaymentService`) |
| `maxTransactionAmount` | `max_transaction_amount` | **10000** EUR | POS sale |
| `dailyMaxRevenue` | `daily_max_revenue` | **50000** EUR | POS sale (UTC day) |
| `maxBackupsPerTenant` | `max_backups_per_tenant` | **50** | Tenant backup trigger (succeeded tenant runs) |
| `maxBackupSizeMB` | `max_backup_size_mb` | **500** | Tenant backup trigger — **cumulative** LogicalDump size, not per-file |
| `maxOfflineTransactions` | `max_offline_transactions` | **50** | TSE offline **intent** queue (tenant-wide Pending + NonFiscalPending) |

Integer API lookups (`GetLimitValueAsync`) truncate money caps to whole units.

---

## Source of truth for offline queue

Enforcement reads **`tenant_limits.max_offline_transactions`** via `TenantLimitGuard.EnsureCanQueueOfflineTransactionAsync` / `ITenantLimitService`.

`TseOptions.MaxOfflineTransactionsPerCashRegister` is **obsolete**. It still binds from `appsettings` so existing config does not fail, but it is **not** used for the queue cap. Development / Demo / license-disabled hosts skip the cap (`LicenseEnforcementPolicy.ShouldSkipOfflineQueueCaps`).

---

## HTTP errors

All tenant-limit failures use HTTP **409** and `LimitErrorDto`:

```json
{
  "code": "LIMIT_EXCEEDED",
  "limitKey": "dailyMaxTransactions",
  "limit": 1000,
  "current": 1000,
  "message": "Daily transaction limit of 1000 reached",
  "canForce": false
}
```

Payment v2 envelopes set `code` / `context.diagnosticCode` to `LIMIT_EXCEEDED` and include the same DTO on `limitError`.

Internal service-string protocol (user create): `LIMIT_EXCEEDED|{limitKey}|{limit}|{current}|{message}`.

### UI error messages

FA and POS **do not** show the English `message` field. They map `code` + `limitKey` to i18n (`{{limit}}` / `{{current}}`) and add a short next step.

| Limit key | FA i18n | POS i18n | User message (DE, interpolated) |
|-----------|---------|----------|----------------------------------|
| `maxProductsPerTenant` | `tenants.limits.errors.maxProductsPerTenant` | — | Maximale Produktanzahl ({{limit}}) erreicht. Aktuell: {{current}}. Bitte löschen Sie nicht benötigte Produkte oder kontaktieren Sie den Administrator. |
| `maxUsersPerTenant` | `tenants.limits.errors.maxUsersPerTenant` | — | Maximale Benutzeranzahl ({{limit}}) erreicht. Aktuell: {{current}}. Bitte deaktivieren Sie nicht benötigte Benutzer oder kontaktieren Sie den Administrator. |
| `maxActiveRegistersPerUser` | `tenants.limits.errors.maxActiveRegistersPerUser` | — | Maximale Kassenanzahl pro Kassierer ({{limit}}) erreicht. Aktuell: {{current}}. Bitte heben Sie nicht benötigte Zuweisungen auf. |
| `maxBackupsPerTenant` | `tenants.limits.errors.maxBackupsPerTenant` | — | Maximale Backup-Anzahl ({{limit}}) erreicht. Aktuell: {{current}}. Bitte löschen Sie alte Backups. |
| `maxBackupSizeMB` | `tenants.limits.errors.maxBackupSizeMB` | — | Maximale Backup-Größe ({{limit}} MB) erreicht. Aktuell: {{current}} MB. Bitte löschen Sie alte Backups. |
| `dailyMaxTransactions` | `tenants.limits.errors.dailyMaxTransactions` | `payment:errors.limitDailyTransactions` | Tägliches Transaktionslimit ({{limit}}) erreicht. Aktuell: {{current}}. Bitte warten Sie bis morgen. |
| `maxTransactionAmount` | `tenants.limits.errors.maxTransactionAmount` | `payment:errors.limitTransactionAmount` | Maximaler Transaktionsbetrag ({{limit}} €) überschritten. Bitte reduzieren Sie den Betrag. |
| `dailyMaxRevenue` | `tenants.limits.errors.dailyMaxRevenue` | `payment:errors.limitDailyRevenue` | Tägliches Umsatzlimit ({{limit}} €) erreicht. Aktuell: {{current}} €. Bitte warten Sie bis morgen. |
| `maxOfflineTransactions` | `tenants.limits.errors.maxOfflineTransactions` | `payment:errors.limitOfflineQueue` | Offline-Warteschlange voll ({{current}}/{{limit}}). Bitte synchronisieren Sie die Warteschlange. |
| *(unknown key)* | `tenants.limits.errors.generic` | `payment:errors.limitExceeded` | Mandanten-Limit erreicht … |

FA wiring:

- Products create, user create / quick create → `toastLimitExceededOrFallback`
- Cash-register assignment → `notify.apiError` (global `LIMIT_EXCEEDED` translator)
- Tenant backup trigger → `triggerErrorMessageBackupDashboard`

en/tr catalogs live next to the DE strings (`frontend-admin/src/i18n/locales/*/tenants.json`, `frontend/i18n/locales/*/payment.json`).

`LimitErrorDto.canForce` remains SuperAdmin-only for assignment; the FA assignment field does not send `force=true` today.

---

## Super Admin override

`force=true` is **only** for cash-register **assignment** (`maxActiveRegistersPerUser`) and only when the actor is SuperAdmin (`AssignCashRegisterUserRequest.Force && actorIsSuperAdmin`).

`LimitErrorDto.CanForce` is `true` only for that key. Other caps have **no** override.

---

## APIs

| Method | Route | Who |
|--------|-------|-----|
| GET / PUT / POST reset | `/api/admin/tenants/{tenantId}/limits` | Super Admin |
| GET | `/api/admin/limits` | Ambient tenant usage (Manager + Super Admin) |
| GET | `/api/admin/limits/dashboard` | Manager (own tenant) + Super Admin (`allTenants=true` or no ambient tenant = all mandants; `tenantId` = one mandant) |

Usage DTO includes live counts: products, users, daily transactions/revenue, backups, cumulative backup MB, offline queue, peak assigned registers per user.

Dashboard DTO (`LimitDashboardDto`): `lastUpdated`, `summary` (healthy / warning / critical / total), per-limit progress (`Healthy` / `Warning` ≥80% / `Critical` ≥100%) with 7-day `trend` + `changeCount`, critical users (`Approaching` / `Full` / `Exceeded` for `maxActiveRegistersPerUser`), `recentActivity` (`LimitApproaching` / `LimitExceeded`), and `unreadAlertCount`.

Activity feed: `LimitApproaching` (warning) and `LimitExceeded` (error). Published on 409 enforcement and by `ActivityMonitoringHostedService` (deduped). FA page: `/admin/limits/dashboard` (Lizenzverwaltung).

Cache: `CacheKeys` tenant-limits entry, TTL `CacheSettings:TenantLimitsCacheMinutes` (default **5**). Invalidated on Super Admin PUT/reset.

---

## FA warnings

`LimitWarning` (80% threshold) on:

- Products → `maxProductsPerTenant`
- Users → `maxUsersPerTenant`
- Dashboard → `dailyMaxTransactions`, `dailyMaxRevenue`
- Backup (tenant view) → `maxBackupsPerTenant`, `maxBackupSizeMB`
- Cash-register detail → `maxActiveRegistersPerUser`
- Offline settings → `maxOfflineTransactions`
