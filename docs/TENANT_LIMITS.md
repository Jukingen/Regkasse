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
| GET | `/api/admin/limits/dashboard` | `license.manage`. Super Admin: `allTenants=true` or no ambient tenant = all mandants; `tenantId` = one mandant. Mandanten-Admin (`Manager`): ambient tenant only — see [Limit Dashboard tenant targeting](#limit-dashboard-tenant-targeting) |

Usage DTO includes live counts: products, users, daily transactions/revenue, backups, cumulative backup MB, offline queue, peak assigned registers per user.

Dashboard DTO (`LimitDashboardDto`): `lastUpdated`, `summary` (healthy / warning / critical / total), per-limit progress (`Healthy` / `Warning` ≥80% / `Critical` ≥100%) with 7-day `trend` + `changeCount`, critical users (`Approaching` / `Full` / `Exceeded` for `maxActiveRegistersPerUser`), `recentActivity` (`LimitApproaching` / `LimitExceeded`), and `unreadAlertCount`.

Activity feed: `LimitApproaching` (warning) and `LimitExceeded` (error). Published on 409 enforcement and by `ActivityMonitoringHostedService` (deduped). FA page: `/admin/limits/dashboard` (Lizenzverwaltung).

Cache: `CacheKeys` tenant-limits entry, TTL `CacheSettings:TenantLimitsCacheMinutes` (default **5**). Invalidated on Super Admin PUT/reset.

### Limit Dashboard tenant targeting

`GET /api/admin/limits/dashboard` (`license.manage`). FA: `/admin/limits/dashboard`.

| Actor | Query | Behaviour |
|-------|-------|-----------|
| SuperAdmin | `?tenantId={guid}` | Loads that mandant (404 if the tenant does not exist) |
| SuperAdmin | `?allTenants=true` | Aggregates all active, non-deleted mandants |
| SuperAdmin | none | Ambient tenant if set; otherwise all mandants (same as `allTenants=true`) |
| Mandanten-Admin (`Manager`) | `?tenantId=…` | **Ignored.** HTTP **200** with the **ambient** tenant dashboard. Foreign `tenantId` is never loaded |
| Mandanten-Admin (`Manager`) | none | Ambient tenant. No ambient tenant → HTTP **404** (`Tenant context is required.`) |
| Mandanten-Admin (`Manager`) | `?allTenants=true` | Ignored. Ambient tenant only |
| Cashier | any | No `license.manage` — menu hidden; route **403** |

Mandanten-Admin sending `tenantId` for another mandant is **not** a cross-tenant 404. Isolation is fail-closed by **ignoring** the query and returning the caller’s own dashboard (HTTP 200). This is intentional: the actor is allowed to read their own limits; the extra query must not retarget the read.

FA does not send `tenantId` / `allTenants` for non–SuperAdmin. Super Admin may pick one mandant or “all tenants” in the context bar.

---

## Development Limit Test Panel

QA surface for Super Admin to move caps relative to live usage (no fiscal test rows). Not a Production feature.

| Surface | Path |
|---------|------|
| FA page | `/admin/development/limits` (sidebar **Entwicklung → Limit Test**, `developmentOnly`) |
| API | `/api/dev/limits/*` (`DevLimitTestController`, `[Authorize(Roles = SuperAdmin)]`, hidden from OpenAPI) |

### Access

| Actor | Environment | Menu | Page | API |
|-------|-------------|------|------|-----|
| SuperAdmin | Development (`NODE_ENV=development` + `ASPNETCORE_ENVIRONMENT=Development`) | Visible | Panel | 200 when `tenantId` is valid |
| SuperAdmin | Production | Hidden (`developmentOnly`) | UI **404** (`NotFoundAccessView`) | HTTP **404** (`EnsureNotDevelopment`) |
| Mandanten-Admin (`Manager`) | Development | Hidden (`system.critical` required) | FA **403** (`ForbiddenAccessView`) | HTTP **403** (role) |
| Cashier | Development | Hidden | FA **403** | HTTP **403** (role) |

Mandanten-Admin / Cashier opening `/admin/development/limits` is **403**, not 404. That is expected: the route is gated by `system.critical` / SuperAdmin role, not by tenant isolation.

### Tenant validation on `/api/dev/limits`

`/api/dev/limits` is **not** in `TenantValidationMiddleware` SuperAdmin platform exemptions (`/api/admin/limits` is). Super Admin still needs an **ambient** tenant (JWT / `X-Tenant-Id` in Development). Missing ambient tenant → HTTP **404** before the controller. The panel then targets a mandant via explicit `tenantId` query/body (`IgnoreQueryFilters` on usage/caps).

### Scenarios

`POST /api/dev/limits/scenario/trigger` (`near` / `at` / `tiny` / `reset`) via `DevLimitScenarioPlanner`. Caps move relative to live usage; no payments/backups/offline rows are created.

| Scenario | Effect |
|----------|--------|
| `near` | Cap ≈ usage / 0.8 (warning band). Zero usage → integer **5** (money keys use a small fallback) |
| `at` | Cap = current usage (`Math.Max(1, current)` when usage is 0) |
| `tiny` | Cap = 1 (money 1.00) |
| `reset` | Defaults (`ResetLimitsAsync` when no `limitKey`) |

Other routes: `GET …/status`, `POST …/set`, `POST …/reset-all`, `POST …/cache/clear`.

---

## FA warnings

`LimitWarning` (80% threshold) on:

- Products → `maxProductsPerTenant`
- Users → `maxUsersPerTenant`
- Dashboard → `dailyMaxTransactions`, `dailyMaxRevenue`
- Backup (tenant view) → `maxBackupsPerTenant`, `maxBackupSizeMB`
- Cash-register detail → `maxActiveRegistersPerUser`
- Offline settings → `maxOfflineTransactions`
