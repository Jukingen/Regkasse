# Permissions Matrix

Default role grants from `RolePermissionMatrix` / `PermissionCatalog`.  
Implication rules: `PermissionImplication` (composites + manage→view + digital/website).

**Source of truth:** `backend/Authorization/AppPermissions.cs`, `RolePermissionMatrix.cs`, `PermissionImplication.cs`  
**FA mirror:** `frontend-admin/src/shared/auth/permissionImplication.ts` (+ `hasPermission` uses implication)  
**Guides:** [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md) · [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md) · [`AGENTS.md`](../AGENTS.md) § Roles  
**Last updated:** 2026-07-21

Legend: ✅ granted by default · ❌ not granted · *(via implication)* satisfied without an explicit matrix row.

---

## Digital Services

| Permission | SuperAdmin | Manager | Cashier |
|------------|------------|---------|---------|
| `digital.view` | ✅ | ✅ *(via `website.manage`)* | ❌ |
| `digital.preview` | ✅ | ✅ *(via `website.manage`)* | ❌ |
| `digital.request` | ✅ | ✅ *(via `website.manage`)* | ❌ |
| `digital.create` | ✅ | ❌ | ❌ |
| `digital.publish` | ✅ | ❌ | ❌ |
| `digital.edit` | ✅ | ❌ | ❌ |
| `digital.delete` | ✅ | ❌ | ❌ |
| `digital.manage` | ✅ | ❌ | ❌ |
| `digital.pricing.manage` | ✅ | ❌ | ❌ |
| `digital.activate` | ✅ | ❌ | ❌ |
| `digital.orders.view` | ✅ | ✅ *(via `digital.orders.manage`)* | ❌ |
| `digital.orders.manage` | ✅ | ✅ | ❌ |
| `digital.orders.approve` | ✅ | ❌ | ❌ |

Notes:

- **Manager** holds `website.manage` (domains / customization) — that implies view/preview/request only, **not** create/publish.
- Explicit Manager matrix rows for `digital.view` / `preview` / `request` / `digital.orders.view` were removed (2026-07-21); authorization still succeeds via implication.
- Legacy keys (`digital.web.*`, `digital.app.*`) remain in the catalog; prefer the simplified `digital.*` surface. Super Admin gets them via the full catalog / `digital.manage`.
- **Cashier / Waiter / Kitchen / Accountant / ReportViewer:** no `digital.*` / `digital.orders.*` by default.

---

## Online Orders (capabilities)

Mapped to permission keys used by `AdminOnlineOrdersController`.

| Capability | Permission | SuperAdmin | Manager | Cashier |
|------------|------------|------------|---------|---------|
| View orders / analytics / timeline | `digital.orders.view` | ✅ | ✅ | ❌ |
| Update status (forward steps) | `digital.orders.manage` | ✅ | ✅ | ❌ |
| Cancel orders (status → `cancelled`) | `digital.orders.manage` | ✅ | ✅ | ❌ |
| Accept → POS cart bridge | `digital.orders.approve` | ✅ | ❌ | ❌ |
| Hard-delete online orders | — | ❌ (no API) | ❌ | ❌ |

There is **no** `DELETE /api/admin/online-orders/{id}` endpoint. Cancellation is a status transition, not row deletion.

---

## Manager matrix thinning (2026-07-21)

Removed **redundant explicit grants** (still effective via `PermissionImplication`):

| Removed from Manager matrix | Still satisfied by |
|-----------------------------|--------------------|
| `user.reset.password` | `user.manage` |
| `cash_register.view` | `cash_register.manage` / `cash_register.decommission` |
| `daily-closing.view` | `daily-closing.execute` |
| `digital.view` / `preview` / `request` | `website.manage` |
| `digital.orders.view` | `digital.orders.manage` |

**Kept explicit:** `table.view` — `table.manage` is stripped from admin JWTs (POS terminal write strip), so Manager FA needs the view claim embedded.

Cashier / Waiter keep explicit `table.view` / `daily-closing.view` for POS JWT clients that may not expand implication.

---

## Catalog-only / reserved keys (unused gates)

Marked `[Obsolete]` in `AppPermissions` with planned removal after **2026-12-31**. Still registered in `PermissionCatalog` (SuperAdmin full set).

| Permission | Notes |
|------------|--------|
| `payment.export` | No controller; use `report.export` / payment list export paths |
| `report.schedule` | Schedulers (`AuditReportScheduler`, operational reports) do not check this key |
| `creditnote.create` | Live credit-note API uses `invoice.manage` |
| `tse.diagnostics` | No controller yet; SuperAdmin-only policy tests remain |

### Grant-only (matrix / strip; no live `[HasPermission]` yet)

Keep until features land or are explicitly cancelled: `kitchen.view`, `kitchen.update`, `cashdrawer.open/close`, `discount.apply`, `price.override`, `sale.cancel`.

### Tenants API

`/api/admin/tenants/*` is gated by **role `SuperAdmin`**, not `tenant.manage`. Catalog `tenant.*` keys remain for FA / future permission-based tenancy.

### Backup

Live gate is **`backup.manage`**. `settings.backup` is a composite child of `settings.manage` only.

---

## Implication summary

| Holding | Also satisfies (selected) |
|---------|---------------------------|
| `system.critical` | Entire catalog (compact SuperAdmin JWT) |
| `*.manage` (user/product/category/…) | Matching `*.view` (one-way) |
| `user.manage` | Granular `user.create/edit/delete/change.*` / `reset.password` |
| `digital.manage` | All `digital.*` simplified + legacy web/app + `digital.orders.*` |
| `digital.orders.manage` | `digital.orders.view` |
| `digital.orders.approve` | `digital.orders.view` + `digital.orders.manage` |
| `website.manage` | `digital.view` / `preview` / `request` (+ legacy view/preview/request) — **not** create/publish/delete/orders |
| `cash_register.manage` | `cash_register.view` |
| `daily-closing.execute` | `daily-closing.view` |
| `settings.manage` | `settings.backup`, `backup.manage`, `website.manage` (+ `settings.view`) |

`PermissionClaimHelper` and `RolePermissionMatrix.RoleHasPermission` use `PermissionImplication.IsSatisfied` (aligned with `PermissionAuthorizationHandler`).

---

## FA route gates (typical)

| Route | Typical permission |
|-------|-------------------|
| `/settings/digital`, website preview | `digital.view` / `digital.preview` |
| Digital request actions | `digital.request` |
| `/admin/digital`, generators, publish | `digital.create` / `publish` / `manage` (Super Admin) |
| `/admin/digital/requests` | `digital.manage` |
| `/orders/online`, `/tenant/{id}/orders` | `digital.orders.view` (legacy `order.view` may still open list in FA) |
| Status buttons | `digital.orders.manage` |
| Accept → POS | `digital.orders.approve` |

Cross-tenant access still returns **HTTP 404** (not 403), even when the permission key is present.
