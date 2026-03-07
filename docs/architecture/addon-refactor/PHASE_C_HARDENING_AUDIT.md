# Phase C Hardening Audit: Legacy Compatibility Paths

**Scope:** POS runtime after Phase C completion. No refactor in this document; classification and recommended actions only.

---

## 1. AddItemToSpecificCart reachability from active POS create-flow

### Conclusion: **Not reachable from POS create-flow. Legacy / Phase D cleanup candidate.**

| Layer | Endpoint / API | Used by POS? |
|-------|----------------|---------------|
| Backend | `POST /api/cart/{cartId}/items` → `AddItemToSpecificCart` | No |
| Frontend | `cartService.addItemToSpecificCart(cartId, request)` | Never called |
| Frontend | `apiClient.post(\`/cart/${currentCartId}/items\`, { productId, quantity })` | Yes, but **not in POS** |

**Call chains:**

- **POS (cash-register) create-flow:**  
  `ProductRow` / `ProductGridCard` tap → `handleAddProduct` or `onOpenAddOnSheet` → `addItemWithAddOns` or `addItem` from **CartContext** → `apiClient.post('/cart/add-item', body)` → backend **AddItemToCart** (POST `add-item`).  
  **No path uses `AddItemToSpecificCart`.**

- **Non-POS (CartScreen):**  
  `CartScreen` → `useApiCart()` → `addItem()` → `apiClient.post(\`/cart/${currentCartId}/items\`, { productId, quantity })` → backend **AddItemToSpecificCart**.  
  So **AddItemToSpecificCart is only used by the CartScreen / useApiCart flow** (cart-by-id, no table number).

**Risk:** None for active POS. AddItemToSpecificCart does not accept `SelectedModifiers` (unlike AddItemToCart); it only adds a product line. If CartScreen is deprecated or migrated to table-based cart, this endpoint becomes dead from the frontend.

**Recommended action:** Classify as **legacy / Phase D cleanup candidate**. Optionally mark backend `AddItemToSpecificCart` as obsolete when CartScreen is retired or switched to `add-item`; leave as-is until then.

---

## 2. ModifierSelectionModal and ProductExtrasInline – use in active POS

### ModifierSelectionModal

| Check | Result |
|-------|--------|
| **Import / use** | **Not imported** in `frontend/app/`, `frontend/app/(tabs)/`, or `cash-register.tsx`. Only appears in its own file `frontend/components/ModifierSelectionModal.tsx` and in docs under `docs/architecture/addon-refactor/`. |
| **Classification** | **Dormant.** Not mounted in any active POS flow. POS uses `ModifierSelectionBottomSheet` only. |
| **Recommended action** | **Phase D:** Remove from codebase or repurpose; drop deprecated `onAdd` when no callers exist. Not trivially dead (component is complete and buildable) but **removable** in Phase D. |

### ProductExtrasInline

| Check | Result |
|-------|--------|
| **Import / use** | **Not imported** anywhere in the repo (only in `frontend/components/ProductExtrasInline.tsx` and docs). |
| **Classification** | **Dead code.** No references. |
| **Recommended action** | **Removable now** if desired (isolated component). Alternatively **Phase D** to avoid churn; document as dead. |

---

## 3. Payment flow: when is modifierIds generated?

### Exact condition

**File:** `frontend/components/PaymentModal.tsx` (lines 281–286)

```ts
modifierIds: (item.modifiers?.length ? (item.modifiers as any[]).map((m: any) => m.id ?? m.modifierId).filter(Boolean) : undefined)
```

- **Condition:** `item.modifiers?.length` is truthy.
- **Effect:** `modifierIds` is set only when the cart line has at least one modifier attached.

**New add-on-as-product flow:** Lines are added via `addItem(productId, 1, { productName, unitPrice })` with **no** `modifiers` option. Backend returns lines without `SelectedModifiers`; CartContext does not set `modifiers` on those items. So for these lines `item.modifiers` is undefined or empty → **modifierIds is undefined**.

**Conclusion:** modifierIds is generated **only for historical / legacy cart lines** that already have `item.modifiers` (e.g. from add-item with SelectedModifiers or from cart hydration of old data). **Not** for the new add-on-as-product flow.

---

## 4. Cart hydration: legacy modifiers preserved – impact on active POS add-on flow

**Where legacy modifiers are preserved:**

- **CartContext.tsx – fetchTableCart (lines 211–224):**  
  When mapping backend items to local state, if backend sends no modifiers for an item (`!Array.isArray(backendMods) || backendMods.length === 0`), code falls back to `existing?.modifiers` from current local state:  
  `} else if (existing?.modifiers?.length) { modifierList = existing.modifiers; }`.

- **CartContext.tsx – addItem response mapping (lines 409–423):**  
  Same pattern: if backend item has no mods, use `existing?.modifiers` for the same item.

**Can this affect active POS add-on product flows?**

- **No.** New add-on flow creates lines that (1) are created with no modifiers in the request, (2) backend returns with no SelectedModifiers, (3) have no prior `existing` in state (new item) or have `existing` without modifiers. So the fallback `existing?.modifiers` either does not apply (new item) or keeps an already-empty modifiers list.
- The fallback only matters when (a) backend omits modifiers for an item that (b) already exists in local state with modifiers (e.g. legacy line from before refresh). So it only preserves **existing** legacy modifiers across refresh/hydration.

