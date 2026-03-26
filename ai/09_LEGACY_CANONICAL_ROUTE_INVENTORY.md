# Legacy vs Canonical API Route Inventory

**Purpose:** Single place to track duplicate (legacy + canonical) routes, intended replacements, consumers, response-shape drift, removal risk, and migration blockers.  
**Last reviewed:** 2026-03-25 (repository scan: `backend/Controllers`, `frontend/services/api`, `frontend-admin/src/api/generated`, `orval-strip-legacy-paths.cjs`).  
**Related:** `ai/08_API_CONTRACT_STABILIZATION_PLAN.md`

---

## Definitions

| Term | Meaning in this repo |
|------|----------------------|
| **Canonical (target)** | `/api/admin/*` (Admin), `/api/pos/*` (POS) — intentional API boundary. |
| **Legacy alias** | Second route on the **same controller actions** as canonical (e.g. `api/Payment` mirrors `api/pos/payment`). Safe removal = drop `[Route("api/[controller]")]` when traffic is zero. |
| **Policy gap (not a duplicate)** | PascalCase controllers (`api/Tse`, `api/UserManagement`, …) — **one** surface today; not duplicated under `/api/admin/*` yet. Migration = new admin routes + client switch, not “alias removal”. |
| **Shared** | Used by both apps (e.g. `api/user/settings`, `api/Auth`). |

---

## A) Legacy aliases (same handler, two path prefixes)

### A1. Payment — `api/Payment` → `api/pos/payment`

| Item | Detail |
|------|--------|
| **Legacy** | `/api/Payment/*` |
| **Canonical** | `/api/pos/payment/*` | 
| **Backend** | `PaymentController`: `[Route("api/[controller]")]` + `[Route("api/pos/payment")]` |
| **Consumers** | **POS:** `paymentService` / `posPaymentPaths` → **canonical only** (no direct `/api/Payment` in POS code). **Admin:** `frontend-admin/src/api/legacy/payment.ts` → `/api/Payment/*` (intentional legacy wrapper). **Orval:** `/api/Payment` stripped from spec (`orval-strip-legacy-paths.cjs`). |
| **Response shape** | Legacy POST: flat `success`, `paymentId`, `payment`, `invoicePersisted`, `idempotentReplay`, `tse`. **v2** (header `X-Regkasse-Payment-Contract: v2`): `PaymentApiEnvelope` / `PaymentApiErrorBody`. |
| **Removal risk** | **Medium** — legacy hooks + external scripts may still hit `/api/Payment`. |
| **Migration blockers** | Admin pages must stop `legacy/payment`; metrics must show near-zero legacy hits; sunset date aligned with `LegacyRouteDeprecationFilter`. |

---

### A2. Cart — `api/Cart` → `api/pos/cart`

| Item | Detail |
|------|--------|
| **Legacy** | `/api/Cart/*` |
| **Canonical** | `/api/pos/cart/*` |
| **Backend** | `CartController`: `[Route("api/[controller]")]` + `[Route("api/pos/cart")]` |
| **Consumers** | **POS:** `cartService.ts` → `/pos/cart/...` (`apiClient` baseURL → `/api/pos/cart/*`). **Admin:** Orval-generated `cart.ts` still emits **`/api/Cart/*`** (full legacy paths in `generated/cart`). **Also:** `generated/pos/pos.ts` uses **`/api/pos/cart/*`**. |
| **Response shape** | Mostly `SuccessResponse` / `ErrorResponse` patterns; cart-specific payloads (`message`, cart DTOs). Same code path for both prefixes → **same JSON** per action. |
| **Removal risk** | **Medium** — Admin generated client references `/api/Cart`; stripping Orval without replacing usage breaks types/usages. |
| **Migration blockers** | Admin: migrate any `cart` tagged hooks to **pos** paths or **strip** cart from Orval if unused; verify no runtime calls to `/api/Cart`. Deprecation headers/metrics: `LegacyRouteDeprecationFilter` + `legacy_route_hits_total` (`legacy_family=cart`). |

---

### A3. Product — `api/Product` → `api/pos` (catalog) + `api/admin/products` (management)

| Item | Detail |
|------|--------|
| **Legacy** | `/api/Product`, `/api/Product/*` |
| **Canonical** | **POS catalog:** `/api/pos/*` (same `ProductController`). **Admin CRUD:** `/api/admin/products/*` (`AdminProductsController`). |
| **Backend** | `ProductController`: `[Route("api/Product")]` + `[Route("api/pos")]` |
| **Consumers** | **POS:** `productService` + `API_PATHS.PRODUCT.*` → `/pos/...` (canonical). **Admin:** Orval **strips** `/api/Product` from input spec; uses **`/api/admin/products`** in `generated/admin`. |
| **Response shape** | List/detail DTOs identical per action; admin vs POS differ by **which** actions are used (CRUD vs catalog). |
| **Removal risk** | **Medium** — unknown API clients may still call `/api/Product`. |
| **Migration blockers** | Confirm no external consumers; Swagger `deprecated` already marks legacy; optional: same deprecation filter as Payment. |

---

## B) Policy gap: single surface, not under `/api/admin/*` or `/api/pos/*`

These are **not** legacy aliases; they are the **legacy naming** of Admin (and sometimes shared) APIs. Canonical policy is to converge on `/api/admin/*` where appropriate. **Parallel** controllers exist for some domains (e.g. `AdminUsersController` vs `UserManagementController`).

