# Inventory / Lager — optional use (short guide)

This document is an operations summary for deployments that **turn off** stock and Lager surfaces. Schema and product fields stay; only behavior and UI are configured.

## Recommended “Lager off” package

1. **API (no sales block):** `Inventory__EnforceStockAvailability=false`
   - or `Inventory:EnforceStockAvailability`: `false` in appsettings / environment
   - Payment does not check stock or decrement/restore stock; the receipt flow does not depend on inventory.

2. **Admin — product list:** `NEXT_PUBLIC_ADMIN_PRODUCTS_SHOW_LAGER=false`
   - Hides the Lager column, stock button, and low-stock tags on the products table.
   - Must be set **before build** for `frontend-admin` (`next build`).

3. **Admin — Lager module:** `NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV=false`
   - Hides the “Lager” sidebar entry; opening `/inventory` directly shows an info message and does not fire inventory API calls.
   - Also **before build**.

Restart the API after the API change. If admin env changed, rebuild the admin app.

### When a change takes effect (important)

| Change | When it applies |
|--------|-----------------|
| `Inventory__EnforceStockAvailability` (or appsettings) | When the API process **restarts** (and the config is loaded). |
| `NEXT_PUBLIC_ADMIN_*` | In the client bundle from the **next** `next dev` / `next build`; injecting runtime env into a running container is **not** enough. |

## Smoke test checklist (Lager off package)

Prerequisite: API `EnforceStockAvailability=false`; admin `.env.local` or CI has `NEXT_PUBLIC_ADMIN_PRODUCTS_SHOW_LAGER=false` and `NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV=false`; then **admin rebuild**, **API restart**.

- [ ] **Sale:** Payment from POS completes for a normal product (not an add-on) with stock `0`; API does not return “Insufficient stock”.
- [ ] **Products:** `/products` table has **no** Lager column and **no** Lager/stock row action.
- [ ] **Sidebar:** Catalog group has **no** Lager / Inventory menu item.
- [ ] **Dashboard:** Hospitality quick-links card has **no** Stock / Lager link (when env is off).
- [ ] **Direct URL:** Opening `/inventory` shows the info message; Network has **no** `/api/Inventory` request (or it is not fired on page load).
- [ ] **API-only:** Set `EnforceStockAvailability` back to `true`, restart the API, and confirm the same stock-0 scenario is rejected (regression check).

## Defaults (backward compatibility)

- API: `EnforceStockAvailability` **true** (previous stock behavior).
- Admin: `NEXT_PUBLIC_*` unset or `true` → Lager surfaces visible.

## Related files (developers)

- `backend/Configuration/InventoryOptions.cs`, `PaymentService` stock branches
- `frontend-admin/src/shared/config/adminInventoryNavUi.ts`, `buildAdminSidebar.tsx`
- `frontend-admin/src/features/products/utils/adminProductsLagerUi.ts`, `products/page.tsx`
- `backend/appsettings.example.json`, `backend/CONFIGURATION.md`
