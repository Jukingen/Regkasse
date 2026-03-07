# Legacy API Boundary – Deliverable

## What was changed

1. **Legacy boundary (Payment)**  
   - Added `src/api/legacy/` with a dedicated Payment module.  
   - All admin usage of `/api/Payment/*` is intended to go through `@/api/legacy/payment` (query keys, `useLegacyPaymentList`, `useLegacyPaymentById`).  
   - Payments page no longer imports from `@/api/generated/payment/payment`; it uses `useLegacyPaymentList` with a default date range and correctly handles the backend response shape `{ items, pagination }`.  
   - Fixed the broken `useGetApiPayment` reference (that hook does not exist in the generated client); the page now uses the date-range endpoint.

2. **Admin boundary (Products & Categories)**  
   - Documented the stable admin API in `src/api/admin/README.md`.  
   - Confirmed Products and Categories use only `/api/admin/products` and `/api/admin/categories` via `src/api/admin/products.ts` and `src/api/admin/categories.ts`.  
   - No code changes to these modules; they already form the stable boundary.

3. **Language**  
   - Replaced Turkish comments with English in:  
     `src/api/admin/products.ts`, `src/api/admin/categories.ts`,  
     `src/features/products/components/ProductForm.tsx`, `src/features/products/hooks/useProducts.ts`.

4. **Dev-only legacy detection**  
   - In `src/lib/axios.ts`, added a non-breaking request interceptor that logs a **warning** in development when a request URL matches `/api/Payment` or `/api/Cart`.  
   - No build or runtime enforcement; purely informational.

5. **Docs**  
   - `src/api/legacy/README.md`: scope (Payment, Cart, Product/Categories, signature-debug), rules, and that new legacy usage must go through this boundary.  
   - `src/api/admin/README.md`: stable boundary for admin products and categories.

---

## Files modified

| File | Change |
|------|--------|
| `src/api/legacy/README.md` | New. Legacy boundary scope and rules. |
| `src/api/legacy/payment.ts` | New. Query keys, `useLegacyPaymentList`, `useLegacyPaymentById`, re-exports. |
| `src/api/legacy/index.ts` | New. Re-exports from `payment`. |
| `src/app/(protected)/payments/page.tsx` | Switched to `useLegacyPaymentList`, fixed response shape (`items` + `pagination`), removed invalid `useGetApiPayment` import. |
| `src/api/admin/README.md` | New. Stable admin products/categories boundary. |
| `src/api/admin/products.ts` | Comment only: Turkish → English. |
| `src/api/admin/categories.ts` | Comment only: Turkish → English. |
| `src/features/products/components/ProductForm.tsx` | Comment only: Turkish → English. |
| `src/features/products/hooks/useProducts.ts` | Comment only: Turkish → English. |
| `src/lib/axios.ts` | Dev-only `console.warn` when URL matches `/api/Payment` or `/api/Cart`. |
| `docs/LEGACY_API_BOUNDARY_DELIVERABLE.md` | New. This deliverable. |

---

## Legacy paths still intentionally left in place

- **Generated clients (Orval)**  
  - `src/api/generated/payment/payment.ts` – still contains all `/api/Payment/*` endpoints.  
  - `src/api/generated/cart/cart.ts` – still contains all `/api/Cart/*` endpoints.  
  They are not removed; admin should consume them only via `src/api/legacy/*` (Payment) or a future Cart wrapper if needed.

- **Signature-debug**  
  - `GET /api/Payment/{paymentId}/signature-debug` is still called from `src/features/receipts/api/signature-debug.ts` and used by `useSignatureDebugQuery`.  
  - Intentionally not moved or auto-migrated (debug/fiscal); documented in `src/api/legacy/README.md` and below as high-risk.

- **Other generated usage**  
  - Dashboard and reports use `@/api/generated/reports/reports` (e.g. `useGetApiReportsPayments` for `/api/Reports/payments`).  
  - No change; Reports are not in scope as legacy Payment/Cart namespaces.

---

## High-risk endpoints requiring manual review

| Endpoint | Used by | Note |
|----------|---------|------|
| `GET /api/Payment/{id}/signature-debug` | `src/features/receipts` (`fetchSignatureDebug`) | Debug/diagnostic; RKSV. Do not auto-migrate. |
| `POST /api/Payment` (create), `POST /api/Payment/{id}/cancel`, `POST /api/Payment/{id}/refund`, `GET /api/Payment/{id}/receipt`, `POST /api/Payment/{id}/tse-signature` | Generated only; no current admin UI usage observed | Fiscal/receipt-related; any future admin use should go through a dedicated boundary and manual review. |
| `/api/Cart/*` (all) | Generated only; no admin imports | If admin ever uses Cart, add `src/api/legacy/cart.ts` and route all usage through it. |

---

## Recommended next implementation step

1. **Short term**  
   - When adding new admin screens that need payment or cart data, use only `@/api/legacy/payment` (and a future `@/api/legacy/cart` if needed).  
   - Do not add new direct imports from `@/api/generated/payment/payment` or `@/api/generated/cart/cart` in pages or features.

2. **Backend alignment**  
   - Once backend exposes admin/pos endpoints (e.g. `GET /api/admin/payments` or `GET /api/pos/...`), add corresponding modules under `src/api/admin/*` (or a dedicated pos client) and migrate `useLegacyPaymentList` (and any other legacy hooks) to the new endpoints behind the same or new query keys.

3. **Signature-debug**  
   - Leave as-is until there is an explicit requirement and a replacement endpoint/contract. Then migrate with manual review and tests.

4. **Cart**  
   - If admin starts using Cart, introduce `src/api/legacy/cart.ts` (query keys + wrapper hooks) and route all Cart usage through it, without changing backend contracts in this step.