| Route family | Controller | Typical consumer | Canonical direction | Notes |
|--------------|------------|------------------|---------------------|--------|
| `/api/UserManagement/*` | `UserManagementController` | Admin Orval `user-management.ts` | `/api/admin/users/*` (`AdminUsersController`) — **partial overlap** | Parity matrix required before switching off UserManagement. |
| `/api/Tse/*` | `TseController` | Admin Orval `tse.ts` | Future `/api/admin/tse/*` (or keep with explicit exception) | **High compliance risk** — do not rename casually. |
| `/api/Tagesabschluss/*` | `TagesabschlussController` | Admin | Future `/api/admin/tagesabschluss/*` | Same. |
| `/api/FinanzOnline/*` | `FinanzOnlineController` | Admin | Overlap with some `api/admin/finanzonline-reconciliation/*` | Two surfaces; reconcile deliberately. |
| `/api/AuditLog/*` | `AuditLogController` | Admin | Future `/api/admin/audit-log/*` | Large surface. |
| `/api/Settings/*` | `SettingsController` | Admin | Future `/api/admin/settings/*` | |
| `/api/Orders/*` | `OrdersController` | Admin Orval `orders.ts`, POS `orderService` | POS uses `/api/orders`; canonical split TBD | Both apps use. |
| `/api/Receipts/*` | `ReceiptsController` | Admin + POS | Shared; path may stay | |
| `/api/Invoice/*` | `InvoiceController` | POS `invoiceService` (PDF, etc.) | Shared | |
| `/api/CashRegister/*` | `CashRegisterController` | Admin inventory; **not** POS pickers | POS uses `/api/pos/cash-register/*` | Documented in `cashRegisterService.ts`. |
| `/api/Test/*` | `TestController` | Admin / dev | Internal only | Non-prod or restricted. |

**Gap:** No row in this table should be treated as “drop legacy alias” until **replacement** exists and **clients** migrate.

---

## C) Canonical-only or special paths (no legacy duplicate listed above)

| Route | Role | Consumers |
|-------|------|-----------|
| `/api/admin/*` | Admin boundary | Admin app, Orval `admin.ts` |
| `/api/pos/cash-register/*` | POS register assignment | POS `posCashRegisterReadinessService`, `cashRegisterService` (selectable) |
| `/api/user/settings` | Shared profile/settings | Admin + POS Orval / services |
| `/api/modifier-groups` | Modifier groups | **Verify** — not under `/api/pos` or `/api/admin` in controller |
| `/api/offline-transactions` | Offline sync | `OfflineTransactionsController` — likely POS / internal |

---

## D) POS / unknown: paths in code without backend match in this scan

| Path in `frontend` | Backend controller (this repo) | Risk |
|---------------------|--------------------------------|------|
| `/api/pending-invoices` | **Not found** | Dead code, stub, or removed API — **verify** |
| `/api/network` | **Not found** | Same |
| `/api/coupon/*` | **Not found** | Same |
| `/api/orders` | `OrdersController` → `api/Orders` | **Case mismatch** — POS uses lowercase `/api/orders`; ASP.NET routing is often case-insensitive but document **exact** URL |

---

## E) Response shape mismatches (summary)

| Area | Drift |
|------|--------|
| **Payment POST** | Legacy flat body vs `PaymentApiEnvelope` v2 (header opt-in). `BaseController` success wrapper `{ success, message, data, timestamp }` vs raw create response. |
| **Payment GET** | `SuccessResponse` wraps payment in `data` — POS `normalizePaymentResponse` compensates. |
| **Admin payments** | `AdminPaymentsController` returns typed DTOs / `AdminPaymentActionResponse` — different from POS payment envelope. |
| **Cart / generic** | Mix of `SuccessResponse`, `ErrorResponse`, `message` + `errors` arrays. |

---

## F) Migration blockers (global)

1. **Metrics:** Unified Prometheus counter `legacy_route_hits_total{legacy_family,route_pattern,http_method}` for payment, cart, and product legacy aliases.  
2. **Admin Orval:** Generated `cart.ts` still points at `/api/Cart/*` — must migrate or strip + fix imports.  
3. **UserManagement vs AdminUsers:** Feature parity and RBAC/audit before switching generated client.  
4. **External / mobile:** Old app builds or scripts not visible in repo — **access logs** required before alias removal.  
5. **Compliance:** TSE / FinanzOnline / receipt routes — **no route-only change** without impact review.

---

## G) Acceptance criteria mapping

| Criterion | Where satisfied |
|-----------|-----------------|
| Route inventory document exists | This file: `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` |
| Each legacy route has canonical **or** explicit gap | Sections **A** (aliases), **B** (policy gap), **C** (special) |
| Known consumer information | Per-row **Consumers** + **D** (unknowns) |
| Response shape mismatches | Section **E** + per-alias notes |
| Migration blockers | Per-alias **Migration blockers** + **F** |

---

## H) Maintenance

- **Update this doc** when: a controller `[Route]` changes, Orval transformer `LEGACY` list changes, or a new duplicate prefix is introduced.  
- **Owner:** API / platform (suggested).  
- **Optional next step:** Export section A as a CSV for spreadsheet tracking (same columns).