**Conclusion:** Preservation of legacy modifiers in hydration affects **only historical compatibility** (display and payment for old lines). It does **not** affect active POS add-on-as-product flows.

---

## 5. Classification of remaining legacy modifier references

### Safe compatibility (keep; no change for Phase C)

| Location | What | Why safe |
|----------|------|----------|
| `frontend/contexts/CartContext.tsx` | CartItem.modifiers, getModifierKey, addItem(options.modifiers), body.selectedModifiers, addModifier/incrementModifier/decrementModifier/removeModifier, persistModifiers, fetchTableCart/addItem response mapping (backendMods, existing?.modifiers fallback), getCartLineTotal, recalcLineTotal, lineKey using getModifierKey(i.modifiers) | Required for existing carts, backend contract, and payment payload for legacy lines. New add-on flow does not use modifiers on the line. |
| `frontend/app/(tabs)/cash-register.tsx` | handleAddProduct(..., modifiers), handleAddModifier, lastCartItemModifiersByProductId, selectedModifiersForProduct, pendingModifiersByProduct | Supports legacy inline-chip path and display of existing lines with modifiers. Add-on sheet path does not use these for new lines. |
| `frontend/components/CartDisplay.tsx` | modifierKey from item.modifiers (line 99) | Stable key for items that may have modifiers; harmless when modifiers empty. |
| `frontend/components/CartItemRow.tsx` | item.modifiers render and total (lines 53–55, 102) | Renders legacy lines with modifiers; no modifiers for add-on-only lines. |
| `frontend/components/PaymentModal.tsx` | modifierIds from item.modifiers?.length (line 286) | Only set for lines that have modifiers (legacy); add-on lines have no modifiers. |
| `frontend/app/(tabs)/_layout.tsx` | cartItems modifiers mapping (line 126) | Passes through to payment modal; same as above. |
| `frontend/services/api/cartService.ts` | AddItemToCartRequest.selectedModifiers, comment | Backend still accepts; CartContext sends only when options.modifiers present (legacy path). |
| `frontend/services/api/paymentService.ts` | PaymentItem.modifierIds, comment | Backend payment contract; only populated for legacy lines. |
| ProductRow / ProductGridCard | modifiers prop (group.products mapped to chip shape), selectedModifiers={[]}, modifiersKey(pendingModifiers), onAdd(product, modifiers) | Chips show add-on products; pendingModifiers/onAdd support legacy “add with modifiers” when user does not use sheet. |

### Risky compatibility (monitor; no change unless evidence)

| Location | What | Risk | Action |
|----------|------|------|--------|
| None identified | — | — | All current legacy paths are either backend/display compatibility or optional legacy POS path. No evidence that new add-on flow can write or read modifiers incorrectly. |

### Removable now (trivially dead and isolated)

| Location | What | Recommended action |
|----------|------|---------------------|
| `frontend/components/ProductExtrasInline.tsx` | Entire component | **Optional:** Delete (no imports). Or leave and mark Phase D to avoid churn. |

### Phase D

| Location | What | Recommended action |
|----------|------|---------------------|
| Backend `AddItemToSpecificCart` (POST `cart/{cartId}/items`) | Endpoint only used by CartScreen/useApiCart | Mark obsolete when CartScreen is retired or migrated to table-based add-item; consider removing. |
| Frontend `cartService.addItemToSpecificCart` | Never called | Remove when CartScreen is migrated or removed. |
| `frontend/components/ModifierSelectionModal.tsx` | Not used in POS | Remove or repurpose; drop deprecated `onAdd` prop. |
| `frontend/components/ProductExtrasInline.tsx` | Not imported | Remove if not repurposed for “edit line” add-ons. |
| Backend SelectedModifiers on add-item | Accepted but ignored for write | When no client sends SelectedModifiers, remove from request DTO and stop logging; keep read compat for CartItem. |
| ModifierGroupDto.modifiers (API/type) | Always [] in POS | When backend stops returning, remove from DTO and product types. |

### Phase E (later / API contract)

| Location | What | Recommended action |
|----------|------|---------------------|
| CartItem.modifiers (frontend type and backend CartItemModifier) | Persisted for legacy lines and payment | After backend removes modifierIds from payment and no longer returns SelectedModifiers on cart items, remove from frontend CartItem and payment payload. |
| Payment API modifierIds | Still part of payment request | When backend drops modifierIds from payment contract, remove from PaymentItem and PaymentModal. |

---

## Summary table

| Item | Reachable from POS? | Classification | Action |
|------|---------------------|----------------|--------|
| AddItemToSpecificCart | No (only CartScreen/useApiCart) | Legacy / Phase D | Document; obsolete when CartScreen migrated. |
| ModifierSelectionModal | Not used in POS | Dormant | Phase D: remove or repurpose. |
| ProductExtrasInline | Not imported | Dead | Removable now (optional) or Phase D. |
| modifierIds in payment | Only when item.modifiers?.length | Legacy lines only | No change. |
| Cart hydration existing?.modifiers | Preserves legacy only | Historical compat | No change. |
| All other modifier references | — | Safe compatibility / Phase D / Phase E | As in section 5. |
