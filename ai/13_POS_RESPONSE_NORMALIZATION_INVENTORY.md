# POS response normalization — inventory & reduction plan

**Purpose:** Single inventory of client-side shaping/adapters for inconsistent backend responses; define what can drop after contract alignment (`/api/pos/*`, payment v2 envelope).  
**Related:** `ai/08_API_CONTRACT_STABILIZATION_PLAN.md`, `ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`, `backend/DTOs/PaymentApiContractDtos.cs`  
**Last updated:** 2026-03-25

---

## 1. Inventory (by area)

| # | Location | What it does | Trigger / driver | After contract alignment |
|---|----------|----------------|------------------|---------------------------|
| A | `services/api/paymentService.ts` → `normalizePaymentResponse` | Unwraps `Value`, merges `success`/`paymentId`/`tse` from flat vs `SuccessResponse` vs nested `payment` | Legacy POST body + `BaseController.SuccessResponse` for GET | **Remove most branches** when POS opts into `X-Regkasse-Payment-Contract: v2` and parses `PaymentApiEnvelope` only |
| B | `services/api/normalizePosPaymentMethods.ts` | `normalizeToPosPaymentMethods`, `unwrapApiResponseLayer` | GET `/api/pos/payment/methods` (and similar) row shape / `type` vs `Type` | **Narrow** when API returns stable camelCase array only |
| C | `utils/paymentTaxType.ts` | Maps cart numeric/string tax → `standard`/`reduced`/`special` for POST body | Cart lines may expose enum int or legacy strings | **Keep** until cart API returns only string enum matching payment contract (or document single source in OpenAPI) |
| D | `services/api/normalizeUserSettingsResponse.ts` | `flattenUserSettingsPayload`, `unwrapApiResponseLayer` loop | `GET/PUT /api/user/settings` may envelope payload | **Simplify** when settings always return flat JSON (no `data`/`Value` wrapper) |
| E | `services/api/invoiceService.ts`, `cashRegisterService.ts` | Use `unwrapApiResponseLayer` from (B) | Shared success envelope | Same as D |
| F | `components/PaymentModal.tsx` → `normalizeReceiptDto` | PascalCase/camelCase for receipt preview object | Receipt payload shape drift | **Replace** with typed DTO when receipt endpoint schema is stable |
| G | `services/receiptPrinter.ts` → `normalizeReceiptDTO` | PascalCase → camelCase for print HTML | Same | Align with F or single `ReceiptDTO` from spec |
| H | `features/payment/paymentErrors.ts` → `normalizePaymentError` | Maps axios errors to UI messages | All payment errors | **Tighten** when `PaymentApiErrorBody` with `code` is always returned for failures |
| I | `services/payment/pendingPaymentQueue.ts` | `normalizeEntry`, legacy AsyncStorage key migration | Offline queue versioning | **Keep** for storage migration; not HTTP shaping |
| J | `i18n/localeUtils.ts`, `AuthContext` | Locale string normalization | UX | **Keep** (not API contract) |
| K | `app/(screens)/invoices.tsx` | Status string → label | Invoice list display | **Keep** (presentation) |

---

## 2. Canonical routes (POS)

These services already target **`/api/pos/*`** (see `apiPaths.ts`, `posPaymentPaths.ts`, `cartService.ts`):

- Products / catalog: `/api/pos/...`
- Cart: `/api/pos/cart/...`
- Payment: `/api/pos/payment/...`

**Normalization is not about wrong path** — it is about **response JSON shape** (envelope, casing, optional wrappers).

---

## 3. Acceptance criteria mapping

| Criterion | How addressed |
|-----------|----------------|
| **Normalization inventory exists** | Section 1 table |
| **Contract-aligned routes without manual shaping** | Section 4 (target state) — payment POST is the main remaining gap until v2 is default + client uses one parser |
| **Critical POS flows stable** | Changes must be **incremental**: feature-flag v2 header, run `frontend` Jest tests (`paymentService.taxTypeNormalization`, `normalizePosPaymentMethods`), manual payment smoke |

---

## 4. Target state (incremental)

1. **Payment create (POST):**  
   - Send `X-Regkasse-Payment-Contract: v2` from `paymentService` (config-gated).  
   - Implement single parse path for `PaymentApiEnvelope<PaymentCreateSuccessData>` / `PaymentApiErrorBody`.  
   - Retain legacy `normalizePaymentResponse` behind flag until metrics show only v2.

2. **Payment methods (GET):**  
   - Backend documents fixed array schema → reduce `normalizeToPosPaymentMethods` to validation-only.

3. **Cart tax (C):**  
   - Optional OpenAPI alignment: cart DTO uses same `taxType` strings as payment; then mapper becomes pass-through.

4. **Settings / receipts:**  
   - Backend stable envelope or flat body → delete `flattenUserSettingsPayload` layers or reduce iteration count.

5. **Receipt print / modal:**  
   - One shared `ReceiptDTO` mapper from OpenAPI types (or codegen) instead of duplicate `normalizeReceiptDto` / `normalizeReceiptDTO`.

---

## 5. Suggested PR order

1. Document-only (this file) + optional `paymentService` comment linking here.  
2. POS: v2 header feature flag + unit tests for envelope parse.  
3. Remove legacy branches in `normalizePaymentResponse` when safe.  
4. Consolidate receipt normalization (F+G).  
5. Settings unwrap simplification after backend check.

---

## 6. Tests to keep green

- `frontend/__tests__/paymentService.taxTypeNormalization.test.ts`  
- `frontend/__tests__/normalizePosPaymentMethods.test.ts`  
- `frontend/__tests__/normalizeUserSettingsResponse.test.ts`  
- `frontend/__tests__/posRegisterAssignmentSources.test.ts` (path guardrails)
