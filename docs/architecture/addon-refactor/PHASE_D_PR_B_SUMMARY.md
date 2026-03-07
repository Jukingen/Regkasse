# Phase D PR-B: Remove selectedModifiers from Active POS Add-to-Cart Write Path

**Goal:** Stop sending `selectedModifiers` from the active POS add-to-cart flow while keeping historical and backend compatibility.

**Constraints respected:** Add-on-as-product runtime unchanged; backend API contract intact; legacy cart/response reading preserved.

---

## Touched files

| File | Change |
|------|--------|
| `frontend/contexts/CartContext.tsx` | Add-item request body no longer sets `body.selectedModifiers`; comment Phase D PR-B. Optimistic/merge and response mapping unchanged. |
| `frontend/app/(tabs)/cash-register.tsx` | `handleAddProduct(product)` only (removed modifiers param); addItem called with `productName`/`unitPrice` only. `usePOSOrderFlow` addItem options type narrowed. |
| `frontend/services/api/cartService.ts` | Comments on `AddItemToCartRequest.selectedModifiers` and `UpdateCartItemRequest.selectedModifiers` (POS no longer sends; legacy compat). |
| `frontend/components/ProductList.tsx` | `onAddProduct?: (product: Product) => void` (removed second param). |
| `frontend/components/ProductRow.tsx` | `onAdd: (product: Product) => void`; row press calls `onAdd(product)`. `pendingModifiers` kept for display/memo. |
| `frontend/components/ProductGridCard.tsx` | Same: `onAdd: (product: Product) => void`; press calls `onAdd(product)`. |
| `frontend/__tests__/phaseDAddItemRequest.test.ts` | **New.** Contract tests: add-item request body never includes `selectedModifiers`; add-on flow N+1 bodies, none with modifiers. |
| `docs/architecture/addon-refactor/PHASE_D_PR_B_AUDIT.md` | Audit of write/read paths and minimal implementation plan (pre-implementation). |
| `docs/architecture/addon-refactor/PHASE_D_PR_B_SUMMARY.md` | This summary. |

---

## Removed

- **Add-item request:** `selectedModifiers` is no longer sent in `POST /cart/add-item` from any active POS path (row tap, add-on sheet, chip add-on).
- **Callback signature:** `handleAddProduct(product, modifiers)` → `handleAddProduct(product)`; `onAddProduct` / `onAdd` now `(product: Product) => void` (no modifiers argument on the write path).
- **Dead parameter:** `modifiers` argument removed from the POS add-to-cart callback chain (ProductList, ProductRow, ProductGridCard, usePOSOrderFlow).

---

## Kept for compatibility

- **Backend request types:** `AddItemToCartRequest.selectedModifiers?` and `UpdateCartItemRequest.selectedModifiers?` remain optional (backend still accepts; no contract change).
- **CartContext:**  
  - `addItem(..., options?)` still accepts `options.modifiers` for optimistic UI (merge key, optimistic line, line total).  
  - Response mapping: `item.SelectedModifiers ?? item.selectedModifiers ?? ...` → `modifierList` / `item.modifiers` in `fetchTableCart` and addItem response.  
  - `getModifierKey`, `getCartLineTotal`, `addModifier` / `incrementModifier` / `decrementModifier` / `removeModifier`, `persistModifiers` unchanged (legacy cart display and editing).
- **POS display state:** `pendingModifiersByProduct`, `selectedModifiersForProduct`, `lastCartItemModifiersByProductId` kept for chip/display; `pendingModifiers` prop still passed to ProductRow/ProductGridCard (memo, possible future use).
- **Backend responses:** Cart and table-order recovery still return `SelectedModifiers` per item; no backend changes in PR-B.
- **ModifierOptionChips:** `selectedModifiers` prop and `selectedModifiers={[]}` from list/grid unchanged (display only).

---

## Follow-up for PR-C

- **Backend request DTOs:** Optionally remove or formally deprecate `SelectedModifiers` from `AddItemToCartRequest` / `UpdateCartItemRequest` once no client sends them; keep response DTOs for legacy carts/table orders.
- **CartContext:** If/when deprecating legacy modifier lines, consider narrowing `options.modifiers` and related merge/optimistic logic; keep response reading until backend stops returning `SelectedModifiers` for items.
- **Payment:** Already no `modifierIds` from POS (PR-A); PR-C can align backend payment payload and types if needed.
- **Verification:** Optional integration test that mocks `apiClient`, renders CartProvider, calls `addItem`, and asserts POST body has no `selectedModifiers` (current tests use contract replication only).
