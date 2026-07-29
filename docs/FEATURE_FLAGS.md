# Feature flags

**Last updated:** 2026-07-29  
**Related:** [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) · [`DEVELOPMENT.md`](../DEVELOPMENT.md)

Ship code with new behavior **off by default**, then enable gradually (global or per tenant) without redeploying binaries.

---

## 1. Configuration defaults

`appsettings*.json` / env:

```json
"FeatureFlags": {
  "EnableNewPaymentFlow": false,
  "EnableDepExportV2": false,
  "EnableOnlineOrdersV2": false,
  "EnableAutoAusfall": false
}
```

| Flag | Purpose |
|------|---------|
| `EnableNewPaymentFlow` | Instrumented / alternate payment path hook in `PaymentService` |
| `EnableDepExportV2` | DEP export response header `X-Regkasse-Dep-Export-Schema: v2` (BMF JSON body unchanged) |
| `EnableOnlineOrdersV2` | Online order intake V2 marker (`intakeVersion: "v2"`) |
| `EnableAutoAusfall` | Allows TSE failover auto-enqueue **only if** `Ausfall:AutoEnqueue=true` |

Env override example: `FeatureFlags__EnableAutoAusfall=true`.

---

## 2. Resolution order

Effective value for a flag:

1. **Tenant override** in `tenant_settings` (`key = FeatureFlags:{Name}`, `tenant_id = {guid}`)
2. Else **global override** (`tenant_id` null)
3. Else **config default** from `FeatureFlags`

Unknown names → `false`.

Short names work in code: `IsEnabled("NewPaymentFlow")` → `EnableNewPaymentFlow`.

---

## 3. Service API

```csharp
public interface IFeatureFlagService
{
    bool IsEnabled(string featureName, string? tenantId = null);
    Task SetEnabledAsync(string featureName, bool enabled, string? tenantId = null, ...);
    Task ClearOverrideAsync(string featureName, string? tenantId = null, ...);
    Task<IReadOnlyList<FeatureFlagStatusDto>> GetStatusesAsync(string? tenantId = null, ...);
}
```

Usage:

```csharp
if (_featureFlags.IsEnabled(FeatureFlagNames.EnableNewPaymentFlow, tenantId.ToString("D")))
{
    // new path
}
else
{
    // legacy path
}
```

Canonical names: `FeatureFlagNames` in `backend/Services/FeatureFlags/`.

---

## 4. Admin API (Super Admin / `system.critical`)

| Method | Path | Notes |
|--------|------|--------|
| `GET` | `/api/admin/feature-flags?tenantId=` | List effective flags |
| `PUT` | `/api/admin/feature-flags` | Body: `{ name, enabled, tenantId?, clearOverride? }` |
| `GET` | `/api/admin/feature-flags/{name}/enabled?tenantId=` | Quick check |

Changes are audited (`AuditEventType.FeatureFlagChanged`).

---

## 5. FA UI

- Route: `/admin/feature-flags` (Verwaltung sidebar)
- Permission: Super Admin / `system.critical`
- Optional tenant UUID field scopes overrides; empty = global

---

## 6. Storage

Table `tenant_settings` (migration `20260729230000_AddTenantSettings`):

| Column | Notes |
|--------|--------|
| `tenant_id` | null = global |
| `key` | e.g. `FeatureFlags:EnableDepExportV2` |
| `value` | `true` / `false` |
| `updated_at_utc` / `updated_by_user_id` | audit helpers |

---

## 7. Promotion pattern

1. Merge feature behind flag (default `false`).
2. Deploy to Staging / Production with flag off.
3. Enable globally or for canary tenants via FA / API.
4. Monitor metrics / errors.
5. Flip remaining tenants; later remove flag + dead code.

---

## 8. Tests

```bash
cd backend && dotnet test --filter "FullyQualifiedName~FeatureFlag"
```
