# Permissions Matrix — Digital Services & Online Orders

Default role grants from `RolePermissionMatrix` / `PermissionCatalog`.  
Implication rules: `PermissionImplication` (`digital.manage` → all digital + `digital.orders.*`).

**Source of truth:** `backend/Authorization/AppPermissions.cs`, `RolePermissionMatrix.cs`  
**Guides:** [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md) · [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md) · [`AGENTS.md`](../AGENTS.md) § Roles  
**Last updated:** 2026-07-19

Legend: ✅ granted by default · ❌ not granted · *(via `digital.manage`)* Super Admin holds the full catalog, so individual keys are satisfied even when only `digital.manage` is checked after implication.

---

## Digital Services

| Permission | SuperAdmin | Manager | Cashier |
|------------|------------|---------|---------|
| `digital.view` | ✅ | ✅ | ❌ |
| `digital.preview` | ✅ | ✅ | ❌ |
| `digital.request` | ✅ | ✅ | ❌ |
| `digital.create` | ✅ | ❌ | ❌ |
| `digital.publish` | ✅ | ❌ | ❌ |
| `digital.edit` | ✅ | ❌ | ❌ |
| `digital.delete` | ✅ | ❌ | ❌ |
| `digital.manage` | ✅ | ❌ | ❌ |
| `digital.pricing.manage` | ✅ | ❌ | ❌ |
| `digital.activate` | ✅ | ❌ | ❌ |
| `digital.orders.view` | ✅ | ✅ | ❌ |
| `digital.orders.manage` | ✅ | ✅ | ❌ |
| `digital.orders.approve` | ✅ | ❌ | ❌ |

Notes:

- **Manager** may also hold `website.manage` (domains / customization) — that implies view/preview/request only, **not** create/publish.
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

## Implication summary

| Holding | Also satisfies (selected) |
|---------|---------------------------|
| `digital.manage` | All `digital.*` simplified + legacy web/app keys + all `digital.orders.*` |
| `digital.orders.manage` | `digital.orders.view` |
| `digital.orders.approve` | `digital.orders.view` + `digital.orders.manage` |
| `website.manage` | `digital.view` / `preview` / `request` (+ legacy view/preview/request) — **not** create/publish/delete/orders |

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
